using System;
using System.Net.Http;
using System.Threading;

namespace HttpLibrary
{
	/// <summary>
	/// Information about an HTTP redirect
	/// </summary>
	public sealed class HttpRedirectInfo
	{
		/// <summary>
		/// The client name handling the redirect
		/// </summary>
		public string ClientName { get; init; }

		/// <summary>
		/// Original URL that was requested
		/// </summary>
		public string OriginalUrl { get; init; }

		/// <summary>
		/// New URL to redirect to
		/// </summary>
		public string RedirectUrl { get; init; }

		/// <summary>
		/// HTTP status code that caused the redirect (301, 302, 303, 307, 308)
		/// </summary>
		public int StatusCode { get; init; }

		/// <summary>
		/// Number of redirects so far in this chain
		/// </summary>
		public int RedirectCount { get; init; }

		/// <summary>
		/// HTTP method of the original request
		/// </summary>
		public HttpMethod Method { get; init; }

		/// <summary>
		/// Timestamp when redirect was encountered
		/// </summary>
		public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

		/// <summary>
		/// Cancellation token that can be used to signal cancellation from the redirect callback.
		/// The callback can check if cancellation has been requested or throw OperationCanceledException.
		/// </summary>
		public CancellationToken CancellationToken { get; init; }

		public HttpRedirectInfo(string clientName, string originalUrl, string redirectUrl, int statusCode, int redirectCount, HttpMethod method, CancellationToken cancellationToken = default)
		{
			ClientName = clientName ?? throw new ArgumentNullException(nameof(clientName));
			OriginalUrl = originalUrl ?? throw new ArgumentNullException(nameof(originalUrl));
			RedirectUrl = redirectUrl ?? throw new ArgumentNullException(nameof(redirectUrl));
			StatusCode = statusCode;
			RedirectCount = redirectCount;
			Method = method ?? throw new ArgumentNullException(nameof(method));
			CancellationToken = cancellationToken;
		}
	}
}