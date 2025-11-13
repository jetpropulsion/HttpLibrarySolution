using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;

namespace HttpLibrary
{
	/// <summary>
	/// Extension helpers to register the typed pooled client and metrics with the IServiceCollection.
	/// </summary>
	public static class PooledHttpClientServiceCollectionExtensions
	{
		public static IServiceCollection AddPooledHttpClient(this IServiceCollection services, Action<PooledHttpClientOptions>? configureOptions = null)
		{
			if(services == null)
			{
				throw new ArgumentNullException(nameof(services));
			}
			if(configureOptions != null)
			{
				services.Configure(configureOptions);
			}
			else
			{
				services.Configure<PooledHttpClientOptions>(_ => { });
			}
			services.AddSingleton<PooledHttpClientMetrics>();
			services.AddHttpClient<IPooledHttpClient, PooledHttpClient>((sp, client) =>
			{
				PooledHttpClientOptions opts = sp.GetRequiredService<IOptions<PooledHttpClientOptions>>().Value;
				if(opts.BaseAddress is not null)
				{
					client.BaseAddress = opts.BaseAddress;
				}
				client.DefaultRequestVersion = opts.DefaultRequestVersion;
				if(opts.Timeout.HasValue)
				{
					client.Timeout = opts.Timeout.Value;
				}

				ILogger<PooledHttpClient>? logger = sp.GetService<ILogger<PooledHttpClient>>();

				foreach(System.Collections.Generic.KeyValuePair<string, string> kv in opts.DefaultRequestHeaders)
				{
					if(!client.DefaultRequestHeaders.Contains(kv.Key))
					{
						// RFC 7230 Section 3.2: Validate header name and value
						if(!HttpHeaderValidator.IsValidHeaderName(kv.Key))
						{
							logger?.LogWarning("Invalid default header name '{HeaderName}' - skipping (RFC 7230 violation)", kv.Key);
							continue;
						}
						if(!HttpHeaderValidator.IsValidHeaderValue(kv.Value))
						{
							logger?.LogWarning("Invalid default header value for '{HeaderName}' - skipping (RFC 7230 violation)", kv.Key);
							continue;
						}

						// RFC 7231 Section 5.5.3: Special handling for User-Agent
						if(string.Equals(kv.Key, "User-Agent", StringComparison.OrdinalIgnoreCase))
						{
							if(!HttpHeaderValidator.IsValidUserAgent(kv.Value))
							{
								logger?.LogWarning("Invalid default User-Agent header - skipping (RFC 7231 violation)");
								continue;
							}
							client.DefaultRequestHeaders.Remove("User-Agent");
							client.DefaultRequestHeaders.UserAgent.Clear();
							foreach(ProductInfoHeaderValue token in PooledHttpClient.ParseUserAgentTokens(kv.Value))
							{
								client.DefaultRequestHeaders.UserAgent.Add(token);
							}
						}
						else
						{
							client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
						}
					}
				}
			});
			return services;
		}

