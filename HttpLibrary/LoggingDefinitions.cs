using Microsoft.Extensions.Logging;

using System;

namespace HttpLibrary
{
	internal static class LoggingDefinitions
	{
		private static readonly Action<ILogger, string, Exception?> _setBaseAddress =
		LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "SetBaseAddress"), "Set base address for cookie persistence: {BaseAddress}");

		private static readonly Action<ILogger, string, Exception?> _setCookieHeaderReceived =
		LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "SetCookieHeaderReceived"), "Set-Cookie header received: {SetCookie}");

		private static readonly Action<ILogger, int, Exception?> _totalSetCookieHeadersReceived =
		LoggerMessage.Define<int>(LogLevel.Debug, new EventId(3, "TotalSetCookieHeadersReceived"), "Total Set-Cookie headers received: {Count}");

		private static readonly Action<ILogger, string, Exception?> _noSetCookieHeadersReceived =
		LoggerMessage.Define<string>(LogLevel.Trace, new EventId(4, "NoSetCookieHeadersReceived"), "No Set-Cookie headers received from {Url}");

		private static readonly Action<ILogger, int, Exception?> _responseLength =
		LoggerMessage.Define<int>(LogLevel.Debug, new EventId(5, "ResponseLength"), "Response length: {Length} bytes");

		private static readonly Action<ILogger, Exception?> _cookiesSaved =
		LoggerMessage.Define(LogLevel.Debug, new EventId(6, "CookiesSaved"), "Cookies saved to cookies.json");

		private static readonly Action<ILogger, int, string, Exception?> _httpErrorStatus =
		LoggerMessage.Define<int, string>(LogLevel.Error, new EventId(7, "HttpErrorStatus"), "HTTP error: {StatusCode} ({StatusText})");

		private static readonly Action<ILogger, string, Exception?> _requestErrorMessage =
		LoggerMessage.Define<string>(LogLevel.Error, new EventId(8, "RequestErrorMessage"), "Request error: {Message}");

		private static readonly Action<ILogger, string, int, int, Exception?> _followingRedirect =
		LoggerMessage.Define<string, int, int>(LogLevel.Debug, new EventId(9, "FollowingRedirect"), "Following redirect to: {RedirectUrl} (redirect {Count}/{Max})");

		private static readonly Action<ILogger, string, Exception?> _redirectNoLocation =
		LoggerMessage.Define<string>(LogLevel.Warning, new EventId(10, "RedirectNoLocation"), "Redirect response {StatusCode} has no Location header");

		public static void SetBaseAddress(ILogger logger, string baseAddress)
		{
			_setBaseAddress(logger, baseAddress, null);
		}

		public static void SetCookieHeaderReceived(ILogger logger, string setCookie)
		{
			_setCookieHeaderReceived(logger, setCookie, null);
		}

		public static void TotalSetCookieHeadersReceived(ILogger logger, int count)
		{
			_totalSetCookieHeadersReceived(logger, count, null);
		}

		public static void NoSetCookieHeadersReceived(ILogger logger, string url)
		{
			_noSetCookieHeadersReceived(logger, url, null);
		}

		public static void ResponseLength(ILogger logger, int length)
		{
			_responseLength(logger, length, null);
		}

		public static void CookiesSaved(ILogger logger)
		{
			_cookiesSaved(logger, null);
		}

		public static void HttpErrorStatus(ILogger logger, int statusCode, string statusText)
		{
			_httpErrorStatus(logger, statusCode, statusText, null);
		}

		public static void RequestErrorMessage(ILogger logger, string message)
		{
			_requestErrorMessage(logger, message, null);
		}

		public static void FollowingRedirect(ILogger logger, string redirectUrl, int count, int max)
		{
			_followingRedirect(logger, redirectUrl, count, max, null);
		}

		public static void RedirectNoLocation(ILogger logger, string statusCodeText)
		{
			_redirectNoLocation(logger, statusCodeText, null);
		}
	}
}