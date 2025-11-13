using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	/// <summary>
	/// Interface for the DI-friendly typed client.
	/// Provides convenience methods (string/byte[] and raw HttpResponseMessage) for common HTTP verbs.
	/// </summary>
	public interface IPooledHttpClient
	{
		// logical name for this pooled client instance (may be null)
		string? Name { get; }

		PooledHttpClientMetrics Metrics { get; }

		// Progress callback - invoked during HTTP operations to report progress
		Action<HttpProgressInfo>? ProgressCallback { get; set; }

		// Redirect callback - invoked when a redirect is encountered (when AutoRedirect is disabled)
		Func<HttpRedirectInfo, RedirectAction>? RedirectCallback { get; set; }

		// Maximum number of redirections allowed (both automatic and manual)
		int MaxRedirections { get; }

		// Custom request headers management
		/// <summary>
		/// Adds a custom header that will be included in all requests made by this client instance.
		/// If the header already exists, its value will be updated.
		/// </summary>
		/// <param name="name">Header name (validated per RFC 7230)</param>
		/// <param name="value">Header value (validated per RFC 7230)</param>
		void AddRequestHeader(string name, string value);

		/// <summary>
		/// Removes a custom header that was previously added via AddRequestHeader.
		/// </summary>
		/// <param name="name">Header name to remove</param>
		/// <returns>True if the header was found and removed, false otherwise</returns>
		bool RemoveRequestHeader(string name);

		/// <summary>
		/// Clears all custom headers that were added via AddRequestHeader.
		/// </summary>
		void ClearRequestHeaders();

		// GET
		Task<string> GetStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<byte[]> GetBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<HttpResponseMessage> GetAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// POST
		Task<string> PostStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<byte[]> PostBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// PUT
		Task<string> PutStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<byte[]> PutBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// DELETE
		Task<string> DeleteStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<byte[]> DeleteBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<HttpResponseMessage> DeleteAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// PATCH
		Task<string> PatchStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<byte[]> PatchBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<HttpResponseMessage> PatchAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// HEAD
		Task<HttpResponseMessage> HeadAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// OPTIONS
		Task<string> OptionsStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<byte[]> OptionsBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<HttpResponseMessage> OptionsAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// TRACE
		Task<string> TraceStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<byte[]> TraceBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<HttpResponseMessage> TraceAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// CONNECT
		Task<HttpResponseMessage> ConnectAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);

		// Generic send
		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

		// Raw send without EnsureSuccessStatusCode
		Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

		// Convenience overloads for string/byte content
		Task<string> PostStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<string> PutStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
		Task<string> PatchStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default);
	}
}