		public static IServiceCollection AddPooledHttpClient(this IServiceCollection services, string clientName, Action<PooledHttpClientOptions>? configureOptions = null, Action<SocketsHttpHandler>? configureHandler = null)
		{
			if(services == null)
			{
				throw new ArgumentNullException(nameof(services));
			}
			if(string.IsNullOrWhiteSpace(clientName))
			{
				throw new ArgumentException("clientName must not be null or whitespace", nameof(clientName));
			}
			services.Configure<PooledHttpClientOptions>(clientName, opts =>
			{
				if(configureOptions != null)
				{
					configureOptions(opts);
				}
				opts.Name = clientName;
			});
			services.AddSingleton<PooledHttpClientMetrics>();
			if(!services.Any(sd => sd.ServiceType == typeof(INamedPooledHttpClientProvider)))
			{
				services.AddSingleton<INamedPooledHttpClientProvider, NamedPooledHttpClientProvider>();
			}
			IHttpClientBuilder builder = services.AddHttpClient(clientName, (sp, client) =>
			{
				PooledHttpClientOptions opts = sp.GetRequiredService<IOptionsMonitor<PooledHttpClientOptions>>().Get(clientName);
				if(opts.BaseAddress is not null)
				{
					client.BaseAddress = opts.BaseAddress;
				}
				client.DefaultRequestVersion = opts.DefaultRequestVersion;
				if(opts.Timeout.HasValue)
				{
					client.Timeout = opts.Timeout.Value;
				}

				ILogger<PooledHttpClient>? logger = sp.GetService<ILogger<PooledHttpClient>>();

				foreach(System.Collections.Generic.KeyValuePair<string, string> kv in opts.DefaultRequestHeaders)
				{
					if(!client.DefaultRequestHeaders.Contains(kv.Key))
					{
						// RFC 7230 Section 3.2: Validate header name and value
						if(!HttpHeaderValidator.IsValidHeaderName(kv.Key))
						{
							logger?.LogWarning("Invalid default header name '{HeaderName}' - skipping (RFC 7230 violation)", kv.Key);
							continue;
						}
						if(!HttpHeaderValidator.IsValidHeaderValue(kv.Value))
						{
							logger?.LogWarning("Invalid default header value for '{HeaderName}' - skipping (RFC 7230 violation)", kv.Key);
							continue;
						}

						// RFC 7231 Section 5.5.3: Special handling for User-Agent
						if(string.Equals(kv.Key, "User-Agent", StringComparison.OrdinalIgnoreCase))
						{
							if(!HttpHeaderValidator.IsValidUserAgent(kv.Value))
							{
								logger?.LogWarning("Invalid default User-Agent header - skipping (RFC 7231 violation)");
								continue;
							}
							client.DefaultRequestHeaders.Remove("User-Agent");
							client.DefaultRequestHeaders.UserAgent.Clear();
							foreach(ProductInfoHeaderValue token in PooledHttpClient.ParseUserAgentTokens(kv.Value))
							{
								client.DefaultRequestHeaders.UserAgent.Add(token);
							}
						}
						else
						{
							client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
						}
					}
				}
			})
			.ConfigurePrimaryHttpMessageHandler(sp =>
			{
				PooledHttpClientOptions opts = sp.GetRequiredService<IOptionsMonitor<PooledHttpClientOptions>>().Get(clientName);
				SocketsHttpHandler handler = new SocketsHttpHandler
				{
					AutomaticDecompression = opts.AutomaticDecompression,
					PooledConnectionLifetime = opts.PooledConnectionLifetime,
					MaxConnectionsPerServer = opts.MaxConnectionsPerServer
				};

				// First, apply callback handlers from options (configured in ServiceConfiguration)
				if(opts.ConnectCallback != null)
				{
					handler.ConnectCallback = opts.ConnectCallback;
				}

				if(opts.PlaintextStreamFilter != null)
				{
					handler.PlaintextStreamFilter = opts.PlaintextStreamFilter;
				}

				if(opts.ServerCertificateCustomValidationCallback != null)
				{
					handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
					{
						// Extract HttpRequestMessage from sender if available
						HttpRequestMessage? request = null;
						if(sender is HttpRequestMessage msg)
						{
							request = msg;
						}

						return opts.ServerCertificateCustomValidationCallback(request!, certificate as X509Certificate2, chain, sslPolicyErrors);
					};
				}

				if(opts.LocalCertificateSelectionCallback != null)
				{
					handler.SslOptions.LocalCertificateSelectionCallback = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
					{
						// Extract HttpRequestMessage from sender if available
						HttpRequestMessage? request = null;
						if(sender is HttpRequestMessage msg)
						{
							request = msg;
						}

						X509Certificate2Collection? certCollection = localCertificates as X509Certificate2Collection;
						X509Certificate2? result = opts.LocalCertificateSelectionCallback(request!, certCollection, acceptableIssuers);
						return result!;
					};
				}

				// Then, override with runtime-registered callbacks from CallbackRegistry (takes precedence)
				SocketCallbackHandlers? registeredHandlers = CallbackRegistry.GetHandlers(clientName);

				// DEBUG: Log callback registration status
				ILogger? debugLogger = sp.GetService<ILogger<PooledHttpClient>>();
				debugLogger?.LogDebug("Handler configuration for client '{ClientName}': Registered={HasHandlers}, ConnectCallback={HasConnect}, PlaintextFilter={HasFilter}, ServerCert={HasServerCert}, LocalCert={HasLocalCert}",
					clientName,
					registeredHandlers != null,
					registeredHandlers?.ConnectCallback != null,
					registeredHandlers?.PlaintextStreamFilter != null,
					registeredHandlers?.ServerCertificateCustomValidationCallback != null,
					registeredHandlers?.LocalCertificateSelectionCallback != null);

				if(registeredHandlers != null)
				{
					if(registeredHandlers.ConnectCallback != null)
					{
						debugLogger?.LogInformation("Applying ConnectCallback for client '{ClientName}'", clientName);
						handler.ConnectCallback = registeredHandlers.ConnectCallback;
					}

					if(registeredHandlers.PlaintextStreamFilter != null)
					{
						debugLogger?.LogInformation("Applying PlaintextStreamFilter for client '{ClientName}'", clientName);
						handler.PlaintextStreamFilter = registeredHandlers.PlaintextStreamFilter;
					}

					if(registeredHandlers.ServerCertificateCustomValidationCallback != null)
					{
						debugLogger?.LogInformation("Applying ServerCertificateCustomValidationCallback for client '{ClientName}'", clientName);
						handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
						{
							HttpRequestMessage? request = sender as HttpRequestMessage;
							return registeredHandlers.ServerCertificateCustomValidationCallback(request!, certificate as X509Certificate2, chain, sslPolicyErrors);
						};
					}

					if(registeredHandlers.LocalCertificateSelectionCallback != null)
					{
						debugLogger?.LogInformation("Applying LocalCertificateSelectionCallback for client '{ClientName}'", clientName);
						handler.SslOptions.LocalCertificateSelectionCallback = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
						{
							HttpRequestMessage? request = sender as HttpRequestMessage;
							X509Certificate2Collection? certCollection = localCertificates as X509Certificate2Collection;
							X509Certificate2? result = registeredHandlers.LocalCertificateSelectionCallback(request!, certCollection, acceptableIssuers);
							return result!;
						};
					}
				}
				else
				{
					debugLogger?.LogDebug("No registered callbacks found for client '{ClientName}'", clientName);
				}

				configureHandler?.Invoke(handler);
				return handler;
			});

			// Add HTTP version handler if a non-default version is specified
			builder.ConfigureHttpClient((sp, client) =>
			{
				PooledHttpClientOptions opts = sp.GetRequiredService<IOptionsMonitor<PooledHttpClientOptions>>().Get(clientName);
				if(opts.DefaultRequestVersion != new Version(1, 1))
				{
					// HttpVersionHandler will be added as a delegating handler
				}
			});

			// Register HttpVersionHandler to set HTTP version on all requests
			builder.AddHttpMessageHandler(sp =>
			{
				PooledHttpClientOptions opts = sp.GetRequiredService<IOptionsMonitor<PooledHttpClientOptions>>().Get(clientName);
				return new HttpVersionHandler(opts.DefaultRequestVersion);
			});

			services.AddSingleton<IPooledHttpClient>(sp => sp.GetRequiredService<INamedPooledHttpClientProvider>().GetClient(clientName));
			return services;
		}
	}
}