using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace HttpLibrary
{
	/// <summary>
	/// Helper methods for cookie-related operations in HTTP requests.
	/// </summary>
	public static class CookieHelper
	{
		/// <summary>
		/// Extracts base address from URL and sets it in CookiePersistence for the client.
		/// </summary>
		/// <param name="url">The request URL</param>
		/// <param name="clientName">Name of the HTTP client</param>
		/// <param name="logger">Logger for diagnostic information</param>
		/// <returns>Base URI if successfully parsed, null otherwise</returns>
		public static Uri? ExtractAndSetCookieBaseAddress(
			string url,
			string clientName,
			ILogger logger)
		{
			if(string.IsNullOrWhiteSpace(url))
			{
				return null;
			}

			if(!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
			{
				return null;
			}

			string baseAddress = uri.IsDefaultPort
				? $"{uri.Scheme}://{uri.Host}"
				: $"{uri.Scheme}://{uri.Host}:{uri.Port}";

			// Use the static CookiePersistence helper to register base address
			CookiePersistence.SetBaseAddress(clientName, baseAddress);
			try
			{
				logger.LogInformation("Set base address for cookie persistence: {BaseAddress}", baseAddress);
			}
			catch { }

			return uri;
		}

		/// <summary>
		/// Processes Set-Cookie headers from HTTP response and adds them to CookiePersistence.
		/// </summary>
		/// <param name="response">HTTP response message</param>
		/// <param name="baseUri">Base URI for the request</param>
		/// <param name="clientName">Name of the HTTP client</param>
		/// <param name="logger">Logger for diagnostic information</param>
		public static void ProcessSetCookieHeaders(
			HttpResponseMessage response,
			Uri? baseUri,
			string clientName,
			ILogger logger)
		{
			if(response == null)
			{
				throw new ArgumentNullException(nameof(response));
			}

			if(!response.Headers.TryGetValues(Constants.HeaderSetCookie, out IEnumerable<string>? setCookieHeaders))
			{
				logger.LogDebug("No Set-Cookie headers received from {Url}", response.RequestMessage?.RequestUri?.ToString() ?? "<unknown>");
				return;
			}

			int cookieCount = 0;
			foreach(string setCookie in setCookieHeaders)
			{
				logger.LogInformation("Set-Cookie header received: {SetCookie}", setCookie);

				// Delegate parsing to CookiePersistence - handles domain/path and expiration
				try
				{
					CookiePersistence.AddCookieFromHeader(clientName, setCookie, baseUri ?? response.RequestMessage?.RequestUri ?? new Uri("about:blank"));
					cookieCount++;
				}
				catch(Exception ex)
				{
					logger.LogWarning(ex, "Failed to parse Set-Cookie header: {SetCookie}", setCookie);
				}
			}

			logger.LogDebug("Total Set-Cookie headers processed: {Count}", cookieCount);
		}
	}
}