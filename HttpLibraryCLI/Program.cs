using HttpLibrary;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Events;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HttpLibraryCLI
{
	internal class Program
	{
		// Serilog singleton instance - initialized before any other code runs
		private static readonly Serilog.ILogger SerilogInstance;
		private static readonly ILogger<Program> Logger;

		// Static constructor - runs before any other code in the class
		static Program()
		{
			// Initialize Serilog singleton - configure logging to stderr and file
			// Console logs go to stderr, leaving stdout clean for program output
			string logPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, Constants.LogOutputPath);

			SerilogInstance = new LoggerConfiguration()
				.MinimumLevel.Verbose()
				// Suppress framework/System.Net.Http diagnostic categories that redact headers
				.MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
				.MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
				.MinimumLevel.Override("System.Net.Http.HttpClient.edb", LogEventLevel.Warning)
				.MinimumLevel.Override("System.Net.Http.HttpClient.LogicalHandler", LogEventLevel.Warning)
				.WriteTo.Sink(new Sinks.StderrConsoleSink()) // Custom sink writes to stderr
				.WriteTo.File(
					logPath,
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 7,
					shared: true)
				.CreateLogger();

			Log.Logger = SerilogInstance;

			// Create Microsoft.Extensions.Logging logger from Serilog
			ServiceCollection services = new ServiceCollection();
			services.AddLogging(builder =>
			{
				builder.ClearProviders();
				builder.AddSerilog(SerilogInstance, dispose: false);
				// Ensure Microsoft logging minimum level allows Trace messages to flow through (shifted down)
				builder.SetMinimumLevel(LogLevel.Trace);

				// Suppress framework/System.* and Microsoft.* diagnostic logs that redact header values
				// We prefer the library to control header logging explicitly and show full headers
				builder.AddFilter("Microsoft", LogLevel.Warning);
				builder.AddFilter("System.Net", LogLevel.Warning);
				// Suppress broad System.Net.Http categories (framework emits masked header values)
				builder.AddFilter("System.Net.Http", LogLevel.Warning);
				// More specific filters for HttpClient diagnostic categories (these are the ones that output masked headers)
				builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
				builder.AddFilter("System.Net.Http.HttpClient.edb", LogLevel.Warning);
				builder.AddFilter("System.Net.Http.SocketsHttpHandler", LogLevel.Warning);
				builder.AddFilter("System.Net.Http.HttpClientEventSource", LogLevel.Warning);
				builder.AddFilter("HttpHandlerDiagnosticListener", LogLevel.Warning);
			});

			ServiceProvider provider = services.BuildServiceProvider();
			Logger = provider.GetRequiredService<ILogger<Program>>();

			// Initialize library logger bridge so cached logger is available quickly
			LoggerBridge.SetFactory(provider.GetRequiredService<ILoggerFactory>());

			// Register DiagnosticListener subscriber to capture full HTTP handler headers (debug level)
			try
			{
				// Diagnostic registration is disabled by default to avoid automatic trace context ("traceparent") injection.
				// Enable by setting environment variable HTTP_LIBRARY_ENABLE_HTTP_HANDLER_DIAGNOSTICS to "1" or "true".
				string? enableDiag = Environment.GetEnvironmentVariable("HTTP_LIBRARY_ENABLE_HTTP_HANDLER_DIAGNOSTICS");
				if(!string.IsNullOrEmpty(enableDiag) && (enableDiag == "1" || enableDiag.Equals("true", StringComparison.OrdinalIgnoreCase)))
				{
					ILoggerFactory lf = provider.GetRequiredService<ILoggerFactory>();
					IDisposable? diagSubscription = HttpLibrary.Diagnostics.HttpHandlerDiagnosticSubscriber.Register(lf);
					// store subscription so it can be disposed on process exit
					AppDomain.CurrentDomain.ProcessExit += (s, e) => { try { diagSubscription?.Dispose(); } catch { } };
				}
				else
				{
					// Diagnostics left disabled - nothing to do
				}
			}
			catch
			{
				// best-effort
			}
		}

		static async Task Main(string[] args)
		{
			if(args == null || args.Length == 0)
			{
				PrintUsage();
				return;
			}

			string action = args[ 0 ]!;
			if(string.IsNullOrWhiteSpace(action))
			{
				PrintUsage();
				return;
			}

			// Handle cookie management commands
			if(string.Equals(action, "cookies", StringComparison.OrdinalIgnoreCase))
			{
				HandleCookiesCommand(args);
				return;
			}

			// Handle HTTP verb commands (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, TRACE)
			if(IsHttpVerb(action))
			{
				await HandleHttpVerbCommand(args).ConfigureAwait(false);
				return;
			}

			// If action looks like a URL or alias, delegate to library which will resolve alias or use URL
			// Use local HandleHttpRequest to preserve CLI metrics display
			await HandleHttpRequest("GET", action).ConfigureAwait(false);
		}

		static void PrintUsage()
		{
			System.Console.WriteLine("Usage: HttpLibraryCLI <action> [arguments]");
			System.Console.WriteLine();
			System.Console.WriteLine("Actions:");
			System.Console.WriteLine();
			System.Console.WriteLine("HTTP Verbs:");
			System.Console.WriteLine("\tGET <url> .................. Perform HTTP GET request");
			System.Console.WriteLine("\tPOST <url> [body|@file] .... Perform HTTP POST request");
			System.Console.WriteLine("\tPUT <url> [body|@file] ..... Perform HTTP PUT request");
			System.Console.WriteLine("\tDELETE <url> ............... Perform HTTP DELETE request");
			System.Console.WriteLine("\tPATCH <url> [body|@file] ... Perform HTTP PATCH request");
			System.Console.WriteLine("\tHEAD <url> ................. Perform HTTP HEAD request");
			System.Console.WriteLine("\tOPTIONS <url> .............. Perform HTTP OPTIONS request");
			System.Console.WriteLine("\tTRACE <url> ................ Perform HTTP TRACE request");
			System.Console.WriteLine();
			System.Console.WriteLine("\tBody Format:");
			System.Console.WriteLine("\t'inline text' .............. Send inline text as body");
			System.Console.WriteLine("\t@filepath .................. Read file and send as body");
			System.Console.WriteLine("\tExamples:");
			System.Console.WriteLine("\tPOST <url> '{\"key\":\"value\"}'");
			System.Console.WriteLine("\tPOST <url> @C:\\data.json");
			System.Console.WriteLine();
			System.Console.WriteLine("Client Aliases:");
			System.Console.WriteLine("\t<client>[/path][?query][#fragment] .... Use client alias with optional URL parts");
			System.Console.WriteLine("\tExamples:");
			System.Console.WriteLine("\tgoogle ..................... GET https://google.com");
			System.Console.WriteLine("\tgoogle/search?q=test ....... GET https://google.com/search?q=test");
			System.Console.WriteLine("\tPOST bvk/api/endpoint ...... POST https://www.bvk.rs/api/endpoint");
			System.Console.WriteLine();
			System.Console.WriteLine("Cookie Management:");
			System.Console.WriteLine("\tcookies ls ................. List all cookies with time left");
			System.Console.WriteLine("\tcookies ls <client>......... List cookies for specific client (long format)");
			System.Console.WriteLine("\tcookies prune <client>...... Prune expired cookies for specific client");
			System.Console.WriteLine("\tcookies prune-all .......... Prune expired cookies for all clients");
			System.Console.WriteLine();
			System.Console.WriteLine("Direct URL (defaults to GET):");
			System.Console.WriteLine("\t<url> ...................... Perform HTTP GET request to URL");
			System.Console.WriteLine();
		}

		static bool IsHttpVerb(string verb)
		{
			string[] httpVerbs = { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE", "CONNECT" };
			foreach(string v in httpVerbs)
			{
				if(string.Equals(verb, v, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		static void HandleCookiesCommand(string[] args)
		{
			if(args.Length < 2)
			{
				System.Console.WriteLine("Error: cookies command requires a subcommand");
				System.Console.WriteLine();
				System.Console.WriteLine("Available subcommands:");
				System.Console.WriteLine("\tls [client] ........ List all cookies");
				System.Console.WriteLine("\tprune <client> ..... Prune expired cookies for client");
				System.Console.WriteLine("\tprune-all .......... Prune all expired cookies");
				return;
			}

			string subcommand = args[ 1 ];
			// Use read-only initialization when listing cookies to avoid starting timers or writing files
			if(string.Equals(subcommand, "ls", StringComparison.OrdinalIgnoreCase))
			{
				CookiePersistence.InitializeReadOnly(Constants.CookiesFile);
			}
			else
			{
				CookiePersistence.Initialize(Constants.CookiesFile);
			}

			if(string.Equals(subcommand, "ls", StringComparison.OrdinalIgnoreCase))
			{
				if(args.Length > 2 && !string.IsNullOrWhiteSpace(args[ 2 ]))
				{
					// List cookies for specific client (long format with time left)
					string clientName = args[ 2 ];
					ListCookiesForClient(clientName, longFormat: true);
				}
				else
				{
					// List all cookies with time left
					ListAllCookies();
				}
			}
			else if(string.Equals(subcommand, "prune", StringComparison.OrdinalIgnoreCase))
			{
				if(args.Length < 3 || string.IsNullOrWhiteSpace(args[ 2 ]))
				{
					System.Console.WriteLine("Error: prune requires a client name");
					System.Console.WriteLine("Usage: cookies prune <client>");
					return;
				}

				string clientName = args[ 2 ];
				CookiePersistence.PruneExpired(clientName);
				System.Console.WriteLine($"Pruned expired cookies for '{clientName}'");
			}
			else if(string.Equals(subcommand, "prune-all", StringComparison.OrdinalIgnoreCase))
			{
				CookiePersistence.PruneAll();
				System.Console.WriteLine("Pruned expired cookies for all clients");
			}
			else
			{
				System.Console.WriteLine($"Unknown cookies subcommand: {subcommand}");
				System.Console.WriteLine("Available subcommands: ls, prune, prune-all");
			}
		}

		static void ListAllCookies()
		{
			System.Collections.Generic.List<string> clientNames = CookiePersistence.GetPersistedClientNames();

			if(clientNames.Count == 0)
			{
				System.Console.WriteLine("No clients with persisted cookies found");
				return;
			}

			int totalCookies = 0;
			DateTime now = DateTime.UtcNow;

			foreach(string clientName in clientNames)
			{
				System.Collections.Generic.List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies(clientName);
				totalCookies += cookies.Count;

				System.Console.WriteLine($"Client: {clientName} ({cookies.Count} cookies)");

				if(cookies.Count > 0)
				{
					foreach(PersistedCookie cookie in cookies)
					{
						string timeLeft = Helpers.FormatTimeLeft(cookie.Expires, now);
						string flags = Helpers.BuildCookieFlags(cookie);

						System.Console.WriteLine($"\t[Domain={cookie.Domain ?? Constants.NotSetText}, Path={cookie.Path ?? Constants.DefaultCookiePath}, Expires={timeLeft}] {flags} \"{cookie.Name}\"=\"{cookie.Value}\"");
					}
				}
				System.Console.WriteLine();
			}

			System.Console.WriteLine($"Total: {clientNames.Count} clients, {totalCookies} cookies");
		}

		static void ListCookiesForClient(string clientName, bool longFormat)
		{
			System.Collections.Generic.List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies(clientName);

			if(cookies.Count == 0)
			{
				System.Console.WriteLine($"No cookies found for client '{clientName}'");
				return;
			}

			System.Console.WriteLine($"Cookies for client '{clientName}' ({cookies.Count} total):");
			System.Console.WriteLine();

			DateTime now = DateTime.UtcNow;

			foreach(PersistedCookie cookie in cookies)
			{
				System.Console.WriteLine($"\tDomain: {cookie.Domain ?? Constants.NotSetText}");
				System.Console.WriteLine($"\tPath: {cookie.Path ?? Constants.DefaultCookiePath}");
				System.Console.WriteLine($"\tName: \"{cookie.Name}\"");
				System.Console.WriteLine($"\tValue: \"{cookie.Value}\"");

				if(cookie.Expires.HasValue)
				{
					string timeLeft = Helpers.FormatTimeLeftLong(cookie.Expires.Value, now);
					System.Console.WriteLine($"\tExpires: {cookie.Expires.Value.ToString(Constants.Iso8601FormatSpecifier)} ({timeLeft})");
				}
				else
				{
					System.Console.WriteLine($"\tExpires: {Constants.SessionCookieText}");
				}

				System.Console.WriteLine($"\tSecure: {cookie.Secure}");
				System.Console.WriteLine($"\tHttpOnly: {cookie.HttpOnly}");
				System.Console.WriteLine($"\tSameSite: {cookie.SameSite ?? Constants.NotSetText}");
				System.Console.WriteLine();
			}
		}

		static async Task HandleHttpVerbCommand(string[] args)
		{
			string verb = args[ 0 ].ToUpperInvariant();

			if(args.Length < 2)
			{
				System.Console.WriteLine($"Error: {verb} requires a URL or client alias");
				System.Console.WriteLine($"Usage: {verb} <url|client_alias> [body|@filepath]");
				System.Console.WriteLine();
				System.Console.WriteLine("Examples:");
				System.Console.WriteLine($" {verb} https://api.example.com/endpoint");
				System.Console.WriteLine($" {verb} google/search?q=test");

				if(verb == "POST" || verb == "PUT" || verb == "PATCH")
				{
					System.Console.WriteLine($" {verb} https://api.example.com/endpoint '{{\"key\":\"value\"}}'");
					System.Console.WriteLine($" {verb} https://api.example.com/endpoint @C:\\data.json");
					System.Console.WriteLine($" {verb} bvk/api/endpoint '{{\"data\":\"value\"}}'");
				}

				return;
			}

			string url = args[ 1 ];
			string? bodyOrFile = args.Length > 2 ? args[ 2 ] : null;

			// Delegate to library - it accepts either full URL or client alias and will resolve accordingly
			// await HttpLibrary.HttpRequestExecutor.ExecuteVerbAsync(verb, url, bodyOrFile).ConfigureAwait(false);
			await HandleHttpRequest(verb, url, bodyOrFile).ConfigureAwait(false);
		}

		static bool IsValidUrl(string url, Microsoft.Extensions.Logging.ILogger logger)
		{
			if(!System.Uri.TryCreate(url, System.UriKind.Absolute, out System.Uri? uri))
			{
				logger.LogError("Invalid URL: {Url}", url);
				System.Console.WriteLine($"Error: Invalid URL format: {url}");
				return false;
			}

			if(uri.Scheme != "http" && uri.Scheme != "https")
			{
				logger.LogError("Unsupported URL scheme: {Scheme} (only http/https allowed)", uri.Scheme);
				System.Console.WriteLine($"Error: Unsupported URL scheme '{uri.Scheme}' - only http and https are allowed");
				return false;
			}

			return true;
		}

		static async Task HandleHttpRequest(string verb, string url, string? bodyOrFile = null)
		{
			// =======================================================================
			// RUNTIME APPLICATION CONFIGURATION - Loaded by library at startup
			// =======================================================================
			// NOTE: Callbacks must be registered BEFORE InitializeServices() is called
			// because the SocketsHttpHandler is configured during service registration
			// =======================================================================
			// The library preloads ApplicationConfiguration in ServiceConfiguration static constructor
			// and exposes it via ServiceConfiguration.AppConfig. Do NOT attempt to load a separate
			// configuration here or fall back to defaults. If configuration failed to load the
			// library initialization will throw and the process should exit.

			ApplicationConfiguration appConfig = HttpLibrary.ServiceConfiguration.AppConfig;

			// Determine which client will be used (need to peek at the URL)
			// For now, we'll register callbacks for "default" client since that's what gets selected
			string clientName = "default";

			// Configure callbacks from application configuration (per-client settings in application.json)
			try
			{
				ApplicationConfiguration cfgRoot = HttpLibrary.ServiceConfiguration.AppConfig ?? new ApplicationConfiguration();
				if(cfgRoot.Clients != null)
				{
					foreach(var pair in cfgRoot.Clients)
					{
						string cfgClientName = pair.Key;
						try
						{
							CallbackConfigurator.ConfigureFromApplicationSettings(cfgClientName, cfgRoot, Logger);
						}
						catch(Exception ex)
						{
							Logger.LogWarning(ex, "Failed to register callbacks for client {Client}", cfgClientName);
						}
					}
				}
				else
				{
					// Register a default/global handler set if needed
					CallbackConfigurator.ConfigureFromApplicationSettings("default", cfgRoot, Logger);
				}
			}
			catch(Exception ex)
			{
				Logger.LogError(ex, "Failed to configure callbacks from application configuration");
			}



			// =======================================================================
			// NOW Initialize Services - this will apply the registered callbacks
			// =======================================================================
			ConcurrentDictionary<string, IPooledHttpClient> registeredNames;
			ConcurrentDictionary<string, Uri> clientBaseAddresses;
			using ServiceProvider provider = HttpLibrary.ServiceConfiguration.InitializeServices(out registeredNames, out clientBaseAddresses);
			ILogger<Program> logger = provider.GetRequiredService<ILogger<Program>>();

			// Validate URL before proceeding
			string userInput = url;
			// Only accept explicit alias:// scheme or absolute http/https URLs. Old implicit alias resolution has been removed.
			bool isAliasScheme = url.StartsWith("alias://", StringComparison.OrdinalIgnoreCase);
			bool isHttpOrHttps = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

			if(isAliasScheme)
			{
				// Detected explicit alias:// URI - let the pipeline resolve it via AliasResolutionHandler
				logger.LogTrace("Detected 'alias://' scheme in input; deferring resolution to pipeline");
			}
			else if(!isHttpOrHttps)
			{
				// Implicit client alias resolution (bare client names) was removed. Require alias:// or full URL.
				logger.LogError("Unsupported input format: {UserInput}. Use explicit 'alias://<client>/path' or a full http/https URL.", userInput);
				System.Console.WriteLine($"Error: Unsupported input format: '{userInput}'. Use explicit 'alias://<client>/path' or a full 'https://...' URL.");
				return;
			}

			// No further validation required for alias://; for http/https the IsValidUrl check is applied below
			if(!isAliasScheme && !IsValidUrl(url, logger))
			{
				return;
			}

			// Select the best client based on URL using ClientSelector. When using alias:// choose a default registered client and let the handler resolve the URL.
			IPooledHttpClient? client;
			if(isAliasScheme)
			{
				client = registeredNames.Values.FirstOrDefault();
				if(client == null)
				{
					throw new InvalidOperationException("No registered clients available to handle alias:// request.");
				}
				// Keep clientName as the client's name for logging/redirect callbacks
				clientName = client.Name ?? clientName;
			}
			else
			{
				client = HttpLibrary.ClientSelector.SelectClientForUrl(url, registeredNames, clientBaseAddresses, logger);
				if(client == null)
				{
					throw new InvalidOperationException("No suitable client found for URL.");
				}
			}

			// =======================================================================
			// Configuration loaded from: application.json
			// - Certificate thumbprints can be updated without code changes
			// - Socket options can be tuned via configuration
			// - mTLS certificate selection is configurable
			// - File upload limits and progress thresholds are configurable
			//
			// For more examples: HttpLibrary/Notes/RuntimeCallbackExamples.md
			// =======================================================================

			// Add custom headers that will be included in ALL requests (example)
			// Uncomment to add custom headers:
			// client.AddRequestHeader("X-Custom-Header", "CustomValue");
			// client.AddRequestHeader("X-Request-ID", Guid.NewGuid().ToString());

			// Setup redirect callback to log redirects
			logger.LogDebug("Setting up redirect callback for client '{ClientName}'", clientName);
			client.RedirectCallback = (redirectInfo) =>
			{
				logger.LogWarning("Client '{ClientName}' redirected from {OriginalUrl} to {RedirectUrl} (Status: {StatusCode})",
						redirectInfo.ClientName,
						redirectInfo.OriginalUrl,
						redirectInfo.RedirectUrl,
						redirectInfo.StatusCode);

				// Also write to stderr so the message is visible even if logging filters hide it
				try
				{
					System.Console.Error.WriteLine($"Client '{redirectInfo.ClientName}' redirected from {redirectInfo.OriginalUrl} to {redirectInfo.RedirectUrl} (Status: {redirectInfo.StatusCode})");
				}
				catch
				{
					// ignore
				}

				// Example: Check if cancellation was requested
				if(redirectInfo.CancellationToken.IsCancellationRequested)
				{
					logger.LogWarning("Cancellation was requested, stopping redirect chain");
					return RedirectAction.Cancel;
				}

				// NOTE: Redirect limits are configured in clients.json (MaxRedirections property)
				// The library automatically enforces the client's MaxRedirections limit

				// Always follow redirects
				return RedirectAction.Follow;
			};

			// Setup progress callback to display progress for files > configured threshold
			logger.LogDebug("Setting up progress callback for client '{ClientName}'", clientName);
			int lastProgressPercentage = -1;
			long lastProgressBytes = -1;
			System.DateTimeOffset lastProgressUpdate = System.DateTimeOffset.MinValue;
			client.ProgressCallback = (progress) =>
			{
				// Prefer percentage-based bar when total size is known and above configured threshold
				if(progress.Stage == HttpProgressStage.DownloadingContent)
				{
					if(progress.TotalBytes.HasValue && appConfig.ProgressDisplay != null && progress.TotalBytes.Value > appConfig.ProgressDisplay.MinimumFileSizeBytes)
					{
						if(progress.ProgressPercentage.HasValue)
						{
							int currentPercentage = (int)progress.ProgressPercentage.Value;
							System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
							bool percentChanged = currentPercentage != lastProgressPercentage;
							long delta = progress.BytesTransferred - lastProgressBytes;
							long configuredDelta = appConfig.ProgressDisplay.ProgressUpdateDeltaBytes;
							int configuredInterval = appConfig.ProgressDisplay.ProgressUpdateIntervalMilliseconds;
							bool enoughBytes = delta >= configuredDelta || lastProgressBytes < 0;
							bool enoughTime = ( now - lastProgressUpdate ).TotalMilliseconds >= configuredInterval;
							if(percentChanged || enoughBytes || enoughTime)
							{
								lastProgressPercentage = currentPercentage;
								lastProgressBytes = progress.BytesTransferred;
								lastProgressUpdate = now;
								System.Console.Error.Write($"\rDownloading: {currentPercentage}% ({Helpers.FormatBytes(progress.BytesTransferred)} / {Helpers.FormatBytes(progress.TotalBytes.Value)})");
								System.Console.Error.Flush();
							}
						}
					}
					else
					{
						// Total unknown: show bytes transferred when threshold exceeded
						if(appConfig.ProgressDisplay != null && progress.BytesTransferred > appConfig.ProgressDisplay.MinimumFileSizeBytes)
						{
							System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
							long configuredDelta = appConfig.ProgressDisplay.ProgressUpdateDeltaBytes;
							int configuredInterval = appConfig.ProgressDisplay.ProgressUpdateIntervalMilliseconds;
							bool enoughBytes = ( progress.BytesTransferred - lastProgressBytes ) >= configuredDelta || lastProgressBytes < 0;
							bool enoughTime = ( now - lastProgressUpdate ).TotalMilliseconds >= configuredInterval;
							if(enoughBytes || enoughTime)
							{
								lastProgressBytes = progress.BytesTransferred;
								lastProgressUpdate = now;
								lastProgressPercentage = lastProgressPercentage < 0 ? 0 : lastProgressPercentage;
								System.Console.Error.Write($"\rDownloading: {Helpers.FormatBytes(progress.BytesTransferred)}");
								System.Console.Error.Flush();
							}
						}
					}
				}
				else if(progress.Stage == HttpProgressStage.Completed)
				{
					if(lastProgressPercentage >= 0)
					{
						System.Console.Error.WriteLine();
						System.Console.Error.Flush();
						lastProgressPercentage = -1;
					}
				}
			};

			try
			{
				// Execute HTTP request based on verb using optimized switch statement
				switch(verb)
				{
					case "GET":
					{
						// Always treat GET responses as binary streams for the CLI.
						string outDir = Directory.GetCurrentDirectory();
						HttpLibrary.HttpRequestExecutor.GetResult? getResult = await HttpLibrary.HttpRequestExecutor.GetAutoAsync(client, logger, url, outDir).ConfigureAwait(false);
						if(getResult == null)
						{
							break;
						}
						if(getResult.SavedToFile)
						{
							System.Console.WriteLine(getResult.FilePath ?? string.Empty);
						}
						else
						{
							// Content was returned as text; write it to stdout instead of saving a binary file
							if(!string.IsNullOrEmpty(getResult.Text))
							{
								System.Console.Write(getResult.Text);
							}
						}
						break;
					}
					case "POST":
					{
						HttpContent content = CreateHttpContent(bodyOrFile, "POST", appConfig ?? new ApplicationConfiguration());
						string responseBody = await HttpLibrary.HttpRequestExecutor.PostAsync(client, logger, url, content).ConfigureAwait(false);
						if(!string.IsNullOrEmpty(responseBody))
							System.Console.Write(responseBody);
						break;
					}
					case "PUT":
					{
						HttpContent content = CreateHttpContent(bodyOrFile, "PUT", appConfig ?? new ApplicationConfiguration());
						string responseBody = await HttpLibrary.HttpRequestExecutor.PutAsync(client, logger, url, content).ConfigureAwait(false);
						if(!string.IsNullOrEmpty(responseBody))
							System.Console.Write(responseBody);
						break;
					}
					case "DELETE":
					{
						string responseBody = await HttpLibrary.HttpRequestExecutor.DeleteAsync(client, logger, url).ConfigureAwait(false);
						if(!string.IsNullOrEmpty(responseBody))
							System.Console.Write(responseBody);
						break;
					}
					case "PATCH":
					{
						HttpContent content = CreateHttpContent(bodyOrFile, "PATCH", appConfig ?? new ApplicationConfiguration());
						string responseBody = await HttpLibrary.HttpRequestExecutor.PatchAsync(client, logger, url, content).ConfigureAwait(false);
						if(!string.IsNullOrEmpty(responseBody))
							System.Console.Write(responseBody);
						break;
					}
					case "HEAD":
					{
						string responseBody = await HttpLibrary.HttpRequestExecutor.HeadAsync(client, logger, url).ConfigureAwait(false);
						if(!string.IsNullOrEmpty(responseBody))
							System.Console.Write(responseBody);
						break;
					}
					case "OPTIONS":
					{
						string responseBody = await HttpLibrary.HttpRequestExecutor.OptionsAsync(client, logger, url).ConfigureAwait(false);
						if(!string.IsNullOrEmpty(responseBody))
							System.Console.Write(responseBody);
						break;
					}
					case "TRACE":
					{
						string responseBody = await HttpLibrary.HttpRequestExecutor.TraceAsync(client, logger, url).ConfigureAwait(false);
						if(!string.IsNullOrEmpty(responseBody))
							System.Console.Write(responseBody);
						break;
					}
					default:
					{
						System.Console.WriteLine($"Unsupported HTTP verb: {verb}");
						break;
					}
				}
			}
			finally
			{
				// Display metrics for all clients that were used (controlled by application configuration)
				ApplicationConfiguration cfg = HttpLibrary.ServiceConfiguration.AppConfig ?? new ApplicationConfiguration();
				if(cfg.MetricsDisplay != null && cfg.MetricsDisplay.Enabled)
				{
					System.Console.Error.WriteLine();
					System.Console.Error.WriteLine("=== HTTP Client Metrics ===");
					foreach(System.Collections.Generic.KeyValuePair<string, IPooledHttpClient> kvp in registeredNames)
					{
						IPooledHttpClient pooledClient = kvp.Value;
						PooledHttpClientMetrics metrics = pooledClient.Metrics;

						// Only display metrics for clients that meet the configured threshold
						if(metrics.TotalRequests >= cfg.MetricsDisplay.MinimumTotalRequests && metrics.TotalRequests > 0)
						{
							System.Console.Error.WriteLine();
							System.Console.Error.WriteLine($"Client: {kvp.Key}");
							System.Console.Error.WriteLine($"\tTotal Requests: {metrics.TotalRequests}");
							System.Console.Error.WriteLine($"\tSuccessful Requests: {metrics.SuccessfulRequests}");
							System.Console.Error.WriteLine($"\tFailed Requests: {metrics.FailedRequests}");
							System.Console.Error.WriteLine($"\tActive Requests: {metrics.ActiveRequests}");
							System.Console.Error.WriteLine($"\tTotal Bytes Received: {Helpers.FormatBytes(metrics.TotalBytesReceived)}");
							System.Console.Error.WriteLine($"\tAvg. Request Time: {metrics.AverageRequestMs:F2} ms");
						}
					}
				}
			}
		}

		static HttpContent CreateHttpContent(string? bodyOrFile, string verb, ApplicationConfiguration appConfig)
		{
			if(string.IsNullOrWhiteSpace(bodyOrFile))
			{
				return new StringContent(string.Empty);
			}

			if(bodyOrFile.StartsWith("@", StringComparison.OrdinalIgnoreCase))
			{
				// File path
				string filePath = bodyOrFile.Substring(1);
				// Validate file exists
				if(!System.IO.File.Exists(filePath))
				{
					throw new System.IO.FileNotFoundException($"File not found: {filePath}", filePath);
				}

				// Use configured file size limit
				System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
				if(fileInfo.Length > appConfig.FileUpload.MaxFileSizeBytes)
				{
					throw new System.ArgumentException(
						$"File too large: {fileInfo.Length:N0} bytes (maximum: {appConfig.FileUpload.MaxFileSizeBytes:N0} bytes)",
						nameof(bodyOrFile));
				}

				string fileContent = System.IO.File.ReadAllText(filePath);
				return new StringContent(fileContent);
			}

			// Inline text
			return new StringContent(bodyOrFile);
		}
	}
}