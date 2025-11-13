using HttpLibrary.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	public static class HttpRequestExecutor
	{
		/// <summary>
		/// Executes an HTTP GET request to the specified URL using the provided client.
		/// Returns the response body as string. For binary content use GetBinaryAsync.
		/// </summary>
		public static async Task<string> GetAsync(IPooledHttpClient client, ILogger logger, string url, TimeSpan? timeout = null, int redirectCount = 0, CancellationToken cancellationToken = default, System.Collections.Generic.HashSet<string>? visitedUris = null)
		{
			if(client == null)
			{
				throw new ArgumentNullException(nameof(client));
			}
			if(logger == null)
			{
				throw new ArgumentNullException(nameof(logger));
			}
			if(string.IsNullOrWhiteSpace(url))
			{
				throw new ArgumentException("url is required", nameof(url));
			}

			// Extract base address from URL for cookie persistence
			Uri? baseUri = CookieHelper.ExtractAndSetCookieBaseAddress(
				url,
				client.Name ?? Constants.DefaultClientName,
				logger);

			TimeSpan actualTimeout = timeout ?? Constants.DefaultRequestTimeout;

			// Combine parent cancellation token with timeout-based token
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(actualTimeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				// Create request manually to use SendRawAsync
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

				// Use SendRawAsync to avoid EnsureSuccessStatusCode() which would throw on redirects
				using HttpResponseMessage response = await client.SendRawAsync(request, linkedCts.Token).ConfigureAwait(false);

				// Diagnostic: log actual response URI and status so we can detect if handler auto-followed redirects
				try
				{
					Uri? actualResponseUri = response.RequestMessage?.RequestUri;
					logger.LogDebug("Response received for request {RequestedUri} -> response.RequestMessage.RequestUri={ResponseUri}, Status={Status}", url, actualResponseUri?.ToString() ?? "(null)", (int)response.StatusCode);
					if(actualResponseUri != null && !string.Equals(actualResponseUri.ToString(), request.RequestUri?.ToString(), StringComparison.OrdinalIgnoreCase))
					{
						logger.LogDebug("HttpMessageHandler appears to have followed a redirect automatically from {Original} to {Actual}", request.RequestUri?.ToString(), actualResponseUri.ToString());
					}
				}
				catch
				{
					// best-effort logging only
				}

				// Handle redirects manually if present
				if(IsRedirectStatusCode((int)response.StatusCode))
				{
					logger.LogDebug("Redirect response detected for {Url}: {StatusCode}", url, (int)response.StatusCode);
					if(response.Headers.Location != null)
					{
						logger.LogDebug("Location header: {Location}", response.Headers.Location);
					}

					// Delegate to shared redirect handling so callbacks and limits are respected
					string redirectResult = await HandleRedirectAsync(client, logger, response, baseUri, linkedCts.Token, redirectCount, cancellationToken, originalMethod: null, visitedUris: visitedUris).ConfigureAwait(false);
					return redirectResult ?? string.Empty;
				}

				// Check for success status
				if(!response.IsSuccessStatusCode)
				{
					LoggingDefinitions.HttpErrorStatus(logger, (int)response.StatusCode, response.ReasonPhrase ?? Constants.UnknownStatusText);

					// Don't try to read error response content - it may cause HTTP/2 stream errors
					// Some servers (like Facebook) send RST_STREAM immediately after error responses
					// Attempting to read would cause: "The HTTP/2 server reset the stream. HTTP/2 error code 'STREAM_CLOSED' (0x5)"


					return string.Empty;
				}

				// Process Set-Cookie headers
				CookieHelper.ProcessSetCookieHeaders(
					response,
					baseUri,
					client.Name ?? Constants.DefaultClientName,
					logger);

				string textResponse = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
				LoggingDefinitions.ResponseLength(logger, textResponse.Length);

				// Debug: Log response headers
				if(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace))
				{
					logger.LogTrace("=== Response Headers ===");
					foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in response.Headers)
					{
						logger.LogTrace("{HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
					}

					if(response.Content?.Headers != null)
					{
						foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in response.Content.Headers)
						{
							logger.LogTrace("{HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
						}
					}
				}

				// Debug: Pretty-print JSON responses
				if(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace) && !string.IsNullOrWhiteSpace(textResponse))
				{
					try
					{
						using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(textResponse);
						using System.IO.MemoryStream ms = new System.IO.MemoryStream();
						using(System.Text.Json.Utf8JsonWriter writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = true }))
						{
							doc.WriteTo(writer);
							writer.Flush();
						}
						string prettyJson = System.Text.Encoding.UTF8.GetString(ms.ToArray());
						logger.LogTrace("JSON Response (pretty-printed):");
						logger.LogTrace("{Json}", prettyJson);
					}
					catch
					{
						// Not JSON or invalid JSON, skip pretty-printing
					}
				}

				CookiePersistence.SaveCookies();
				LoggingDefinitions.CookiesSaved(logger);

				// Return response body to caller for CLI to write to stdout
				return textResponse;
			}
			catch(HttpRequestException hre)
			{
				if(hre.StatusCode.HasValue)
				{
					LoggingDefinitions.HttpErrorStatus(logger, (int)hre.StatusCode.Value, hre.StatusCode.Value.ToString());
				}
				else
				{
					LoggingDefinitions.RequestErrorMessage(logger, hre.Message);
				}
				return string.Empty;
			}
			catch(OperationCanceledException oce) when(linkedCts.IsCancellationRequested)
			{
				if(cancellationToken.IsCancellationRequested)
				{
					logger.LogError(oce, "Request canceled by caller");
				}
				else if(timeoutCts.IsCancellationRequested)
				{
					logger.LogError(oce, "Request timed out");
				}
				else
				{
					logger.LogError(oce, "Request canceled");
				}
				return string.Empty;
			}
			catch(OperationCanceledException)
			{
				// Rethrow OperationCanceledException produced by callbacks or explicit cancellations so callers can observe it
				throw;
			}
			catch(Exception ex)
			{
				logger.LogError(ex, "Request failed");
				return string.Empty;
			}
		}

		private static bool IsRedirectStatusCode(int statusCode)
		{
			return statusCode == 301 || statusCode == 302 || statusCode == 303 || statusCode == 307 || statusCode == 308;
		}

		private static async Task<string> HandleRedirectAsync(IPooledHttpClient client, ILogger logger, HttpResponseMessage response, Uri? originalUri, CancellationToken linkedToken, int redirectCount, CancellationToken parentToken, HttpMethod? originalMethod = null, System.Collections.Generic.HashSet<string>? visitedUris = null)
		{
			// Initialize visited set if needed (track absolute URIs as strings, case-insensitive)
			if(visitedUris == null)
			{
				visitedUris = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
				if(originalUri != null)
				{
					visitedUris.Add(originalUri.ToString());
				}
			}

			// Check if cancellation has been requested
			parentToken.ThrowIfCancellationRequested();

			// Check if we've exceeded the maximum redirect limit
			if(redirectCount >= client.MaxRedirections)
			{
				logger.LogWarning("Maximum redirect limit ({MaxRedirections}) exceeded. Stopping redirect chain.", client.MaxRedirections);
				return string.Empty;
			}

			Uri? redirectUri = response.Headers.Location;
			if(redirectUri == null)
			{
				LoggingDefinitions.RedirectNoLocation(logger, ( ( (int)response.StatusCode ) ).ToString());
				return string.Empty;
			}

			// Make absolute if relative
			if(!redirectUri.IsAbsoluteUri && originalUri != null)
			{
				redirectUri = new Uri(originalUri, redirectUri);
			}

			string redirectUriString = redirectUri.ToString();

			// Detect loops: if we've already visited this URI, stop early
			if(visitedUris.Contains(redirectUriString))
			{
				logger.LogWarning("Redirect loop detected. URI {RedirectUri} was already visited. Stopping redirect chain.", redirectUriString);
				return string.Empty;
			}

			// Mark this URI as visited before following
			visitedUris.Add(redirectUriString);

			HttpMethod methodToUse = originalMethod ?? HttpMethod.Get;

			// RFC7231:303 See Other should change method to GET
			if((int)response.StatusCode == 303)
			{
				methodToUse = HttpMethod.Get;
			}

			// Invoke redirect callback if set
			if(client.RedirectCallback != null)
			{
				HttpRedirectInfo redirectInfo = new HttpRedirectInfo(
					client.Name ?? Constants.DefaultClientName,
					originalUri?.ToString() ?? string.Empty,
					redirectUri.ToString(),
					(int)response.StatusCode,
					redirectCount,
					methodToUse,
					parentToken);

				logger.LogInformation("Invoking redirect callback for client '{ClientName}' to {RedirectUrl}", redirectInfo.ClientName, redirectInfo.RedirectUrl);
				logger.LogDebug("Redirect callback invoked for client '{ClientName}' -> {RedirectUrl}", redirectInfo.ClientName, redirectInfo.RedirectUrl);

				RedirectAction action;
				try
				{
					action = client.RedirectCallback(redirectInfo);
				}
				catch(OperationCanceledException)
				{
					logger.LogInformation("Redirect canceled by callback (OperationCanceledException thrown)");
					throw;
				}

				logger.LogInformation("Redirect callback returned action {Action}", action);
				logger.LogDebug("Redirect callback returned: {Action}", action);

				if(action == RedirectAction.Cancel)
				{
					logger.LogInformation("Redirect canceled by callback (RedirectAction.Cancel)");
					throw new OperationCanceledException("Redirect canceled by callback");
				}
				else if(action == RedirectAction.Stop)
				{
					logger.LogInformation("Redirect stopped by callback");
					return string.Empty;
				}
				else if(action == RedirectAction.FollowWithGet)
				{
					methodToUse = HttpMethod.Get;
				}
			}
			else
			{
				logger.LogInformation("No redirect callback registered for client '{ClientName}' - following redirect by default according to handler settings", client.Name ?? Constants.DefaultClientName);
				logger.LogDebug("No redirect callback registered for client '{ClientName}' - default handling applies", client.Name ?? Constants.DefaultClientName);
			}

			// Follow the redirect
			LoggingDefinitions.FollowingRedirect(logger, redirectUri.ToString(), redirectCount + 1, client.MaxRedirections);

			// Recursively fetch the redirect URL with incremented redirect count
			if(methodToUse == HttpMethod.Get)
			{
				string result = await GetAsync(client, logger, redirectUri.ToString(), null, redirectCount + 1, parentToken, visitedUris).ConfigureAwait(false);
				return result;
			}
			else
			{
				// For non-GET methods, use the generic send method (without content on redirects)
				string result = await SendRequestAsync(client, logger, methodToUse, redirectUri.ToString(), content: null, timeout: null, cancellationToken: parentToken, redirectCount: redirectCount + 1, visitedUris: visitedUris).ConfigureAwait(false);
				return result;
			}
		}

		/// <summary>
		/// Executes an HTTP POST request to the specified URL using the provided client.
		/// Returns response body as string.
		/// </summary>
		public static async Task<string> PostAsync(IPooledHttpClient client, ILogger logger, string url, HttpContent content, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return await SendRequestAsync(client, logger, HttpMethod.Post, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes an HTTP PUT request to the specified URL using the provided client.
		/// Returns response body as string.
		/// </summary>
		public static async Task<string> PutAsync(IPooledHttpClient client, ILogger logger, string url, HttpContent content, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return await SendRequestAsync(client, logger, HttpMethod.Put, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes an HTTP DELETE request to the specified URL using the provided client.
		/// Returns response body as string.
		/// </summary>
		public static async Task<string> DeleteAsync(IPooledHttpClient client, ILogger logger, string url, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return await SendRequestAsync(client, logger, HttpMethod.Delete, url, content: null, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes an HTTP PATCH request to the specified URL using the provided client.
		/// Returns response body as string.
		/// </summary>
		public static async Task<string> PatchAsync(IPooledHttpClient client, ILogger logger, string url, HttpContent content, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return await SendRequestAsync(client, logger, HttpMethod.Patch, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes an HTTP HEAD request to the specified URL using the provided client.
		/// Returns response body as string.
		/// </summary>
		public static async Task<string> HeadAsync(IPooledHttpClient client, ILogger logger, string url, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return await SendRequestAsync(client, logger, HttpMethod.Head, url, content: null, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes an HTTP OPTIONS request to the specified URL using the provided client.
		/// Returns response body as string.
		/// </summary>
		public static async Task<string> OptionsAsync(IPooledHttpClient client, ILogger logger, string url, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return await SendRequestAsync(client, logger, HttpMethod.Options, url, content: null, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes an HTTP TRACE request to the specified URL using the provided client.
		/// Returns response body as string.
		/// </summary>
		public static async Task<string> TraceAsync(IPooledHttpClient client, ILogger logger, string url, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return await SendRequestAsync(client, logger, HttpMethod.Trace, url, content: null, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes an HTTP CONNECT request to the specified URL using the provided client.
		/// Returns no value; textual response (if any) is ignored by this helper.
		/// </summary>
		public static async Task ConnectAsync(IPooledHttpClient client, ILogger logger, string url, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			// Reuse string-based request and discard the result
			await SendRequestAsync(client, logger, HttpMethod.Connect, url, content: null, timeout, cancellationToken).ConfigureAwait(false);
		}

		#region Binary Data Methods

		/// <summary>
		/// Uploads binary data via POST request with application/octet-stream content type.
		/// </summary>
		public static async Task<byte[]> PostBinaryAsync(IPooledHttpClient client, ILogger logger, string url, ReadOnlyMemory<byte> data, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			ByteArrayContent content = new ByteArrayContent(data.ToArray());
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Constants.MediaTypeOctetStream);
			return await SendBinaryRequestAsync(client, logger, HttpMethod.Post, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Uploads binary data via PUT request with application/octet-stream content type.
		/// </summary>
		public static async Task<byte[]> PutBinaryAsync(IPooledHttpClient client, ILogger logger, string url, ReadOnlyMemory<byte> data, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			ByteArrayContent content = new ByteArrayContent(data.ToArray());
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Constants.MediaTypeOctetStream);
			return await SendBinaryRequestAsync(client, logger, HttpMethod.Put, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Uploads binary data via PATCH request with application/octet-stream content type.
		/// </summary>
		public static async Task<byte[]> PatchBinaryAsync(IPooledHttpClient client, ILogger logger, string url, ReadOnlyMemory<byte> data, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			ByteArrayContent content = new ByteArrayContent(data.ToArray());
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Constants.MediaTypeOctetStream);
			return await SendBinaryRequestAsync(client, logger, HttpMethod.Patch, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Downloads binary data via GET request and returns as byte array.
		/// </summary>
		public static async Task<byte[]> GetBinaryAsync(IPooledHttpClient client, ILogger logger, string url, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return await SendBinaryRequestAsync(client, logger, HttpMethod.Get, url, content: null, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Uploads a file via POST request with application/octet-stream content type.
		/// </summary>
		/// <param name="client">The HTTP client to use for the request</param>
		/// <param name="logger">Logger for diagnostic information</param>
		/// <param name="url">The target URL</param>
		/// <param name="filePath">Path to the file to upload</param>
		/// <param name="timeout">Optional timeout for the request</param>
		/// <param name="cancellationToken">Cancellation token for the operation</param>
		/// <returns>Response content as byte array</returns>
		public static async Task<byte[]> PostFileAsync(IPooledHttpClient client, ILogger logger, string url, string filePath, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			if(!System.IO.File.Exists(filePath))
			{
				throw new System.IO.FileNotFoundException("File not found", filePath);
			}

			using System.IO.FileStream fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, bufferSize: Constants.StreamBufferSize, useAsync: true);
			StreamContent content = new StreamContent(fileStream, bufferSize: Constants.StreamBufferSize);
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Constants.MediaTypeOctetStream);

			logger.LogInformation("Uploading file: {FilePath} ({FileSize} bytes)", filePath, fileStream.Length);
			return await SendBinaryRequestAsync(client, logger, HttpMethod.Post, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Uploads a file via PUT request with application/octet-stream content type.
		/// </summary>
		public static async Task<byte[]> PutFileAsync(IPooledHttpClient client, ILogger logger, string url, string filePath, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			if(!System.IO.File.Exists(filePath))
			{
				throw new System.IO.FileNotFoundException("File not found", filePath);
			}

			using System.IO.FileStream fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, bufferSize: Constants.StreamBufferSize, useAsync: true);
			StreamContent content = new StreamContent(fileStream, bufferSize: Constants.StreamBufferSize);
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Constants.MediaTypeOctetStream);

			logger.LogInformation("Uploading file: {FilePath} ({FileSize} bytes)", filePath, fileStream.Length);
			return await SendBinaryRequestAsync(client, logger, HttpMethod.Put, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Downloads binary data via GET request and saves to a file.
		/// </summary>
		/// <param name="client">The HTTP client to use for the request</param>
		/// <param name="logger">Logger for diagnostic information</param>
		/// <param name="url">The target URL</param>
		/// <param name="outputFilePath">Path where the downloaded file should be saved</param>
		/// <param name="timeout">Optional timeout for the request (defaults to5 minutes)</param>
		/// <param name="cancellationToken">Cancellation token for the operation</param>
		/// <returns>Number of bytes downloaded</returns>
		public static async Task<long> DownloadToFileAsync(IPooledHttpClient client, ILogger logger, string url, string outputFilePath, TimeSpan? timeout = null, CancellationToken cancellationToken = default, int redirectCount = 0, System.Collections.Generic.HashSet<string>? visitedUris = null)
		{
			if(client == null)
			{
				throw new ArgumentNullException(nameof(client));
			}
			if(logger == null)
			{
				throw new ArgumentNullException(nameof(logger));
			}
			if(string.IsNullOrWhiteSpace(url))
			{
				throw new ArgumentException("url is required", nameof(url));
			}
			if(string.IsNullOrWhiteSpace(outputFilePath))
			{
				throw new ArgumentException("outputFilePath is required", nameof(outputFilePath));
			}

			// Create a local non-nullable reference so the compiler knows client is not null
			IPooledHttpClient localClient = client;

			// Ensure directory exists
			string? directory = System.IO.Path.GetDirectoryName(outputFilePath);
			if(!string.IsNullOrWhiteSpace(directory) && !System.IO.Directory.Exists(directory))
			{
				System.IO.Directory.CreateDirectory(directory);
			}

			TimeSpan actualTimeout = timeout ?? Constants.DefaultBinaryOperationTimeout;

			// Combine parent cancellation token with timeout-based token
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(actualTimeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

				using HttpResponseMessage response = await client.SendRawAsync(request, linkedCts.Token).ConfigureAwait(false);

				// Handle redirects manually if present
				if(IsRedirectStatusCode((int)response.StatusCode))
				{
					logger.LogDebug("Redirect response detected for {Url}: {StatusCode}", url, (int)response.StatusCode);
					if(response.Headers.Location != null)
					{
						logger.LogDebug("Location header: {Location}", response.Headers.Location);
					}

					Uri? redirectUri = response.Headers.Location;
					if(redirectUri == null)
					{
						LoggingDefinitions.RedirectNoLocation(logger, ( (int)response.StatusCode ).ToString());
						return 0;
					}

					if(!redirectUri.IsAbsoluteUri && request.RequestUri != null)
					{
						redirectUri = new Uri(request.RequestUri, redirectUri);
					}

					// invoke redirect callback if present and respect its decision
					HttpMethod methodToUse = HttpMethod.Get;
					if((int)response.StatusCode == 303)
					{
						methodToUse = HttpMethod.Get;
					}

					if(localClient.RedirectCallback != null)
					{
						HttpRedirectInfo redirectInfo = new HttpRedirectInfo(localClient.Name ?? Constants.DefaultClientName, url, redirectUri.ToString(), (int)response.StatusCode, redirectCount, methodToUse, cancellationToken);
						RedirectAction action;
						try
						{
							action = localClient.RedirectCallback(redirectInfo);
						}
						catch(OperationCanceledException)
						{
							logger.LogInformation("Redirect canceled by callback (OperationCanceledException thrown)");
							throw;
						}

						if(action == RedirectAction.Cancel)
						{
							logger.LogInformation("Redirect canceled by callback (RedirectAction.Cancel)");
							throw new OperationCanceledException("Redirect canceled by callback");
						}
						else if(action == RedirectAction.Stop)
						{
							logger.LogInformation("Redirect stopped by callback");
							return 0;
						}
						else if(action == RedirectAction.FollowWithGet)
						{
							methodToUse = HttpMethod.Get;
						}
					}

					// Follow the redirect by recursively calling DownloadToFileAsync
					LoggingDefinitions.FollowingRedirect(logger, redirectUri.ToString(), redirectCount + 1, localClient.MaxRedirections);
					return await DownloadToFileAsync(localClient, logger, redirectUri.ToString(), outputFilePath, timeout, cancellationToken, redirectCount + 1, visitedUris).ConfigureAwait(false);
				}

				if(!response.IsSuccessStatusCode)
				{
					logger.LogError("HTTP error: {StatusCode} ({StatusText})", (int)response.StatusCode, response.ReasonPhrase ?? Constants.UnknownStatusText);
					throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
				}

				long? totalBytes = response.Content.Headers.ContentLength;
				string totalBytesText = totalBytes.HasValue ? totalBytes.Value.ToString() : "unknown";
				logger.LogInformation("Downloading to file: {FilePath} ({TotalBytes} bytes)", outputFilePath, totalBytesText);

				using System.IO.FileStream fileStream = new System.IO.FileStream(outputFilePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, bufferSize: Constants.StreamBufferSize, useAsync: true);
				using System.IO.Stream contentStream = await response.Content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);

				byte[] buffer = new byte[ Constants.StreamBufferSize ];
				int bytesRead;
				long totalBytesRead = 0;
				int lastReportedPercentage = -1;
				System.DateTimeOffset lastProgressUpdate = System.DateTimeOffset.MinValue;
				// Determine granularity for progress events (use client-config if available)
				long granularity = Constants.DefaultProgressGranularityBytes; // kept for potential future use
				try
				{
					if(localClient is not null && HttpLibrary.ServiceConfiguration.AppConfig != null && HttpLibrary.ServiceConfiguration.AppConfig.ProgressDisplay != null)
					{
						granularity = Math.Max(1, HttpLibrary.ServiceConfiguration.AppConfig.ProgressDisplay.ProgressEventGranularityBytes);
					}
				}
				catch
				{
					// Best-effort; keep default granularity
				}

				while(( bytesRead = await contentStream.ReadAsync(buffer, linkedCts.Token).ConfigureAwait(false) ) > 0)
				{
					await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), linkedCts.Token).ConfigureAwait(false);
					totalBytesRead += bytesRead;

					// inside DownloadToFileAsync: use null-conditional for progress callback
					System.Action<HttpProgressInfo>? progressCallback = localClient?.ProgressCallback;
					if(progressCallback != null)
					{
						System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
						int currentPct = -1;
						if(totalBytes.HasValue && totalBytes.Value > 0)
						{
							currentPct = (int)( (double)totalBytesRead / totalBytes.Value * 100.0 );
						}

						bool shouldReport = false;
						if(( now - lastProgressUpdate ).TotalMilliseconds >= 1500)
						{
							shouldReport = true;
						}
						else if(currentPct >= 0 && currentPct != lastReportedPercentage)
						{
							shouldReport = true;
						}

						if(shouldReport)
						{
							HttpProgressInfo info = new HttpProgressInfo(localClient?.Name ?? Constants.DefaultClientName, url)
							{
								Stage = HttpProgressStage.DownloadingContent,
								BytesTransferred = totalBytesRead,
								TotalBytes = totalBytes,
								Message = null
							};
							progressCallback(info);
							lastReportedPercentage = currentPct;
							lastProgressUpdate = now;
						}
					}
				}

				await fileStream.FlushAsync(linkedCts.Token).ConfigureAwait(false);

				// Final progress - completed
				System.Action<HttpProgressInfo>? finalCallback = localClient?.ProgressCallback;
				if(finalCallback != null)
				{
					HttpProgressInfo doneInfo = new HttpProgressInfo(localClient?.Name ?? Constants.DefaultClientName, url)
					{
						Stage = HttpProgressStage.Completed,
						BytesTransferred = totalBytesRead,
						TotalBytes = totalBytes,
						Message = "Completed"
					};
					finalCallback(doneInfo);
				}

				logger.LogInformation("Download complete: {FilePath} ({TotalBytes} bytes)", outputFilePath, totalBytesRead);

				return totalBytesRead;
			}
			catch(HttpRequestException hre)
			{
				if(hre.StatusCode.HasValue)
				{
					logger.LogError("HTTP error: {StatusCode} ({StatusText}) - {Message}", (int)hre.StatusCode.Value, hre.StatusCode.Value, hre.Message);
				}
				else
				{
					logger.LogError("Request error: {Message}", hre.Message);
				}
				throw;
			}
			catch(OperationCanceledException oce) when(linkedCts.IsCancellationRequested)
			{
				if(cancellationToken.IsCancellationRequested)
				{
					logger.LogError(oce, "Download canceled by caller");
				}
				else if(timeoutCts.IsCancellationRequested)
				{
					logger.LogError(oce, "Download timed out");
				}
				else
				{
					logger.LogError(oce, "Download canceled");
				}
				throw;
			}
			catch(OperationCanceledException)
			{
				// Rethrow OperationCanceledException produced by callbacks or explicit cancellations so callers can observe it
				throw;
			}
			catch(Exception ex)
			{
				logger.LogError(ex, "Download failed");
				throw;
			}
		}

		/// <summary>
		/// Uploads binary data from a stream via POST request with application/octet-stream content type.
		/// </summary>
		public static async Task<byte[]> PostStreamAsync(IPooledHttpClient client, ILogger logger, string url, System.IO.Stream inputStream, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			if(inputStream == null)
			{
				throw new ArgumentNullException(nameof(inputStream));
			}

			StreamContent content = new StreamContent(inputStream, bufferSize: Constants.StreamBufferSize);
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Constants.MediaTypeOctetStream);

			long streamLength = inputStream.CanSeek ? inputStream.Length : -1;
			logger.LogInformation("Uploading stream: {StreamLength} bytes", streamLength >= 0 ? streamLength.ToString() : "unknown size");

			return await SendBinaryRequestAsync(client, logger, HttpMethod.Post, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Uploads binary data from a stream via PUT request with application/octet-stream content type.
		/// </summary>
		public static async Task<byte[]> PutStreamAsync(IPooledHttpClient client, ILogger logger, string url, System.IO.Stream inputStream, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			if(inputStream == null)
			{
				throw new ArgumentNullException(nameof(inputStream));
			}

			StreamContent content = new StreamContent(inputStream, bufferSize: Constants.StreamBufferSize);
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Constants.MediaTypeOctetStream);

			long streamLength = inputStream.CanSeek ? inputStream.Length : -1;
			logger.LogInformation("Uploading stream: {StreamLength} bytes", streamLength >= 0 ? streamLength.ToString() : "unknown size");

			return await SendBinaryRequestAsync(client, logger, HttpMethod.Put, url, content, timeout, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Generic method to send binary HTTP requests and receive binary responses.
		/// </summary>
		private static async Task<byte[]> SendBinaryRequestAsync(IPooledHttpClient client, ILogger logger, HttpMethod method, string url, HttpContent? content, TimeSpan? timeout = null, CancellationToken cancellationToken = default, int redirectCount = 0, System.Collections.Generic.HashSet<string>? visitedUris = null)
		{
			if(client == null)
			{
				throw new ArgumentNullException(nameof(client));
			}
			if(logger == null)
			{
				throw new ArgumentNullException(nameof(logger));
			}
			if(string.IsNullOrWhiteSpace(url))
			{
				throw new ArgumentException("url is required", nameof(url));
			}

			// Extract base address from URL for cookie persistence
			Uri? baseUri = CookieHelper.ExtractAndSetCookieBaseAddress(
				url,
				client.Name ?? Constants.DefaultClientName,
				logger);

			TimeSpan actualTimeout = timeout ?? Constants.DefaultBinaryOperationTimeout;

			// Combine parent cancellation token with timeout-based token
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(actualTimeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				HttpRequestMessage request = new HttpRequestMessage(method, url);
				if(content != null)
				{
					request.Content = content;
				}

				using HttpResponseMessage response = await client.SendRawAsync(request, linkedCts.Token).ConfigureAwait(false);

				// Handle redirect responses
				if(IsRedirectStatusCode((int)response.StatusCode))
				{
					// Similar flow to HandleRedirectAsync but for binary responses: invoke callback and follow by calling appropriate binary/text helper
					Uri? redirectUri = response.Headers.Location;
					if(redirectUri == null)
					{
						LoggingDefinitions.RedirectNoLocation(logger, ( (int)response.StatusCode ).ToString());
						return Array.Empty<byte>();
					}

					if(!redirectUri.IsAbsoluteUri && request.RequestUri != null)
					{
						redirectUri = new Uri(request.RequestUri, redirectUri);
					}

					string redirectUriString = redirectUri.ToString();

					if(visitedUris == null)
					{
						visitedUris = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
						if(request.RequestUri != null)
						{
							visitedUris.Add(request.RequestUri.ToString());
						}
					}

					if(visitedUris.Contains(redirectUriString))
					{
						logger.LogWarning("Redirect loop detected. URI {RedirectUri} was already visited. Stopping redirect chain.", redirectUriString);
						return Array.Empty<byte>();
					}

					visitedUris.Add(redirectUriString);

					HttpMethod methodToUse = method;
					if((int)response.StatusCode == 303)
					{
						methodToUse = HttpMethod.Get;
					}

					if(client.RedirectCallback != null)
					{
						HttpRedirectInfo redirectInfo = new HttpRedirectInfo(client.Name ?? Constants.DefaultClientName, request.RequestUri?.ToString() ?? string.Empty, redirectUriString, (int)response.StatusCode, redirectCount, methodToUse, cancellationToken);
						RedirectAction action;
						try
						{
							action = client.RedirectCallback(redirectInfo);
						}
						catch(OperationCanceledException)
						{
							logger.LogInformation("Redirect canceled by callback (OperationCanceledException thrown)");
							throw;
						}

						if(action == RedirectAction.Cancel)
						{
							logger.LogInformation("Redirect canceled by callback (RedirectAction.Cancel)");
							throw new OperationCanceledException("Redirect canceled by callback");
						}
						else if(action == RedirectAction.Stop)
						{
							logger.LogInformation("Redirect stopped by callback");
							return Array.Empty<byte>();
						}
						else if(action == RedirectAction.FollowWithGet)
						{
							methodToUse = HttpMethod.Get;
						}
					}

					LoggingDefinitions.FollowingRedirect(logger, redirectUriString, redirectCount + 1, client.MaxRedirections);

					// If resulting method is GET, reuse GetBinaryAsync
					if(methodToUse == HttpMethod.Get)
					{
						return await GetBinaryAsync(client, logger, redirectUriString, timeout, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						// For other methods recursively call SendBinaryRequestAsync without content
						return await SendBinaryRequestAsync(client, logger, methodToUse, redirectUriString, content: null, timeout, cancellationToken, redirectCount + 1, visitedUris).ConfigureAwait(false);
					}
				}

				if(!response.IsSuccessStatusCode)
				{
					logger.LogError("HTTP error: {StatusCode} ({StatusText})", (int)response.StatusCode, response.ReasonPhrase ?? Constants.UnknownStatusText);
					throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
				}

				// Process Set-Cookie headers
				CookieHelper.ProcessSetCookieHeaders(
					response,
					baseUri,
					client.Name ?? Constants.DefaultClientName,
					logger);

				byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(linkedCts.Token).ConfigureAwait(false);
				logger.LogInformation("Binary response received: {Length} bytes", responseBytes.Length);

				CookiePersistence.SaveCookies();

				return responseBytes;
			}
			catch(HttpRequestException hre)
			{
				if(hre.StatusCode.HasValue)
				{
					logger.LogError("HTTP error: {StatusCode} ({StatusText}) - {Message}", (int)hre.StatusCode.Value, hre.StatusCode.Value, hre.Message);
				}
				else
				{
					logger.LogError("Request error: {Message}", hre.Message);
				}
				throw;
			}
			catch(OperationCanceledException oce) when(linkedCts.IsCancellationRequested)
			{
				if(cancellationToken.IsCancellationRequested)
				{
					logger.LogError(oce, "Request canceled by caller");
				}
				else if(timeoutCts.IsCancellationRequested)
				{
					logger.LogError(oce, "Request timed out");
				}
				else
				{
					logger.LogError(oce, "Request canceled");
				}
				return System.Array.Empty<byte>();
			}
			catch(OperationCanceledException)
			{
				// Rethrow OperationCanceledException produced by callbacks or explicit cancellations so callers can observe it
				throw;
			}
			catch(Exception ex)
			{
				logger.LogError(ex, "Binary request failed");
				throw;
			}
		}

		#endregion

		/// <summary>
		/// Generic method to send textual HTTP requests and return response body as string.
		/// </summary>
		private static async Task<string> SendRequestAsync(IPooledHttpClient client, ILogger logger, HttpMethod method, string url, HttpContent? content = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default, int redirectCount = 0, System.Collections.Generic.HashSet<string>? visitedUris = null)
		{
			if(client == null)
			{
				throw new ArgumentNullException(nameof(client));
			}
			if(logger == null)
			{
				throw new ArgumentNullException(nameof(logger));
			}
			if(string.IsNullOrWhiteSpace(url))
			{
				throw new ArgumentException("url is required", nameof(url));
			}

			// Extract base address from URL for cookie persistence
			Uri? baseUri = CookieHelper.ExtractAndSetCookieBaseAddress(
				url,
				client.Name ?? Constants.DefaultClientName,
				logger);

			TimeSpan actualTimeout = timeout ?? Constants.DefaultRequestTimeout;

			// Combine parent cancellation token with timeout-based token
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(actualTimeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				HttpRequestMessage request = new HttpRequestMessage(method, url);
				if(content != null)
				{
					request.Content = content;
				}

				// Send the request
				using HttpResponseMessage response = await client.SendRawAsync(request, linkedCts.Token).ConfigureAwait(false);

				// Process redirect responses (3xx)
				if(IsRedirectStatusCode((int)response.StatusCode))
				{
					// Log the redirect response details
					logger.LogInformation("Redirect response received: {StatusCode} ({StatusText})", (int)response.StatusCode, response.ReasonPhrase);

					// Delegate to shared redirect handling so callbacks and limits are respected
					string redirectResult = await HandleRedirectAsync(client, logger, response, request.RequestUri, linkedCts.Token, redirectCount, cancellationToken, originalMethod: method, visitedUris: visitedUris).ConfigureAwait(false);
					if(redirectResult != null)
					{
						return redirectResult;
					}
					// If the shared handler returned null or empty, fall through to error handling
					logger.LogWarning("Redirect response does not contain a Location header or was not followed");
				}

				if(!response.IsSuccessStatusCode)
				{
					LoggingDefinitions.HttpErrorStatus(logger, (int)response.StatusCode, response.ReasonPhrase ?? Constants.UnknownStatusText);

					// Don't try to read error response content - it may cause HTTP/2 stream errors
					// Some servers (like Facebook) send RST_STREAM immediately after error responses
					// Attempting to read would cause: "The HTTP/2 server reset the stream. HTTP/2 error code 'STREAM_CLOSED' (0x5)"

					return string.Empty;
				}

				// Process Set-Cookie headers
				CookieHelper.ProcessSetCookieHeaders(
					response,
					baseUri,
					client.Name ?? Constants.DefaultClientName,
				 logger);

				string textResponse = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
				LoggingDefinitions.ResponseLength(logger, textResponse.Length);

				// Debug: Log response headers
				if(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace))
				{
					logger.LogTrace("=== Response Headers ===");
					foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in response.Headers)
					{
						logger.LogTrace("{HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
					}

					if(response.Content?.Headers != null)
					{
						foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in response.Content.Headers)
						{
							logger.LogTrace("{HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
						}
					}
				}

				// Debug: Pretty-print JSON responses
				if(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace) && !string.IsNullOrWhiteSpace(textResponse))
				{
					try
					{
						using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(textResponse);
						using System.IO.MemoryStream ms = new System.IO.MemoryStream();
						using(System.Text.Json.Utf8JsonWriter writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = true }))
						{
							doc.WriteTo(writer);
							writer.Flush();
						}
						string prettyJson = System.Text.Encoding.UTF8.GetString(ms.ToArray());
						logger.LogTrace("JSON Response (pretty-printed):");
						logger.LogTrace("{Json}", prettyJson);
					}
					catch
					{
						// Not JSON or invalid JSON, skip pretty-printing
					}
				}

				CookiePersistence.SaveCookies();
				LoggingDefinitions.CookiesSaved(logger);

				// Return response body to caller for CLI to write to stdout
				return textResponse;
			}
			catch(HttpRequestException hre)
			{
				if(hre.StatusCode.HasValue)
				{
					LoggingDefinitions.HttpErrorStatus(logger, (int)hre.StatusCode.Value, hre.StatusCode.Value.ToString());
				}
				else
				{
					LoggingDefinitions.RequestErrorMessage(logger, hre.Message);
				}
				return string.Empty;
			}
			catch(OperationCanceledException oce) when(linkedCts.IsCancellationRequested)
			{
				if(cancellationToken.IsCancellationRequested)
				{
					logger.LogError(oce, "Request canceled by caller");
				}
				else if(timeoutCts.IsCancellationRequested)
				{
					logger.LogError(oce, "Request timed out");
				}
				else
				{
					logger.LogError(oce, "Request canceled");
				}
				return string.Empty;
			}
			catch(OperationCanceledException)
			{
				// Rethrow OperationCanceledException produced by callbacks or explicit cancellations so callers can observe it
				throw;
			}
			catch(Exception ex)
			{
				logger.LogError(ex, "Request failed");
				return string.Empty;
			}
		}

		/// <summary>
		/// Result returned by GetAutoAsync indicating whether content was saved to file or returned as text.
		/// </summary>
		public sealed class GetResult
		{
			public bool SavedToFile { get; init; }
			public string? FilePath { get; init; }
			public string? Text { get; init; }
			public long? TotalBytes { get; init; }
		}

		/// <summary>
		/// Performs a GET request and either saves the response to a file under the provided output directory
		/// or returns the response body as text depending on the response content type.
		/// Returns null on non-success HTTP status codes.
		/// </summary>
		public static async Task<GetResult?> GetAutoAsync(IPooledHttpClient client, ILogger logger, string url, string outputDirectory, TimeSpan? timeout = null, CancellationToken cancellationToken = default, int redirectCount = 0, System.Collections.Generic.HashSet<string>? visitedUris = null)
		{
			if(client == null)
			{
				throw new ArgumentNullException(nameof(client));
			}
			if(logger == null)
			{
				throw new ArgumentNullException(nameof(logger));
			}
			if(string.IsNullOrWhiteSpace(url))
			{
				throw new ArgumentException("url is required", nameof(url));
			}
			if(string.IsNullOrWhiteSpace(outputDirectory))
			{
				throw new ArgumentException("outputDirectory is required", nameof(outputDirectory));
			}

			TimeSpan actualTimeout = timeout ?? Constants.DefaultBinaryOperationTimeout;
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(actualTimeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
				using HttpResponseMessage response = await client.SendRawAsync(request, linkedCts.Token).ConfigureAwait(false);

				// Handle redirects manually if present
				if(IsRedirectStatusCode((int)response.StatusCode))
				{
					logger.LogDebug("Redirect response detected for {Url}: {StatusCode}", url, (int)response.StatusCode);
					if(response.Headers.Location != null)
					{
						logger.LogDebug("Location header: {Location}", response.Headers.Location);
					}

					// Delegate to shared redirect handling so callbacks and limits are respected
					string redirectResult = await HandleRedirectAsync(client, logger, response, response.RequestMessage?.RequestUri, linkedCts.Token, redirectCount, cancellationToken, originalMethod: null, visitedUris: visitedUris).ConfigureAwait(false);
					if(redirectResult == null)
					{
						return null;
					}

					// The shared redirect handler returns a string (textual response). Wrap into GetResult for the caller.
					return new GetResult
					{
						SavedToFile = false,
						Text = redirectResult,
						TotalBytes = redirectResult != null ? (long?)System.Text.Encoding.UTF8.GetByteCount(redirectResult) : null
					};
				}

				// Check for success status
				if(!response.IsSuccessStatusCode)
				{
					logger.LogError("HTTP error: {StatusCode} ({StatusText})", (int)response.StatusCode, response.ReasonPhrase ?? Constants.UnknownStatusText);
					return null;
				}

				// Capture locals to help the nullable analyzer across awaits
				IPooledHttpClient localClient = client;
				ILogger localLogger = logger;

				HttpContent? content = response.Content;
				if(content == null)
				{
					localLogger.LogWarning("Response contained no content for GET {Url}", url);
					return null;
				}

				long? totalBytes = content.Headers.ContentLength;

				// Ensure output directory exists
				try
				{
					if(!Directory.Exists(outputDirectory))
					{
						Directory.CreateDirectory(outputDirectory);
					}
				}
				catch(Exception ex)
				{
					localLogger.LogWarning(ex, "Failed to create output directory {OutputDir}", outputDirectory);
				}

				string? mediaType = content.Headers.ContentType?.MediaType;
				bool isTextual = false;

				// Best-effort: if the response has a Content-Disposition indicating an attachment or a filename,
				// prefer to treat it as binary (download) regardless of media type.
				bool hasAttachmentDisposition = false;
				try
				{
					System.Net.Http.Headers.ContentDispositionHeaderValue? cd = content.Headers.ContentDisposition;
					if(cd != null)
					{
						if(!string.IsNullOrWhiteSpace(cd.FileName) || string.Equals(cd.DispositionType, "attachment", StringComparison.OrdinalIgnoreCase))
						{
							hasAttachmentDisposition = true;
						}
					}
				}
				catch
				{
					// best-effort
				}

				if(hasAttachmentDisposition)
				{
					isTextual = false;
				}
				else if(!string.IsNullOrWhiteSpace(mediaType))
				{
					string mt = mediaType.ToLowerInvariant();

					// Known binary media types that should be downloaded
					if(mt == "application/octet-stream"
						|| mt == "application/zip"
						|| mt == "application/x-gzip"
						|| mt == "application/gzip"
						|| mt == "application/x-7z-compressed"
						|| mt == "application/x-tar"
						|| mt == "application/pdf")
					{
						isTextual = false;
					}
					// Image/audio/video are binary
					else if(mt.StartsWith("image/") || mt.StartsWith("audio/") || mt.StartsWith("video/"))
					{
						isTextual = false;
					}
					// Well-known textual types
					else if(mt.StartsWith("text/")
						|| mt == "application/json"
						|| mt == "application/xml"
						|| mt == "application/javascript"
						|| mt == "application/ecmascript"
						|| mt.EndsWith("+json")
						|| mt.EndsWith("+xml")
						|| mt.EndsWith("+text"))
					{
						isTextual = true;
					}
					else
					{
						// Unknown application/* subtype - by default prefer to treat as textual so the CLI writes to stdout
						// This avoids surprising the user by saving to disk when the response is human-readable but the server
						// didn't set a proper media type. If callers want stricter behavior they can rely on Content-Disposition
						// or explicit media types.
						isTextual = true;
					}
				}
				else
				{
					// No media type provided - default to textual to favor stdout output. This may be overridden in the
					// future by inspecting URL/file extension if desired.
					isTextual = true;
				}

				if(isTextual)
				{
					string text = await content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
					return new GetResult
					{
						SavedToFile = false,
						Text = text,
						TotalBytes = totalBytes
					};
				}
				else
				{
					// Determine filename from response request URI or fallback
					string? fileName = null;
					try
					{
						Uri? reqUri = response.RequestMessage?.RequestUri;
						string? lastSegment = null;
						if(reqUri?.Segments != null && reqUri.Segments.Length > 0)
						{
							lastSegment = reqUri.Segments[ reqUri.Segments.Length - 1 ];
						}

						if(string.IsNullOrWhiteSpace(lastSegment))
						{
							fileName = "downloaded.bin";
						}
						else
						{
							fileName = lastSegment.Trim('/');
							if(string.IsNullOrWhiteSpace(fileName))
								fileName = "downloaded.bin";
						}
					}
					catch
					{
						fileName = "downloaded.bin";
					}

					string fileNameNonNull = fileName ?? "downloaded.bin";

					string outputPath = Path.Combine(outputDirectory, fileNameNonNull);
					int suffix = 1;
					while(File.Exists(outputPath))
					{
						string nameOnly = Path.GetFileNameWithoutExtension(fileNameNonNull);
						string ext = Path.GetExtension(fileNameNonNull);
						outputPath = Path.Combine(outputDirectory, $"{nameOnly}({suffix}){ext}");
						suffix++;
					}

					System.IO.Stream? contentStream = await content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);
					if(contentStream == null)
					{
						localLogger.LogWarning("Failed to obtain response stream for GET {Url}", url);
						return null;
					}

					using FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: Constants.StreamBufferSize, useAsync: true);

					byte[] buffer = new byte[ Constants.StreamBufferSize ];
					int bytesRead;
					long written = 0;
					int lastReportedPercentage = -1;
					System.DateTimeOffset lastProgressUpdate = System.DateTimeOffset.MinValue;
					// Determine granularity for progress events (use client-config if available)
					long granularity = Constants.DefaultProgressGranularityBytes; // kept for potential future use
					try
					{
						if(localClient is not null && HttpLibrary.ServiceConfiguration.AppConfig != null && HttpLibrary.ServiceConfiguration.AppConfig.ProgressDisplay != null)
						{
							granularity = Math.Max(1, HttpLibrary.ServiceConfiguration.AppConfig.ProgressDisplay.ProgressEventGranularityBytes);
						}
					}
					catch
					{
						// Best-effort; keep default granularity
					}

					while(( bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token).ConfigureAwait(false) ) > 0)
					{
						await fs.WriteAsync(buffer.AsMemory(0, bytesRead), linkedCts.Token).ConfigureAwait(false);
						written += bytesRead;

						// In GetAutoAsync binary branch: replace client usages with localClient/localLogger and null-conditional for ProgressCallback
						// Find the streaming loop and replace the progressCallback block

						// inside DownloadToFileAsync: use null-conditional for progress callback
						System.Action<HttpProgressInfo>? progressCallback = localClient?.ProgressCallback;
						if(progressCallback != null)
						{
							System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
							int currentPct = -1;
							if(totalBytes.HasValue && totalBytes.Value > 0)
							{
								currentPct = (int)( (double)written / totalBytes.Value * 100.0 );
							}

							bool shouldReport = false;
							if(( now - lastProgressUpdate ).TotalMilliseconds >= 1500)
							{
								shouldReport = true;
							}
							else if(currentPct >= 0 && currentPct != lastReportedPercentage)
							{
								shouldReport = true;
							}

							if(shouldReport)
							{
								HttpProgressInfo info = new HttpProgressInfo(localClient?.Name ?? Constants.DefaultClientName, url)
								{
									Stage = HttpProgressStage.DownloadingContent,
									BytesTransferred = written,
									TotalBytes = totalBytes,
									Message = null
								};
								progressCallback(info);
								lastReportedPercentage = currentPct;
								lastProgressUpdate = now;
							}
						}
					}
					await fs.FlushAsync(linkedCts.Token).ConfigureAwait(false);

					// Final progress - completed
					System.Action<HttpProgressInfo>? finalCallback2 = localClient?.ProgressCallback;
					if(finalCallback2 != null)
					{
						HttpProgressInfo doneInfo = new HttpProgressInfo(localClient?.Name ?? Constants.DefaultClientName, url)
						{
							Stage = HttpProgressStage.Completed,
							BytesTransferred = written,
							TotalBytes = written,
							Message = "Completed"
						};
						localLogger.LogDebug("GetAutoAsync emitting final progress: {BytesTransferred} of {TotalBytes}", written, written);
						finalCallback2(doneInfo);
					}

					return new GetResult
					{
						SavedToFile = true,
						FilePath = outputPath,
						TotalBytes = written
					};
				}
			}
			catch(HttpRequestException hre)
			{
				if(hre.StatusCode.HasValue)
				{
					logger.LogError("HTTP error: {StatusCode} ({StatusText}) - {Message}", (int)hre.StatusCode.Value, hre.StatusCode.Value, hre.Message);
				}
				else
				{
					logger.LogError("Request error: {Message}", hre.Message);
				}
				return null;
			}
			catch(OperationCanceledException oce) when(linkedCts.IsCancellationRequested)
			{
				if(cancellationToken.IsCancellationRequested)
				{
					logger.LogError(oce, "Request canceled by caller");
				}
				else if(timeoutCts.IsCancellationRequested)
				{
					logger.LogError(oce, "Request timed out");
				}
				else
				{
					logger.LogError(oce, "Request canceled");
				}
				return null;
			}
			catch(Exception ex)
			{
				logger.LogError(ex, "GetAutoAsync failed");
				return null;
			}
		}

		/// <summary>
		/// Sends an OPTIONS preflight request to the specified URI and parses Access-Control-Allow-* headers.
		/// </summary>
		public static async Task<HttpLibrary.Models.PreflightResult> SendPreflightAsync(IPooledHttpClient client, ILogger logger, string requestUri, string origin, string accessControlRequestMethod, string? accessControlRequestHeaders = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			if(client == null)
			{
				throw new ArgumentNullException(nameof(client));
			}
			if(logger == null)
			{
				throw new ArgumentNullException(nameof(logger));
			}
			if(string.IsNullOrWhiteSpace(requestUri))
			{
				throw new ArgumentException("requestUri is required", nameof(requestUri));
			}

			TimeSpan actualTimeout = timeout ?? Constants.DefaultRequestTimeout;
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(actualTimeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			try
			{
				HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Options, requestUri);
				if(!string.IsNullOrEmpty(origin))
				{
					req.Headers.Add("Origin", origin);
				}
				if(!string.IsNullOrEmpty(accessControlRequestMethod))
				{
					req.Headers.Add("Access-Control-Request-Method", accessControlRequestMethod);
				}
				if(!string.IsNullOrEmpty(accessControlRequestHeaders))
				{
					req.Headers.Add("Access-Control-Request-Headers", accessControlRequestHeaders);
				}

				using HttpResponseMessage resp = await client.SendRawAsync(req, linkedCts.Token).ConfigureAwait(false);

				// Build result from Access-Control-Allow-* headers (best-effort)
				HttpLibrary.Models.PreflightResult result = new HttpLibrary.Models.PreflightResult();

				if(resp.Headers.TryGetValues("Access-Control-Allow-Origin", out var originVals))
				{
					result.AllowOrigin = string.Join(", ", originVals);
				}

				if(resp.Headers.TryGetValues("Access-Control-Allow-Methods", out var methodVals))
				{
					result.AllowMethods = string.Join(", ", methodVals);
				}

				if(resp.Headers.TryGetValues("Access-Control-Allow-Headers", out var headerVals))
				{
					result.AllowHeaders = string.Join(", ", headerVals);
				}

				if(resp.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credVals))
				{
					string first = credVals.FirstOrDefault() ?? string.Empty;
					if(string.Equals(first, "true", StringComparison.OrdinalIgnoreCase))
					{
						result.AllowCredentials = true;
					}
					else if(string.Equals(first, "false", StringComparison.OrdinalIgnoreCase))
					{
						result.AllowCredentials = false;
					}
				}

				return result;
			}
			catch(HttpRequestException hre)
			{
				logger.LogWarning(hre, "Preflight request failed");
				return new HttpLibrary.Models.PreflightResult();
			}
			catch(OperationCanceledException) when(linkedCts.IsCancellationRequested)
			{
				logger.LogWarning("Preflight request canceled or timed out");
				return new HttpLibrary.Models.PreflightResult();
			}
			catch(Exception ex)
			{
				logger.LogWarning(ex, "Preflight request failed");
				return new HttpLibrary.Models.PreflightResult();
			}
		}
	}
}