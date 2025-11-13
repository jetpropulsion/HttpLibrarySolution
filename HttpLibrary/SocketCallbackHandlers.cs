using System;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	/// <summary>
	/// Container for SocketHttpHandler callback handlers that can be configured at runtime.
	/// Allows users to customize HTTP connection behavior without modifying library code.
	/// </summary>
	public sealed class SocketCallbackHandlers
	{
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

		/// <summary>
		/// Indicates whether the PlaintextStreamFilter has been composed from multiple registrations.
		/// This is used for testing and diagnostics only.
		/// </summary>
		public bool PlaintextFilterIsComposed { get; internal set; }
	}
}