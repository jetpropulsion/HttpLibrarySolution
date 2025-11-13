using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	/// <summary>
	/// DelegatingHandler that sets the default HTTP version for all requests.
	/// Allows per-client HTTP version configuration via DefaultRequestVersion.
	/// </summary>
	public sealed class HttpVersionHandler : DelegatingHandler
	{
		readonly Version defaultVersion;

		/// <summary>
		/// Creates a new HttpVersionHandler with the specified default HTTP version.
		/// </summary>
		/// <param name="defaultVersion">The HTTP version to use for requests (e.g., 1.1, 2.0, 3.0)</param>
		public HttpVersionHandler(Version defaultVersion)
		{
			this.defaultVersion = defaultVersion ?? throw new ArgumentNullException(nameof(defaultVersion));
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// Only set version if not already explicitly set on the request
			// Default is HTTP/1.1, so if request uses default, apply our configured version
			if(request.Version == new Version(1, 1) || request.Version == null)
			{
				request.Version = defaultVersion;
			}

			return base.SendAsync(request, cancellationToken);
		}
	}
}