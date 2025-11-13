using HttpLibrary.Handlers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	/// <summary>
	/// Typed HTTP client registered via IHttpClientFactory. Metrics are updated for every request.
	/// </summary>
	public sealed class PooledHttpClient : IPooledHttpClient
	{
		readonly HttpClient client;
		readonly PooledHttpClientMetrics metrics;
		public string? Name { get; }
		readonly ILogger<PooledHttpClient> logger;
		readonly int maxRedirections;
		readonly HttpRequestHeaders customHeaders;

		public Action<HttpProgressInfo>? ProgressCallback { get; set; }
		public Func<HttpRedirectInfo, RedirectAction>? RedirectCallback { get; set; }
		public int MaxRedirections => maxRedirections;

		internal static IEnumerable<ProductInfoHeaderValue> ParseUserAgentTokens(string raw)
		{
			return HttpRequestHeaders.ParseUserAgentTokens(raw);
		}

		private static void ApplyDefaultUserAgent(HttpClient httpClient, string raw)
		{
			if(httpClient == null)
			{
				return;
			}
			httpClient.DefaultRequestHeaders.Remove(Constants.HeaderUserAgent);
			httpClient.DefaultRequestHeaders.UserAgent.Clear();
			System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
			foreach(ProductInfoHeaderValue token in ParseUserAgentTokens(raw))
			{
				parts.Add(token.ToString());
			}
			string ua = string.Join(" ", parts);
			httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Constants.HeaderUserAgent, ua);
		}

		public PooledHttpClient(HttpClient client, IOptions<PooledHttpClientOptions> options, ILogger<PooledHttpClient> logger)
		{
			this.client = client ?? throw new ArgumentNullException(nameof(client));
			metrics = new PooledHttpClientMetrics();
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			customHeaders = HttpRequestHeaders.CreateCustomHeadersContainer();

			PooledHttpClientOptions opt = options?.Value ?? new PooledHttpClientOptions();

			// read configured name (may be null)
			this.Name = opt.Name;
			this.maxRedirections = opt.MaxRedirections;

			if(opt.BaseAddress is not null)
			{
				this.client.BaseAddress = opt.BaseAddress;
			}
			this.client.DefaultRequestVersion = opt.DefaultRequestVersion;
			if(opt.Timeout.HasValue)
			{
				this.client.Timeout = opt.Timeout.Value;
			}

			// apply default headers with RFC7230 validation
			foreach(KeyValuePair<string, string> kv in opt.DefaultRequestHeaders)
			{
				if(!this.client.DefaultRequestHeaders.Contains(kv.Key))
				{
					// RFC7230 Section3.2: Validate header name and value
					if(!HttpHeaderValidator.IsValidHeaderName(kv.Key))
					{
						logger.LogWarning("Invalid default header name '{HeaderName}' - skipping (RFC7230 violation)", kv.Key);
						continue;
					}
					if(!HttpHeaderValidator.IsValidHeaderValue(kv.Value))
					{
						logger.LogWarning("Invalid default header value for '{HeaderName}' - skipping (RFC7230 violation)", kv.Key);
						continue;
					}

					// RFC7231 Section5.5.3: Special handling for User-Agent
					if(string.Equals(kv.Key, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
					{
						if(!HttpHeaderValidator.IsValidUserAgent(kv.Value))
						{
							logger.LogWarning("Invalid default User-Agent header - skipping (RFC7231 violation)");
							continue;
						}
						ApplyDefaultUserAgent(this.client, kv.Value);
					}
					else
					{
						this.client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
					}
				}
			}

			maxRedirections = opt.MaxRedirections;
		}

		public PooledHttpClientMetrics Metrics => metrics;

		/// <summary>
		/// Adds a custom header that will be included in all requests made by this client instance.
		/// If the header already exists, its value will be updated.
		/// </summary>
		/// <param name="name">Header name (validated per RFC 7230)</param>
		/// <param name="value">Header value (validated per RFC 7230)</param>
		public void AddRequestHeader(string name, string value)
		{
			customHeaders.AddWithValidation(name, value);
			logger.LogDebug("Added custom request header: {HeaderName}", name);
		}

		/// <summary>
		/// Removes a custom header that was previously added via AddRequestHeader.
		/// </summary>
		/// <param name="name">Header name to remove</param>
		/// <returns>True if the header was found and removed, false otherwise</returns>
		public bool RemoveRequestHeader(string name)
		{
			bool removed = customHeaders.Remove(name);
			if(removed)
			{
				logger.LogDebug("Removed custom request header: {HeaderName}", name);
			}

			return removed;
		}

		/// <summary>
		/// Clears all custom headers that were added via AddRequestHeader.
		/// </summary>
		public void ClearRequestHeaders()
		{
			int count = customHeaders.Count;
			customHeaders.Clear();
			logger.LogDebug("Cleared {Count} custom request headers", count);
		}

		async Task<HttpResponseMessage> ExecuteAsync(Func<Task<HttpResponseMessage>> action, CancellationToken cancellationToken)
		{
			metrics.OnRequestStarted();
			Stopwatch sw = Stopwatch.StartNew();
			try
			{
				HttpResponseMessage response = await action().ConfigureAwait(false);

				// Best-effort: capture Set-Cookie headers and forward to CookiePersistence so cookies are persisted
				try
				{
					string clientName = this.Name ?? Constants.DefaultClientName;
					if(response.Headers != null && response.RequestMessage?.RequestUri != null)
					{
						if(response.Headers.TryGetValues(Constants.HeaderSetCookie, out var setCookieValues))
						{
							foreach(string sc in setCookieValues)
							{
								try
								{
									// Diagnostic: log the client name and raw Set-Cookie header being forwarded
									try
									{ LoggerBridge.LogInformation("PooledHttpClient forwarding Set-Cookie for client '{Client}': {SetCookieRaw}", clientName, sc); }
									catch { }

									CookiePersistence.AddCookieFromHeader(clientName, sc, response.RequestMessage.RequestUri);
								}
								catch { /* best-effort */ }
							}
						}
					}
				}
				catch { /* best-effort */ }

				long bytes = 0L;
				if(response.Content != null)
				{
					try
					{
						// attempt to read content length cheaply if available
						if(response.Content.Headers.ContentLength.HasValue)
						{
							bytes = response.Content.Headers.ContentLength.Value;
						}
						else if(response.IsSuccessStatusCode)
						{
							// Only buffer content for successful responses
							// For error responses, EnsureSuccessStatusCode will throw before buffering
							byte[]? b = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
							bytes = b?.LongLength ?? 0;
							if(b != null && bytes > 0)
							{
								// we consumed content; recreate a new HttpContent to return a response with content still accessible
								ByteArrayContent newContent = new ByteArrayContent(b);
								// copy headers from original content to the new content
								foreach(KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
								{
									newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
								}
								response.Content = newContent;
							}
						}
						// else: Error response - EnsureSuccessStatusCode will throw, don't buffer
					}
					catch
					{
						// ignore content-length read errors
					}
				}

				sw.Stop();
				response.EnsureSuccessStatusCode();
				metrics.OnRequestCompleted(true, bytes, sw.ElapsedMilliseconds);
				return response;
			}
			catch(Exception)
			{
				sw.Stop();
				metrics.OnRequestCompleted(false, 0, sw.ElapsedMilliseconds);
				throw;
			}
		}

		async Task<HttpResponseMessage> PrepareAndSendAsync(HttpMethod method, string requestUri, HttpContent? content, HttpRequestHeaders? headers, CancellationToken cancellationToken)
		{
			using HttpRequestMessage request = new HttpRequestMessage(method, requestUri) { Content = content };

			// RFC7230 Section5.4: Validate Host header is present
			// HttpClient automatically adds Host header based on request URI, so this is a sanity check
			if(request.RequestUri != null && !string.IsNullOrWhiteSpace(request.RequestUri.Host))
			{
				// Host header will be automatically added by HttpClient
				logger.LogDebug("Request to host: {Host}", request.RequestUri.Host);
			}

			// Apply headers: custom headers first, then per-request headers (which can override)
			if(headers != null)
			{
				headers.ApplyTo(request, customHeaders, (name, reason) =>
				{
					logger.LogWarning("Invalid header '{HeaderName}' - skipping: {Reason}", name, reason);
				});
			}
			else
			{
				// Only apply custom headers
				customHeaders.ApplyTo(request, null, (name, reason) =>
				{
					logger.LogWarning("Invalid header '{HeaderName}' - skipping: {Reason}", name, reason);
				});
			}

			// Log all request headers at Information level
			if(logger.IsEnabled(LogLevel.Information))
			{
				System.Text.StringBuilder headersLog = new System.Text.StringBuilder();
				headersLog.AppendLine($"Request headers for {method} {requestUri}:");

				// Log default headers from HttpClient
				foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in this.client.DefaultRequestHeaders)
				{
					if(string.Equals(header.Key, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
					{
						string ua = string.Join(" ", this.client.DefaultRequestHeaders.UserAgent.Select(t => t.ToString()));
						headersLog.AppendLine($" {header.Key}: {ua}");
					}
					else
					{
						headersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
					}
				}

				// Log request-specific headers
				foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in request.Headers)
				{
					if(string.Equals(header.Key, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
					{
						string ua = string.Join(" ", request.Headers.UserAgent.Select(t => t.ToString()));
						headersLog.AppendLine($" {header.Key}: {ua}");
					}
					else
					{
						headersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
					}
				}

				// Log content headers if present
				if(request.Content is not null)
				{
					foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in request.Content.Headers)
					{
						headersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
					}
				}

				logger.LogInformation(headersLog.ToString());
			}

			// If a persisted CookieContainer exists for this client, log the Cookie header that will be sent for the request URI.
			try
			{
				string clientName = this.Name ?? string.Empty;
				CookieContainer? container = CookiePersistence.GetContainer(clientName);
				if(container != null)
				{
					if(Uri.TryCreate(requestUri, UriKind.Absolute, out Uri? reqUri))
					{
						string cookieHeader = container.GetCookieHeader(reqUri);
						if(!string.IsNullOrWhiteSpace(cookieHeader))
						{
							logger.LogInformation("Cookie header to be sent for client '{Client}' to '{Uri}': {CookieHeader}", clientName, reqUri, cookieHeader);
						}
						else
						{
							logger.LogWarning("No cookies available for client '{Client}' to send to '{Uri}' (container has cookies but none match this URI)", clientName, reqUri);
						}
					}
				}
				else
				{
					logger.LogDebug("No cookie container registered for client '{Client}'", clientName);
				}
			}
			catch(Exception ex)
			{
				// Do not fail request due to logging; log at debug level if possible
				try
				{
					logger.LogDebug(ex, "Failed to enumerate cookies for client before send");
				}
				catch
				{
					// ignore
				}
			}

			// Note: ExecuteAsync will EnsureSuccessStatusCode before returning to caller convenience methods.
			return await ExecuteAsync(() => this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken), cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			if(request is null)
			{
				throw new ArgumentNullException(nameof(request));
			}
			return ExecuteAsync(() => this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken), cancellationToken);
		}

		public async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			if(request is null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			// If the request uses the alias:// scheme, resolve it immediately so no other handlers observe the unsupported scheme
			try
			{
				if(request.RequestUri != null && string.Equals(request.RequestUri.Scheme, "alias", StringComparison.OrdinalIgnoreCase))
				{
					if(AliasResolutionHandler.TryResolveAlias(request.RequestUri, out Uri? resolved, out string? aliasName, out string? error))
					{
						request.RequestUri = resolved!;
						// Ensure Host header matches resolved host
						try
						{
							string hostHeader = request.RequestUri.IsDefaultPort ? request.RequestUri.Host : $"{request.RequestUri.Host}:{request.RequestUri.Port}";
							request.Headers.Host = hostHeader;
						}
						catch { /* best-effort */ }
					}
					else
					{
						// Mirror handler behavior: surface as network-like error
						throw new HttpRequestException(error ?? "Failed to resolve alias URI");
					}
				}
			}
			catch(Exception ex) when(!( ex is HttpRequestException ))
			{
				// Wrap unexpected exceptions
				throw new HttpRequestException("Failed to resolve alias URI", ex);
			}

			// Track metrics for SendRawAsync as well
			metrics.OnRequestStarted();
			Stopwatch sw = Stopwatch.StartNew();

			try
			{
				// Apply custom runtime headers (pre-validated) to manually-created requests.
				// Do NOT copy HttpClient.DefaultRequestHeaders into the request because HttpClient will apply them when sending
				try
				{
					customHeaders.ApplyTo(request, null, (n, reason) => { /* best-effort */ });
				}
				catch
				{
					// best-effort: do not fail request if header application fails
				}

				// Use ResponseHeadersRead to avoid automatic buffering - we'll buffer manually if needed
				// Log request headers for manually-built requests at Information level
				if(logger.IsEnabled(LogLevel.Information))
				{
					try
					{
						System.Text.StringBuilder headersLog = new System.Text.StringBuilder();
						headersLog.AppendLine($"Request headers for {request.Method} {request.RequestUri}:");
						// Log default headers from HttpClient
						foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in this.client.DefaultRequestHeaders)
						{
							if(string.Equals(header.Key, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
							{
								string ua = string.Join(" ", this.client.DefaultRequestHeaders.UserAgent.Select(t => t.ToString()));
								headersLog.AppendLine($" {header.Key}: {ua}");
							}
							else
							{
								headersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
							}
						}
						// Log request-specific headers
						foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in request.Headers)
						{
							if(string.Equals(header.Key, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
							{
								string ua = string.Join(" ", request.Headers.UserAgent.Select(t => t.ToString()));
								headersLog.AppendLine($" {header.Key}: {ua}");
							}
							else
							{
								headersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
							}
						}
						// Log content headers if present
						if(request.Content is not null)
						{
							foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in request.Content.Headers)
							{
								headersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
							}
						}

						logger.LogInformation(headersLog.ToString());
					}
					catch
					{
						// best-effort
					}
				}

				// Ensure Host header present for absolute URIs. Some test primary handlers are used as the primary message handler
				// and do not automatically set the Host header. Setting here ensures downstream handlers and servers receive it.
				try
				{
					if(request.RequestUri != null && string.IsNullOrWhiteSpace(request.Headers.Host))
					{
						string hostHeader = request.RequestUri.IsDefaultPort ? request.RequestUri.Host : $"{request.RequestUri.Host}:{request.RequestUri.Port}";
						request.Headers.Host = hostHeader;
					}
				}
				catch
				{
					// best-effort
				}

				// If a runtime CookieContainer exists for this client and the request doesn't already have a Cookie header,
				// copy cookies into the request so custom primary handlers (in tests) receive them.
				try
				{
					string clientNameForCookies = this.Name ?? string.Empty;
					System.Net.CookieContainer? runtimeContainer = CookiePersistence.GetContainer(clientNameForCookies);
					if(runtimeContainer != null && request.RequestUri != null)
					{
						string cookieHeader = runtimeContainer.GetCookieHeader(request.RequestUri);
						if(!string.IsNullOrWhiteSpace(cookieHeader) && !request.Headers.Contains("Cookie"))
						{
							request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
						}
					}
				}
				catch
				{
					// best-effort
				}

				HttpResponseMessage response = await this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

				long bytes = 0L;

				// If a progress callback is registered, avoid buffering the entire response here
				// so callers that stream the response can report progress as bytes arrive.
				if(this.ProgressCallback != null)
				{
					sw.Stop();

					bool successEarly = response.IsSuccessStatusCode;
					metrics.OnRequestCompleted(successEarly, bytes, sw.ElapsedMilliseconds);

					// Diagnostic logging: show actual response request URI and status to detect automatic redirects
					try
					{
						Uri? respUriEarly = response.RequestMessage?.RequestUri;
						logger.LogInformation("PooledHttpClient.SendRawAsync: request {RequestUri} -> response.RequestMessage.RequestUri={RespUri}, Status={StatusCode}", request.RequestUri?.ToString(), respUriEarly?.ToString() ?? "(null)", (int)response.StatusCode);
						if(respUriEarly != null && !string.Equals(respUriEarly.ToString(), request.RequestUri?.ToString(), StringComparison.OrdinalIgnoreCase))
						{
							logger.LogInformation("PooledHttpClient: handler followed redirect from {Original} to {Actual}", request.RequestUri?.ToString(), respUriEarly.ToString());
						}
					}
					catch
					{
						// best-effort
					}

					// Log the final set of headers that were actually sent on the underlying request (best-effort).
					try
					{
						HttpRequestMessage? sent = response.RequestMessage;
						if(sent != null)
						{
							System.Text.StringBuilder sentHeadersLog = new System.Text.StringBuilder();
							sentHeadersLog.AppendLine($"Final request headers sent for {sent.Method} {sent.RequestUri}:");

							foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in sent.Headers)
							{
								if(string.Equals(header.Key, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
								{
									string ua = string.Join(" ", sent.Headers.UserAgent.Select(t => t.ToString()));
									sentHeadersLog.AppendLine($" {header.Key}: {ua}");
								}
								else
								{
									sentHeadersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
								}
							}

							if(sent.Content != null)
							{
								foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in sent.Content.Headers)
								{
									sentHeadersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
								}
							}

							logger.LogInformation(sentHeadersLog.ToString());
						}
					}
					catch
					{
						// best-effort
					}

					return response;
				}

				if(response.Content != null)
				{
					try
					{
						// attempt to read content length cheaply if available
						if(response.Content.Headers.ContentLength.HasValue)
						{
							bytes = response.Content.Headers.ContentLength.Value;
						}

						// Only buffer content for successful responses
						// For error responses, let the caller handle content reading
						if(response.IsSuccessStatusCode)
						{
							byte[]? b = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
							bytes = b?.LongLength ?? 0;
							if(b != null && bytes > 0)
							{
								// we consumed content; recreate a new HttpContent to return a response with content still accessible
								ByteArrayContent newContent = new ByteArrayContent(b);
								// copy headers from original content to the new content
								foreach(KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
								{
									newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
								}
								response.Content = newContent;
							}
						}
						// else: Error response - don't buffer content, let caller handle it
					}
					catch
					{
						// ignore content-length read errors
					}
				}


				// Continue with the rest of the method (stop timer, metrics, logging)
				sw.Stop();

				// Consider any response a success for metrics purposes (caller will handle status codes)
				bool success = response.IsSuccessStatusCode;
				metrics.OnRequestCompleted(success, bytes, sw.ElapsedMilliseconds);

				// Diagnostic logging: show actual response request URI and status to detect automatic redirects
				try
				{
					Uri? respUri = response.RequestMessage?.RequestUri;
					logger.LogInformation("PooledHttpClient.SendRawAsync: request {RequestUri} -> response.RequestMessage.RequestUri={RespUri}, Status={StatusCode}", request.RequestUri?.ToString(), respUri?.ToString() ?? "(null)", (int)response.StatusCode);
					if(respUri != null && !string.Equals(respUri.ToString(), request.RequestUri?.ToString(), StringComparison.OrdinalIgnoreCase))
					{
						logger.LogInformation("PooledHttpClient: handler followed redirect from {Original} to {Actual}", request.RequestUri?.ToString(), respUri.ToString());
					}
				}
				catch
				{
					// best-effort
				}

				// Log the final set of headers that were actually sent on the underlying request (best-effort).
				try
				{
					HttpRequestMessage? sent = response.RequestMessage;
					if(sent != null)
					{
						System.Text.StringBuilder sentHeadersLog = new System.Text.StringBuilder();
						sentHeadersLog.AppendLine($"Final request headers sent for {sent.Method} {sent.RequestUri}:");

						foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in sent.Headers)
						{
							if(string.Equals(header.Key, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
							{
								string ua = string.Join(" ", sent.Headers.UserAgent.Select(t => t.ToString()));
								sentHeadersLog.AppendLine($" {header.Key}: {ua}");
							}
							else
							{
								sentHeadersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
							}
						}

						if(sent.Content != null)
						{
							foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in sent.Content.Headers)
							{
								sentHeadersLog.AppendLine($" {header.Key}: {string.Join(", ", header.Value)}");
							}
						}

						logger.LogInformation(sentHeadersLog.ToString());
					}
				}
				catch
				{
					// best-effort
				}

				return response;
			}
			catch(Exception)
			{
				sw.Stop();
				metrics.OnRequestCompleted(false, 0, sw.ElapsedMilliseconds);
				throw;
			}
		}

		public async Task<string> GetStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Get, requestUri, null, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		public async Task<byte[]> GetBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Get, requestUri, null, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> GetAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Get, requestUri, null, headers, cancellationToken);

		public async Task<string> PostStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Post, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Post, requestUri, content, headers, cancellationToken);

		public async Task<byte[]> PostBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Post, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		}

		public async Task<string> PutStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Put, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Put, requestUri, content, headers, cancellationToken);

		public async Task<byte[]> PutBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Put, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		}

		public async Task<string> DeleteStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Delete, requestUri, null, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> DeleteAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Delete, requestUri, null, headers, cancellationToken);

		public async Task<byte[]> DeleteBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Delete, requestUri, null, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		}

		public async Task<string> PatchStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Patch, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> PatchAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Patch, requestUri, content, headers, cancellationToken);

		public async Task<byte[]> PatchBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Patch, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> HeadAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Head, requestUri, null, headers, cancellationToken);

		public async Task<string> OptionsStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Options, requestUri, null, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> OptionsAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Options, requestUri, null, headers, cancellationToken);

		public async Task<byte[]> OptionsBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Options, requestUri, null, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		}

		public async Task<string> TraceStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Trace, requestUri, null, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> TraceAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Trace, requestUri, null, headers, cancellationToken);

		public async Task<byte[]> TraceBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			using HttpResponseMessage resp = await PrepareAndSendAsync(HttpMethod.Trace, requestUri, null, headers, cancellationToken).ConfigureAwait(false);
			return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task<HttpResponseMessage> ConnectAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
			=> PrepareAndSendAsync(HttpMethod.Connect, requestUri, null, headers, cancellationToken);

		public Task<string> PostStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			HttpContent c = new StringContent(content ?? string.Empty, System.Text.Encoding.UTF8, mediaType);
			return PostStringAsync(requestUri, c, headers, cancellationToken);
		}

		public Task<string> PutStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			HttpContent c = new StringContent(content ?? string.Empty, System.Text.Encoding.UTF8, mediaType);
			return PutStringAsync(requestUri, c, headers, cancellationToken);
		}

		public Task<string> PatchStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default)
		{
			HttpContent c = new StringContent(content ?? string.Empty, System.Text.Encoding.UTF8, mediaType);
			return PatchStringAsync(requestUri, c, headers, cancellationToken);
		}
	}
}