using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	/// <summary>
	/// Configures socket callback handlers from application settings and registers them in CallbackRegistry.
	/// Supports per-client callback settings located in ApplicationConfiguration.Clients[clientName].
	/// </summary>
	public static class CallbackConfigurator
	{
		/// <summary>
		/// Configure callbacks for a client based on application configuration.
		/// This registers handlers in the global CallbackRegistry for the specified client name.
		/// It prefers per-client settings from config.Clients if present; otherwise falls back to global settings.
		/// </summary>
		public static void ConfigureFromApplicationSettings(string clientName, ApplicationConfiguration config, ILogger logger)
		{
			// If clientName is null/whitespace we treat it as an invalid entry and skip rather than throw.
			if(string.IsNullOrWhiteSpace(clientName))
			{
				logger?.LogWarning("ConfigureFromApplicationSettings called with empty clientName - skipping registration");
				return;
			}

			if(config == null)
			{
				throw new ArgumentNullException(nameof(config));
			}

			// Merge semantics: preserve any existing handlers for this client and augment them with configured callbacks
			SocketCallbackHandlers? existingHandlers = CallbackRegistry.GetHandlers(clientName);
			SocketCallbackHandlers handlers = existingHandlers ?? new SocketCallbackHandlers();

			// Determine per-client override if present
			ClientCallbackSettings? clientSettings = null;
			if(config.Clients != null && config.Clients.TryGetValue(clientName, out ClientCallbackSettings? cs))
			{
				clientSettings = cs;
			}

			// Certificate pinning
			bool pinningEnabled = clientSettings?.EnableCertificatePinning ?? config.EnableCertificatePinning;
			CertificatePinningConfig? pinConfig = clientSettings?.CertificatePinning ?? config.CertificatePinning;
			if(pinningEnabled && pinConfig != null)
			{
				string[] pinnedThumbprints = pinConfig.PinnedThumbprints?.ToArray() ?? Array.Empty<string>();
				logger.LogInformation("Registering certificate pinning for client '{ClientName}' with {Count} pinned thumbprints", clientName, pinnedThumbprints.Length);

				handlers.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
				{
					if(cert == null)
					{
						logger.LogWarning("No certificate provided");
						return false;
					}

					bool isPinned = pinnedThumbprints.Any(thumbprint => string.Equals(cert.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));

					if(!isPinned)
					{
						logger.LogWarning("Certificate not pinned: {Thumbprint}", cert.Thumbprint);
						return false;
					}

					return errors == SslPolicyErrors.None;
				};
			}

			// Custom DNS / Connect callback
			bool dnsEnabled = clientSettings?.EnableCustomDns ?? config.EnableCustomDns;
			SocketOptions? socketOpts = clientSettings?.CustomDns?.SocketOptions ?? config.CustomDns?.SocketOptions;
			if(dnsEnabled && socketOpts != null)
			{
				logger.LogInformation("Registering custom DNS resolution for client '{ClientName}'", clientName);

				handlers.ConnectCallback = async (context, cancellationToken) =>
				{
					logger.LogInformation("Custom connection to {Host}:{Port}", context.DnsEndPoint.Host, context.DnsEndPoint.Port);

					System.Net.IPAddress[] addresses = await System.Net.Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);

					System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp)
					{
						NoDelay = socketOpts.NoDelay,
						SendTimeout = socketOpts.SendTimeout,
						ReceiveTimeout = socketOpts.ReceiveTimeout,
						SendBufferSize = socketOpts.SendBufferSize,
						ReceiveBufferSize = socketOpts.ReceiveBufferSize
					};

					await socket.ConnectAsync(addresses[ 0 ], context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
					return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
				};
			}

			// Traffic monitoring
			bool trafficEnabled = clientSettings?.EnableTrafficMonitoring ?? config.EnableTrafficMonitoring;
			if(trafficEnabled)
			{
				logger.LogInformation("Registering traffic monitoring for client '{ClientName}'", clientName);
				handlers.PlaintextStreamFilter = (context, cancellationToken) =>
				{
					logger.LogInformation("Stream opened - Host: {Host}", context.InitialRequestMessage.RequestUri?.Host);
					return new System.Threading.Tasks.ValueTask<System.IO.Stream>(context.PlaintextStream);
				};
			}

			// Mutual TLS selection
			bool mtlsEnabled = clientSettings?.EnableMutualTls ?? config.EnableMutualTls;
			MutualTlsConfig? mtlsConfig = clientSettings?.MutualTls ?? config.MutualTls;
			if(mtlsEnabled && mtlsConfig != null)
			{
				logger.LogInformation("Registering mutual TLS for client '{ClientName}'", clientName);
				handlers.LocalCertificateSelectionCallback = (request, localCertificates, acceptableIssuers) =>
				{
					logger.LogInformation("Server requesting client certificate for {Host}", request?.RequestUri?.Host);

					try
					{
						using System.Security.Cryptography.X509Certificates.X509Store store = new System.Security.Cryptography.X509Certificates.X509Store(mtlsConfig.GetStoreName(), mtlsConfig.GetStoreLocation());
						store.Open(OpenFlags.ReadOnly);

						System.Security.Cryptography.X509Certificates.X509Certificate2Collection validCerts = store.Certificates.Find(System.Security.Cryptography.X509Certificates.X509FindType.FindBySubjectName, mtlsConfig.CertificateSubjectName, validOnly: true);

						if(validCerts.Count > 0)
						{
							logger.LogInformation("Using client certificate: {Subject}", validCerts[ 0 ].Subject);
							return validCerts[ 0 ];
						}
					}
					catch(Exception ex)
					{
						logger.LogWarning(ex, "Mutual TLS selection failed for client {Client}", clientName);
					}

					logger.LogWarning("No suitable client certificate found for {Client}", clientName);
					return null;
				};
			}

			// Finally register handlers (merge semantics are implicit - caller may call this multiple times to overwrite)
			CallbackRegistry.RegisterHandlers(clientName, handlers);
			logger.LogInformation("Registered callbacks for client '{ClientName}' from application configuration", clientName);
		}
	}
}