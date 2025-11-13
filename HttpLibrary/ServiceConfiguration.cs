using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace HttpLibrary
{
	public static class ServiceConfiguration
	{
		// Simple passthrough DelegatingHandler used as a safe fallback when DI-based handler creation fails
		private sealed class PassthroughDelegatingHandler : DelegatingHandler
		{
			protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
			{
				return base.SendAsync(request, cancellationToken);
			}
		}

		// Preloaded application configuration for callers (CLI expects ServiceConfiguration.AppConfig)
		// Attempt to load configured application file first, then fall back to library config filename
		private static readonly string _configuredApplicationFile = LibraryConfigurationProvider.Paths?.ApplicationConfigFile ?? Constants.ApplicationConfigFile;
		public static ApplicationConfiguration AppConfig { get; } =
			ApplicationConfigurationLoader.Load(_configuredApplicationFile) ?? ApplicationConfigurationLoader.Load(Constants.LibraryConfigurationFile) ?? new ApplicationConfiguration();

		// Test hook: allow tests to provide a custom primary HttpMessageHandler factory.
		// When non-null, InitializeServices will use this factory instead of creating a SocketsHttpHandler.
		internal static Func<HttpMessageHandler>? PrimaryHandlerFactory { get; set; }

		public static ServiceProvider InitializeServices(out ConcurrentDictionary<string, IPooledHttpClient> registeredNames, out ConcurrentDictionary<string, Uri> clientBaseAddresses)
		{
			// Resolve configuration file paths relative to application base directory first
			static string ResolveConfigPath(string fileName)
			{
				if(string.IsNullOrWhiteSpace(fileName))
					return fileName ?? string.Empty;
				// Prefer application base directory (where exe is located)
				string appPath = System.IO.Path.Combine(AppContext.BaseDirectory, fileName);
				if(File.Exists(appPath))
					return appPath;
				// Fall back to current working directory
				string cwdPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), fileName);
				if(File.Exists(cwdPath))
					return cwdPath;
				// Otherwise return path in app base dir (for creation)
				return appPath;
			}

			// Ensure configuration files exist (defaults.json, clients.json, cookies.json, application.json)
			EnsureConfigurationFilesExist();

			// Use configured filenames from HttpLibraryConfiguration.json if present
			LibraryFilePaths libPaths = LibraryConfigurationProvider.Paths ?? new LibraryFilePaths();

			string defaultsPath = ResolveConfigPath(libPaths.DefaultsConfigFile ?? Constants.DefaultsConfigFile);
			string configPath = ResolveConfigPath(libPaths.ClientsConfigFile ?? Constants.ClientsConfigFile);
			string cookiesPath = ResolveConfigPath(libPaths.CookiesFile ?? Constants.CookiesFile);

			// Create DI container
			ServiceCollection services = new ServiceCollection();

			// If host provided an ILoggerFactory via LoggerBridge, register it so DI loggers use the same pipeline
			ILoggerFactory? hostLoggerFactory = LoggerBridge.GetFactory();
			if(hostLoggerFactory != null)
			{
				services.AddSingleton(typeof(ILoggerFactory), hostLoggerFactory);
				// Also register ILogger<T> support via the provided factory
				services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
			}

			// Register cookie persistence implementation
			services.AddSingleton<ICookiePersistence, CookiePersistenceImpl>();

			// Register compliance handler
			services.AddTransient<HttpLibrary.Handlers.HttpComplianceHandler>();
			// Add logging handler to capture full request/response headers at Debug level
			services.AddTransient<HttpLibrary.Handlers.LoggingHandler>();
			// Register alias resolver handler (opt-in). It's safe to register even if no aliases configured.
			services.AddTransient<HttpLibrary.Handlers.AliasResolutionHandler>();

			// Register a builder filter that will ensure the AliasResolutionHandler is placed as the outermost handler
			services.AddSingleton<Microsoft.Extensions.Http.IHttpMessageHandlerBuilderFilter, HttpLibrary.Handlers.AliasMessageHandlerBuilderFilter>();

			// Initialize client base addresses dictionary
			clientBaseAddresses = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
			// Expose registered client base addresses for other components (alias resolver fallback)
			RegisteredClientBaseAddresses = clientBaseAddresses;

			// load multiple pooled client cfgs from JSON
			HttpClientConfig[]? cfgs = null;
			HttpClientConfig defaults;

			// Load defaults first (required for client fallbacks and validation logging)
			if(File.Exists(defaultsPath))
			{
				try
				{
					string defTxt = File.ReadAllText(defaultsPath);
					defaults = ConfigurationLoader.LoadDefaultClientConfig(defTxt, defaultsPath);
				}
				catch(Exception ex)
				{
					LoggerBridge.LogError(ex, "Failed to load defaults from {Path}", defaultsPath);
					throw;
				}
			}
			else
			{
				throw new InvalidOperationException($"{Constants.DefaultsConfigFile} not found at {defaultsPath}. This file is required.");
			}

			// Load client configurations
			if(File.Exists(configPath))
			{
				try
				{
					string txt = File.ReadAllText(configPath);
					cfgs = ConfigurationLoader.LoadClientConfigs(txt, configPath, defaults);
				}
				catch(Exception ex)
				{
					LoggerBridge.LogError(ex, "Failed to load client configurations from {Path}", configPath);
					throw;
				}
			}

			if(cfgs == null || cfgs.Length == 0)
			{
				throw new InvalidOperationException($"No valid pooled client configurations found in {configPath}");
			}

			// Register named HttpClient instances based on configuration
			foreach(HttpClientConfig clientConfig in cfgs)
			{
				// Capture loop variables into locals to avoid closure capture issues when lambdas are created below
				string name = clientConfig.Name ?? Constants.DefaultClientName;
				string clientName = name;

				// Pre-parse base URI to avoid capturing out parameters inside lambdas
				Uri? preParsedBaseUri = null;
				if(!string.IsNullOrWhiteSpace(clientConfig.Uri) && Uri.TryCreate(clientConfig.Uri, UriKind.Absolute, out Uri? tmp))
				{
					preParsedBaseUri = tmp;
					// store mapping now (safe - not inside lambda)
					clientBaseAddresses[ clientName ] = preParsedBaseUri;
				}

				HttpClientConfig cfgLocal = clientConfig;

				services.AddHttpClient(clientName)
					.ConfigureHttpClient((sp, httpClient) =>
					{
						// Set base address if present
						if(preParsedBaseUri != null)
						{
							httpClient.BaseAddress = preParsedBaseUri;
						}

						if(clientConfig.Timeout.HasValue)
						{
							httpClient.Timeout = clientConfig.Timeout.Value;
						}

						// Prefer HTTP/2 where available
						httpClient.DefaultRequestVersion = new Version(2, 0);
						httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
					})
					.ConfigurePrimaryHttpMessageHandler(sp =>
					{
						// If tests have provided a custom primary handler factory, use it. This allows tests to inject
						// an in-memory handler (e.g., TestResponseHandler) instead of performing real network I/O.
						if(PrimaryHandlerFactory != null)
						{
							return PrimaryHandlerFactory();
						}

						SocketsHttpHandler handler = new SocketsHttpHandler();

						// Cookies: use container per-client when enabled
						handler.UseCookies = cfgLocal.UseCookies.GetValueOrDefault();

						// If cookie handling is enabled, attempt to obtain or register a CookieContainer from the
						// cookie persistence implementation so cookies received by the handler are stored in the
						// same runtime container used for persistence.
						if(handler.UseCookies)
						{
							try
							{
								ICookiePersistence? cp = sp.GetService<ICookiePersistence>();
								if(cp != null && cfgLocal.CookiePersistenceEnabled.GetValueOrDefault())
								{
									// Register client with persistence enabled (this will populate runtime container from persisted store)
									cp.RegisterClient(clientName, preParsedBaseUri?.ToString(), persist: true);
									CookieContainer? cont = cp.GetContainer(clientName);
									if(cont != null)
									{
										handler.CookieContainer = cont;
										try
										{ LoggerBridge.LogInformation("Wired cookie container for client '{Client}' into SocketsHttpHandler (persistence enabled)", name); }
										catch { }
									}
									else
									{
										handler.CookieContainer = new CookieContainer();
										try
										{ LoggerBridge.LogInformation("Created ephemeral cookie container for client '{Client}' (persistence enabled but no container returned)", clientName); }
										catch { }
									}
								}
								else
								{
									// Persistence is not enabled for this client; use a fresh container for runtime-only cookies
									handler.CookieContainer = new CookieContainer();
									try
									{
										LoggerBridge.LogInformation("Cookie persistence not enabled for client '{Client}'; using ephemeral cookie container", clientName);
									}
									catch { }
								}
							}
							catch
							{
								// best-effort: do not fail handler creation due to cookie wiring
							}
						}

						// TLS settings: restrict to TLS1.2 and TLS1.3 and prefer HTTP/2 via ALPN
						System.Net.Security.SslClientAuthenticationOptions sslOptions = new System.Net.Security.SslClientAuthenticationOptions
						{
							EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
							CertificateRevocationCheckMode = X509RevocationMode.Online,
							ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 }
						};

						// Wire per-client callbacks if registered
						SocketCallbackHandlers? clientHandlers = CallbackRegistry.GetHandlers(name);

						if(clientHandlers != null)
						{
							if(clientHandlers.ConnectCallback != null)
							{
								handler.ConnectCallback = clientHandlers.ConnectCallback;
							}

							if(clientHandlers.PlaintextStreamFilter != null)
							{
								handler.PlaintextStreamFilter = clientHandlers.PlaintextStreamFilter;
							}

							if(clientHandlers.ServerCertificateCustomValidationCallback != null)
							{
								sslOptions.RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
								{
									try
									{
										X509Certificate2? cert2 = cert as X509Certificate2;
										if(cert2 == null && cert != null)
											cert2 = new X509Certificate2(cert);
										// Construct a minimal HttpRequestMessage with RequestUri (non-null)
										HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, preParsedBaseUri ?? new Uri(Constants.DefaultClientBaseUri));
										return clientHandlers.ServerCertificateCustomValidationCallback(req, cert2, chain as X509Chain, errors);
									}
									catch(Exception ex)
									{
										LoggerBridge.LogWarning("Client certificate validation callback threw an exception: {Message}", ex.Message);
										return false;
									}
								};
							}
						}
						else
						{
							// No client handlers - use default validation with optional pinning
							sslOptions.RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
							{
								try
								{
									if(AppConfig.EnableCertificatePinning && AppConfig.CertificatePinning is not null && AppConfig.CertificatePinning.PinnedThumbprints is not null && AppConfig.CertificatePinning.PinnedThumbprints.Count > 0)
									{
										if(cert is null)
											return false;
										X509Certificate2 cert2 = cert as X509Certificate2 ?? new X509Certificate2(cert);
										string thumb = Convert.ToBase64String(cert2.GetCertHash());
										bool pinned = false;
										foreach(string p in AppConfig.CertificatePinning.PinnedThumbprints)
										{
											if(string.Equals(p, thumb, StringComparison.OrdinalIgnoreCase))
											{ pinned = true; break; }
											string hex = BitConverter.ToString(cert2.GetCertHash()).Replace("-", string.Empty);
											if(string.Equals(p, hex, StringComparison.OrdinalIgnoreCase))
											{ pinned = true; break; }
										}
										if(!pinned)
										{ LoggerBridge.LogWarning("Certificate pinning: server cert thumbprint not in allowed list"); return false; }
									}

									return errors == SslPolicyErrors.None;
								}
								catch(Exception ex)
								{
									LoggerBridge.LogWarning("Certificate validation encountered an exception: {Message}", ex.Message);
									return false;
								}
							};
						}

						handler.SslOptions = sslOptions;

						// Connection pooling and other production-safe defaults
						handler.MaxConnectionsPerServer = clientConfig.MaxConnectionsPerServer.GetValueOrDefault(int.MaxValue);
						handler.PooledConnectionLifetime = clientConfig.PooledConnectionLifetime ?? TimeSpan.FromMinutes(10);

						// Apply decompression settings from configuration
						handler.AutomaticDecompression = ParseDecompression(clientConfig.Decompression);

						// Apply connection and timeout related settings when provided
						if(clientConfig.ConnectTimeout.HasValue)
						{
							handler.ConnectTimeout = clientConfig.ConnectTimeout.Value;
						}

						if(clientConfig.PooledConnectionIdleTimeout.HasValue)
						{
							handler.PooledConnectionIdleTimeout = clientConfig.PooledConnectionIdleTimeout.Value;
						}

						if(clientConfig.Expect100ContinueTimeout.HasValue)
						{
							handler.Expect100ContinueTimeout = clientConfig.Expect100ContinueTimeout.Value;
						}

						if(clientConfig.MaxResponseHeadersLength.HasValue)
						{
							handler.MaxResponseHeadersLength = clientConfig.MaxResponseHeadersLength.Value;
						}

						// Apply redirect settings from configuration so library can intercept redirects when desired
						handler.AllowAutoRedirect = clientConfig.AllowAutoRedirect.GetValueOrDefault(false);
						handler.MaxAutomaticRedirections = clientConfig.MaxRedirections ?? Constants.DefaultMaxRedirections;

						// Proxy settings: map HttpProxy/HttpsProxy to handler.Proxy when provided
						if(clientConfig.UseProxy.GetValueOrDefault(false))
						{
							string? proxyUri = !string.IsNullOrWhiteSpace(clientConfig.HttpsProxy) ? clientConfig.HttpsProxy : clientConfig.HttpProxy;
							if(!string.IsNullOrWhiteSpace(proxyUri))
							{
								try
								{
									System.Uri parsed = new System.Uri(proxyUri);
									System.Net.WebProxy webProxy = new System.Net.WebProxy(parsed);
									if(!string.IsNullOrWhiteSpace(clientConfig.ProxyUsername) || !string.IsNullOrWhiteSpace(clientConfig.ProxyPassword))
									{
										webProxy.Credentials = new System.Net.NetworkCredential(clientConfig.ProxyUsername ?? string.Empty, clientConfig.ProxyPassword ?? string.Empty);
									}
									handler.Proxy = webProxy;
									handler.UseProxy = true;
								}
								catch
								{
									// best-effort: if parsing fails, leave UseProxy enabled to allow system proxy
									handler.UseProxy = true;
								}
							}
						}

						// Runtime SSL validation toggle: when DisableSslValidation is true, accept any server certificate
						if(clientConfig.DisableSslValidation.GetValueOrDefault(false))
						{
							try
							{
								handler.SslOptions.RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
							}
							catch
							{
								// ignore failures; best-effort
							}
						}

						// Log redirect handler settings for debugging (use LoggerBridge since DI logger isn't available here)
						try
						{
							LoggerBridge.LogInformation("Configured redirect behavior for client '{Client}': AllowAutoRedirect={AllowAutoRedirect}, MaxAutomaticRedirections={MaxAutomaticRedirections}", name, handler.AllowAutoRedirect, handler.MaxAutomaticRedirections);
						}
						catch
						{
							// best-effort logging only
						}

						return handler;
					})
					// Add core handlers: compliance and logging first, then cookie capture. Alias resolution must be outermost so it can rewrite alias:// URIs before other handlers inspect them.
					.AddHttpMessageHandler<HttpLibrary.Handlers.HttpComplianceHandler>()
					.AddHttpMessageHandler<HttpLibrary.Handlers.LoggingHandler>()
					// Add CookieCaptureHandler for clients with persistence enabled (registered below)
					.AddHttpMessageHandler(sp =>
					{
						try
						{
							return new HttpLibrary.Handlers.CookieCaptureHandler(sp.GetRequiredService<ICookiePersistence>(), clientName);
						}
						catch
						{
							// best-effort fallback: return a simple passthrough delegating handler that forwards the call
							return new PassthroughDelegatingHandler();
						}
					})
					// Finally add AliasResolutionHandler as the outermost handler so it runs before logging/cookie handlers and rewrites alias:// requests to http/https
					.AddHttpMessageHandler(sp =>
					{
						try
						{
							if(ServiceConfiguration.AppConfig?.Aliases != null)
							{
								return sp.GetRequiredService<HttpLibrary.Handlers.AliasResolutionHandler>();
							}
						}
						catch { }
						return new PassthroughDelegatingHandler();
					});
			}

			// Build provider
			ServiceProvider provider = services.BuildServiceProvider();

			// Initialize cookie persistence from provider and wire facade
			ICookiePersistence cookiePersistence = provider.GetRequiredService<ICookiePersistence>();
			cookiePersistence.Initialize(Constants.CookiesFile);
			try
			{ CookiePersistence.SetImplementation(cookiePersistence); }
			catch { }

			// Ensure runtime cookie containers are registered for each client when persistence is enabled.
			try
			{
				foreach(HttpClientConfig clientConfig in cfgs)
				{
					string name = clientConfig.Name ?? Constants.DefaultClientName;
					if(clientConfig.UseCookies.GetValueOrDefault() && clientConfig.CookiePersistenceEnabled.GetValueOrDefault())
					{
						string? baseAddr = clientBaseAddresses.GetValueOrDefault(name)?.ToString();
						cookiePersistence.RegisterClient(name, baseAddr, persist: true);
					}
				}
			}
			catch
			{
				// best-effort
			}

			// Initialize registeredNames by creating PooledHttpClient instances for each configured client
			registeredNames = new ConcurrentDictionary<string, IPooledHttpClient>(StringComparer.OrdinalIgnoreCase);
			try
			{
				IHttpClientFactory httpFactory = provider.GetRequiredService<IHttpClientFactory>();
				ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();

				foreach(HttpClientConfig clientConfig in cfgs)
				{
					string name = clientConfig.Name ?? Constants.DefaultClientName;
					try
					{
						HttpClient httpClient = httpFactory.CreateClient(name);
						PooledHttpClientOptions opts = new PooledHttpClientOptions()
						{
							Name = name,
							BaseAddress = clientBaseAddresses.GetValueOrDefault(name),
							Timeout = clientConfig.Timeout,
							MaxRedirections = clientConfig.MaxRedirections ?? Constants.DefaultMaxRedirections
						};

						// Population of default request headers from configuration (defaults.json / clients.json)
						try
						{
							if(clientConfig.DefaultRequestHeaders != null)
							{
								foreach(var hdr in clientConfig.DefaultRequestHeaders)
								{
									if(hdr.Key is not null)
									{
										opts.DefaultRequestHeaders[ hdr.Key ] = hdr.Value ?? string.Empty;
									}
								}
							}

							// Respect configured default request version if provided
							if(clientConfig.DefaultRequestVersion != null)
							{
								opts.DefaultRequestVersion = clientConfig.DefaultRequestVersion;
							}

							// Apply decompression setting if provided
							if(!string.IsNullOrWhiteSpace(clientConfig.Decompression))
							{
								opts.AutomaticDecompression = ParseDecompression(clientConfig.Decompression);
							}
						}
						catch(Exception ex)
						{
							LoggerBridge.LogWarning("Error processing pooled client options for '{Client}': {Message}", name, ex.Message);
						}

						ILogger<PooledHttpClient> clientLogger = loggerFactory.CreateLogger<PooledHttpClient>();
						IPooledHttpClient pooled = new PooledHttpClient(httpClient, Microsoft.Extensions.Options.Options.Create(opts), clientLogger);
						registeredNames[ name ] = pooled;

						// Diagnostic: log default headers applied for this client
						try
						{
							if(opts.DefaultRequestHeaders != null && opts.DefaultRequestHeaders.Count > 0)
							{
								LoggerBridge.LogInformation("DefaultRequestHeaders for client '{Client}': {Headers}", name, string.Join(", ", opts.DefaultRequestHeaders.Select(kv => $"{kv.Key}={kv.Value}")));
							}
						}
						catch { }

						// Diagnostic: log whether a runtime cookie container exists for this client
						try
						{
							ICookiePersistence? cp2 = provider.GetService<ICookiePersistence>();
							if(cp2 != null)
							{
								CookieContainer? runtimeContainer = cp2.GetContainer(name);
								if(runtimeContainer != null)
								{
									LoggerBridge.LogInformation("Client '{Client}' has a runtime cookie container registered", name);
								}
								else
								{
									LoggerBridge.LogInformation("Client '{Client}' does not have a runtime cookie container registered", name);
								}
							}
						}
						catch { }
					}
					catch(Exception ex)
					{
						LoggerBridge.LogWarning("Failed to create pooled client '{ClientName}': {Message}", name, ex.Message);
					}
				}
			}
			catch(Exception ex)
			{
				LoggerBridge.LogWarning("Failed to initialize pooled clients: {Message}", ex.Message);
			}

			registeredNames = registeredNames ?? new ConcurrentDictionary<string, IPooledHttpClient>(StringComparer.OrdinalIgnoreCase);

			// Ensure static view of registered base addresses is set
			try
			{ RegisteredClientBaseAddresses = clientBaseAddresses; }
			catch { }

			return provider;
		}

		// Exposed mapping of client name -> base address populated during InitializeServices
		public static ConcurrentDictionary<string, Uri> RegisteredClientBaseAddresses { get; private set; } = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

		private static void EnsureConfigurationFilesExist()
		{
			try
			{
				// Use file names from library configuration if provided
				LibraryFilePaths libPaths = LibraryConfigurationProvider.Paths ?? new LibraryFilePaths();
				string defaultsFileName = libPaths.DefaultsConfigFile ?? Constants.DefaultsConfigFile;
				string clientsFileName = libPaths.ClientsConfigFile ?? Constants.ClientsConfigFile;
				string cookiesFileName = libPaths.CookiesFile ?? Constants.CookiesFile;
				string applicationFileName = libPaths.ApplicationConfigFile ?? Constants.ApplicationConfigFile;

				string baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();

				string defaultsPath = Path.Combine(baseDir, defaultsFileName);
				string clientsPath = Path.Combine(baseDir, clientsFileName);
				string cookiesPath = Path.Combine(baseDir, cookiesFileName);
				string applicationPath = Path.Combine(baseDir, applicationFileName);

				// Create defaults.json from HttpClientConfig defaults if missing
				if(!File.Exists(defaultsPath))
				{
					HttpClientConfig template = new HttpClientConfig();

					string json = JsonSerializer.Serialize<HttpClientConfig>(template, ApplicationJsonContext.Default.HttpClientConfig);

					Directory.CreateDirectory(Path.GetDirectoryName(defaultsPath) ?? baseDir);
					File.WriteAllText(defaultsPath, json);
					LoggerBridge.LogInformation("Created defaults configuration file at {Path}", defaultsPath);
				}

				// Create clients.json with a single default client if missing
				if(!File.Exists(clientsPath))
				{
					ClientsFile clientsFile = new ClientsFile();
					clientsFile.ConfigVersion = 1;
					clientsFile.Clients.Add(new HttpClientConfig
					{
						Name = Constants.DefaultClientName,
						Uri = Constants.DefaultClientBaseUri,
						Timeout = TimeSpan.FromMinutes(1),
						UseCookies = true,
						CookiePersistenceEnabled = true,
						AllowAutoRedirect = false,
						MaxRedirections = Constants.DefaultMaxRedirections
					});

					string json = JsonSerializer.Serialize<ClientsFile>(clientsFile, ApplicationJsonContext.Default.ClientsFile);

					Directory.CreateDirectory(Path.GetDirectoryName(clientsPath) ?? baseDir);
					File.WriteAllText(clientsPath, json);
					LoggerBridge.LogInformation("Created clients configuration file at {Path}", clientsPath);
				}

				// Create empty cookies.json if missing
				if(!File.Exists(cookiesPath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(cookiesPath) ?? baseDir);
					File.WriteAllText(cookiesPath, Constants.InitialCookiesFileContents);
					LoggerBridge.LogInformation("Created empty cookies file at {Path}", cookiesPath);
				}

				// Create application.json with default ApplicationSettingsRoot if missing
				if(!File.Exists(applicationPath))
				{
					ApplicationSettingsRoot root = new ApplicationSettingsRoot();
					string json = JsonSerializer.Serialize<ApplicationSettingsRoot>(root, ApplicationJsonContext.Default.ApplicationSettingsRoot);
					Directory.CreateDirectory(Path.GetDirectoryName(applicationPath) ?? baseDir);
					File.WriteAllText(applicationPath, json);
					LoggerBridge.LogInformation("Created application configuration file at {Path}", applicationPath);
				}
			}
			catch(Exception ex)
			{
				// Swallow exceptions here but log; initialization should continue and surface errors when files are read
				try
				{
					LoggerBridge.LogError(ex, "Failed to ensure configuration files exist");
				}
				catch
				{
					// ignore logging failures
				}
			}
		}

		static DecompressionMethods ParseDecompression(string? s)
		{
			if(string.IsNullOrWhiteSpace(s))
			{
				return DecompressionMethods.None;
			}
			string[] parts = s.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
			DecompressionMethods result = 0;
			foreach(string p in parts)
			{
				if(Enum.TryParse<DecompressionMethods>(p.Trim(), true, out DecompressionMethods v))
				{
					result |= v;
				}
			}
			return result;
		}

		// Internal helper for tests: create a SocketsHttpHandler configured according to a given HttpClientConfig
		internal static SocketsHttpHandler CreateHandlerFromConfig(HttpClientConfig cfg)
		{
			SocketsHttpHandler handler = new SocketsHttpHandler();

			handler.UseCookies = cfg.UseCookies.GetValueOrDefault();
			handler.MaxConnectionsPerServer = cfg.MaxConnectionsPerServer.GetValueOrDefault(int.MaxValue);
			handler.PooledConnectionLifetime = cfg.PooledConnectionLifetime ?? TimeSpan.FromMinutes(10);
			handler.AutomaticDecompression = ParseDecompression(cfg.Decompression);
			if(cfg.ConnectTimeout.HasValue)
				handler.ConnectTimeout = cfg.ConnectTimeout.Value;
			if(cfg.PooledConnectionIdleTimeout.HasValue)
				handler.PooledConnectionIdleTimeout = cfg.PooledConnectionIdleTimeout.Value;
			if(cfg.Expect100ContinueTimeout.HasValue)
				handler.Expect100ContinueTimeout = cfg.Expect100ContinueTimeout.Value;
			if(cfg.MaxResponseHeadersLength.HasValue)
				handler.MaxResponseHeadersLength = cfg.MaxResponseHeadersLength.Value;

			// Proxy settings: create concrete IWebProxy when configured
			if(cfg.UseProxy.GetValueOrDefault(false))
			{
				string? proxyUri = !string.IsNullOrWhiteSpace(cfg.HttpsProxy) ? cfg.HttpsProxy : cfg.HttpProxy;
				if(!string.IsNullOrWhiteSpace(proxyUri))
				{
					try
					{
						System.Uri parsed = new System.Uri(proxyUri);
						System.Net.WebProxy webProxy = new System.Net.WebProxy(parsed);
						if(!string.IsNullOrWhiteSpace(cfg.ProxyUsername) || !string.IsNullOrWhiteSpace(cfg.ProxyPassword))
						{
							webProxy.Credentials = new System.Net.NetworkCredential(cfg.ProxyUsername ?? string.Empty, cfg.ProxyPassword ?? string.Empty);
						}
						handler.Proxy = webProxy;
						handler.UseProxy = true;
					}
					catch
					{
						// If proxy URI parsing fails, still set UseProxy so system proxy may be used; best-effort
						handler.UseProxy = true;
					}
				}
			}

			// SSL/TLS validation toggle: when disabled, accept any server certificate
			if(cfg.DisableSslValidation.GetValueOrDefault(false))
			{
				try
				{
					handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
				}
				catch
				{
					// Best-effort: some runtimes may not allow setting this property; ignore failures
				}
			}

			return handler;
		}
	}
}