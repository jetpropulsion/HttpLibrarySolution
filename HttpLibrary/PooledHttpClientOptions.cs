using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	/// <summary>
	/// Options used when registering the typed pooled client via DI.
	/// These influence the HttpClient and the primary handler created by IHttpClientFactory.
	/// After registration changes require app restart or re-registration.
	/// </summary>
	public sealed class PooledHttpClientOptions
	{
		// optional logical name for the configured pooled client
		public string? Name { get; set; }

		public Uri? BaseAddress { get; set; }
		public Version DefaultRequestVersion { get; set; } = HttpVersion.Version20;
		public IDictionary<string, string> DefaultRequestHeaders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		public DecompressionMethods AutomaticDecompression { get; set; } = DecompressionMethods.All;
		public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);
		public int MaxConnectionsPerServer { get; set; } = int.MaxValue;
		public TimeSpan? Timeout { get; set; } = null;
		public int MaxRedirections { get; set; } = 10;

		/// <summary>
		/// Callback invoked to establish a custom socket connection.
		/// Allows complete control over DNS resolution, socket creation, and connection establishment.
		/// </summary>
		public Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>? ConnectCallback { get; set; }

		/// <summary>
		/// Callback invoked to filter or wrap the plaintext stream before it's used for HTTP communication.
		/// Useful for traffic monitoring, logging, or custom stream processing.
		/// </summary>
		public Func<SocketsHttpPlaintextStreamFilterContext, CancellationToken, ValueTask<Stream>>? PlaintextStreamFilter { get; set; }

		/// <summary>
		/// Callback invoked to validate the server's SSL/TLS certificate.
		/// Return true to accept the certificate, false to reject it.
		/// Useful for certificate pinning, custom validation logic, or development environments.
		/// </summary>
		public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback { get; set; }

		/// <summary>
		/// Callback invoked to select a client certificate for mutual TLS (mTLS) authentication.
		/// Return the certificate to use, or null if no certificate should be sent.
		/// </summary>
		public Func<HttpRequestMessage, X509Certificate2Collection?, string[], X509Certificate2?>? LocalCertificateSelectionCallback { get; set; }
	}
}