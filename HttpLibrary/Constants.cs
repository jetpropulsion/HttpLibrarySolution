using System;

namespace HttpLibrary
{
	/// <summary>
	/// Centralized configuration constants for the HttpLibrary.
	/// Contains file paths, buffer sizes, timeouts, and other runtime parameters.
	/// File path constants are generated from Directory.Build.props - see HttpLibraryConfig.Generated.cs
	/// </summary>
	public static partial class Constants
	{
		// Note: CookiesFile, ClientsConfigFile, and DefaultsConfigFile are defined in HttpLibraryConfig.Generated.cs
		// and are synchronized with Directory.Build.props

		#region File Paths

		/// <summary>
		/// Path pattern for log files (daily rolling).
		/// The date will be inserted at the position marked by {Date} or at the first hyphen followed by .log
		/// Example: "logs/app-{Date}.log" becomes "logs/app-20250109.log"
		/// </summary>
		public const string LogFilePathPattern = "logs/app-{Date}.log";

		/// <summary>
		/// Library-level configuration filename used to override default filenames for clients/defaults/cookies.
		/// </summary>
		public const string LibraryConfigurationFile = "HttpLibraryConfiguration.json";

		/// <summary>
		/// Application configuration filename used by consumers (default: application.json).
		/// Can be overridden via HttpLibraryConfiguration.json.
		/// </summary>
		public const string ApplicationConfigFile = "application.json";

		#endregion

		#region Buffer Sizes

		/// <summary>
		/// Default buffer size for streaming operations (80 KB).
		/// Used for file uploads, downloads, and stream processing.
		/// </summary>
		public const int StreamBufferSize = 81920;

		/// <summary>
		/// Default initial HTTP/2 stream window size (256 KB).
		/// </summary>
		public const int DefaultHttp2StreamWindowSize = 262144;

		/// <summary>
		/// Default progress event granularity in bytes (64 KB). Used to split large reads into smaller progress events.
		/// </summary>
		public const long DefaultProgressGranularityBytes = 64 * 1024;

		#endregion

		#region Timeout Defaults

		/// <summary>
		/// Default timeout for standard HTTP requests (30 seconds).
		/// </summary>
		public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

		/// <summary>
		/// Default timeout for binary operations including uploads and downloads (5 minutes).
		/// </summary>
		public static readonly TimeSpan DefaultBinaryOperationTimeout = TimeSpan.FromSeconds(300);

		/// <summary>
		/// Default pooled connection lifetime (2 minutes).
		/// </summary>
		public static readonly TimeSpan DefaultPooledConnectionLifetime = TimeSpan.FromMinutes(2);

		/// <summary>
		/// Default connect timeout (10 seconds).
		/// </summary>
		public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Default Expect100-Continue timeout (1 second).
		/// </summary>
		public static readonly TimeSpan DefaultExpect100ContinueTimeout = TimeSpan.FromSeconds(1);

		/// <summary>
		/// Default keep-alive ping delay (15 seconds).
		/// </summary>
		public static readonly TimeSpan DefaultKeepAlivePingDelay = TimeSpan.FromSeconds(15);

		/// <summary>
		/// Default keep-alive ping timeout (5 seconds).
		/// </summary>
		public static readonly TimeSpan DefaultKeepAlivePingTimeout = TimeSpan.FromSeconds(5);

		/// <summary>
		/// Default pooled connection idle timeout (1 minute).
		/// </summary>
		public static readonly TimeSpan DefaultPooledConnectionIdleTimeout = TimeSpan.FromMinutes(1);

		#endregion

		#region Client Defaults

		/// <summary>
		/// Default base URI for the default HTTP client when none is configured.
		/// </summary>
		public const string DefaultClientBaseUri = "https://localhost";

		/// <summary>
		/// Default Accept header value.
		/// </summary>
		public const string DefaultAcceptHeader = "text/html";

		/// <summary>
		/// Default maximum number of redirections allowed.
		/// </summary>
		public const int DefaultMaxRedirections = 10;

		/// <summary>
		/// Default client name used when no name is specified.
		/// </summary>
		public const string DefaultClientName = "default";

		#endregion

		#region RFC6265 Cookie Constants

		/// <summary>
		/// RFC6265 Section4.1.1: Separator characters that are invalid in cookie names.
		/// Includes: ( ) < > @ , ; : \ " / [ ] ? = { } SP HT
		/// </summary>
		public const string Rfc6265CookieSeparators = "()<>@,;:\u005C\u0022/[]?={} \t";

		#endregion

		#region RFC7230 HTTP Header Constants

		/// <summary>
		/// RFC7230 Section3.2: Delimiter characters that are invalid in HTTP header token names.
		/// Includes: ( ) < > @ , ; : \ " / [ ] ? = { } SP HT
		/// </summary>
		public const string Rfc7230HeaderDelimiters = "()<>@,;:\u005C\u0022/[]?={} \t";

		#endregion

		#region HTTP Header Names

		/// <summary>
		/// Set-Cookie HTTP response header name (RFC6265).
		/// </summary>
		public const string HeaderSetCookie = "Set-Cookie";

		/// <summary>
		/// Cookie HTTP request header name (RFC6265).
		/// </summary>
		public const string HeaderCookie = "Cookie";

		/// <summary>
		/// Accept HTTP request header name (RFC7231 Section5.3.2).
		/// </summary>
		public const string HeaderAccept = "Accept";

		/// <summary>
		/// User-Agent HTTP request header name (RFC7231 Section5.5.3).
		/// </summary>
		public const string HeaderUserAgent = "User-Agent";

		/// <summary>
		/// Host HTTP request header name (RFC7230 Section5.4).
		/// </summary>
		public const string HeaderHost = "Host";

		/// <summary>
		/// Location HTTP response header name (RFC7231).
		/// </summary>
		public const string HeaderLocation = "Location";

		/// <summary>
		/// Content-Length HTTP header name (RFC7230 Section3.3.2).
		/// </summary>
		public const string HeaderContentLength = "Content-Length";

		/// <summary>
		/// Content-Type HTTP header name (RFC7231 Section3.1.1.5).
		/// </summary>
		public const string HeaderContentType = "Content-Type";

		#endregion

		#region Media Types

		/// <summary>
		/// Binary octet stream media type.
		/// </summary>
		public const string MediaTypeOctetStream = "application/octet-stream";

		/// <summary>
		/// Plain text media type.
		/// </summary>
		public const string MediaTypePlainText = "text/plain";

		/// <summary>
		/// JSON media type.
		/// </summary>
		public const string MediaTypeJson = "application/json";

		/// <summary>
		/// XML media type.
		/// </summary>
		public const string MediaTypeXml = "application/xml";

		/// <summary>
		/// HTML media type.
		/// </summary>
		public const string MediaTypeHtml = "text/html";

		/// <summary>
		/// URL-encoded form data media type (RFC1866 Section8.2.1).
		/// </summary>
		public const string MediaTypeFormUrlEncoded = "application/x-www-form-urlencoded";

		/// <summary>
		/// Multipart form data media type (RFC7578).
		/// </summary>
		public const string MediaTypeFormData = "multipart/form-data";

		#endregion

		#region Encoding Constants

		/// <summary>
		/// UTF-8 charset name for Content-Type header (RFC3629).
		/// </summary>
		public const string CharsetUtf8 = "utf-8";

		/// <summary>
		/// Default text encoding: UTF-8 (RFC3629).
		/// This is the recommended encoding for HTTP text content per RFC7231.
		/// </summary>
		public static readonly System.Text.Encoding DefaultTextEncoding = System.Text.Encoding.UTF8;

		#endregion

		#region Log Messages

		/// <summary>
		/// Unknown status text placeholder.
		/// </summary>
		public const string UnknownStatusText = "Unknown";

		#endregion

		#region Cookie Persistence Constants

		/// <summary>
		/// File extension for temporary cookie files during atomic write operations.
		/// </summary>
		public const string TempFileExtension = ".tmp";

		/// <summary>
		/// URL scheme for HTTPS protocol.
		/// </summary>
		public const string HttpsScheme = "https://";

		/// <summary>
		/// URL scheme for HTTP protocol.
		/// </summary>
		public const string HttpScheme = "http://";

		/// <summary>
		/// Display text for session cookies (no expiration).
		/// </summary>
		public const string SessionCookieText = "Session";

		/// <summary>
		/// Display text for unset/null cookie attributes.
		/// </summary>
		public const string NotSetText = "(not set)";

		/// <summary>
		/// Display text for null values in logging.
		/// </summary>
		public const string NullText = "null";

		/// <summary>
		/// ISO8601 format specifier for DateTime serialization.
		/// ISO8601 format specifier for DateTime serialization.
		/// ISO8601 format specifier for DateTime serialization.
		/// </summary>
		public const string Iso8601FormatSpecifier = "O";

		/// <summary>
		/// Default path for cookies when not specified.
		/// </summary>
		public const string DefaultCookiePath = "/";

		/// <summary>
		/// SameSite attribute value: Strict.
		/// </summary>
		public const string SameSiteStrict = "Strict";

		/// <summary>
		/// SameSite attribute value: Lax.
		/// </summary>
		public const string SameSiteLax = "Lax";

		/// <summary>
		/// SameSite attribute value: None.
		/// </summary>
		public const string SameSiteNone = "None";

		/// <summary>
		/// Keep-alive ping policy: Always (string used in configuration).
		/// </summary>
		public const string KeepAlivePingPolicyAlways = "Always";

		/// <summary>
		/// Keep-alive ping policy: WithActiveRequests (string used in configuration).
		/// </summary>
		public const string KeepAlivePingPolicyWithActiveRequests = "WithActiveRequests";

		/// <summary>
		/// Initial contents for a newly created cookies file.
		/// </summary>
		public const string InitialCookiesFileContents = "{}\n";

		#endregion

		#region Common Log Templates

		/// <summary>
		/// Log message when defaults configuration file is created.
		/// </summary>
		public const string LogCreatedDefaultsConfig = "Created defaults configuration file at {Path}";

		/// <summary>
		/// Log message when clients configuration file is created.
		/// </summary>
		public const string LogCreatedClientsConfig = "Created clients configuration file at {Path}";

		/// <summary>
		/// Log message when an empty cookies file is created.
		/// </summary>
		public const string LogCreatedEmptyCookiesFile = "Created empty cookies file at {Path}";

		/// <summary>
		/// Log message when failing to load defaults.
		/// </summary>
		public const string LogFailedToLoadDefaults = "Failed to load defaults from {Path}";

		/// <summary>
		/// Log message when failing to load client configurations.
		/// </summary>
		public const string LogFailedToLoadClientConfigs = "Failed to load client configurations from {Path}";

		/// <summary>
		/// Error message when no valid client configs found.
		/// </summary>
		public const string ErrorNoValidClientConfigs = "No valid pooled client configurations found in {Path}";

		/// <summary>
		/// Log message for cookie housekeeping completion.
		/// </summary>
		public const string LogCookieHousekeepingCompleted = "Cookie housekeeping completed: removed {Count} orphaned client cookie store(s)";

		/// <summary>
		/// Log message for auto-registered default client.
		/// </summary>
		public const string LogAutoRegisteredClient = "Auto-registered named pooled client '{ClientName}' with default settings (CookiePersistenceEnabled={CookiePersist}, SslValidationDisabled={SslDisabled})";

		#endregion

		#region Cookie Attribute Names (RFC6265)
		#region Cookie Attribute Names (RFC6265)

		/// <summary>
		/// Cookie attribute name: domain (RFC6265 Section5.2.1).
		/// </summary>
		public const string CookieAttributeDomain = "domain";

		/// <summary>
		/// Cookie attribute name: path (RFC6265 Section5.2.4).
		/// </summary>
		public const string CookieAttributePath = "path";

		/// <summary>
		/// Cookie attribute name: expires (RFC6265 Section5.2.3).
		/// </summary>
		public const string CookieAttributeExpires = "expires";

		/// <summary>
		/// Cookie attribute name: max-age (RFC6265 Section5.2.2).
		/// </summary>
		public const string CookieAttributeMaxAge = "max-age";

		/// <summary>
		/// Cookie attribute name: secure (RFC6265 Section5.2.5).
		/// </summary>
		public const string CookieAttributeSecure = "secure";

		/// <summary>
		/// Cookie attribute name: httponly (RFC6265 Section5.2.6).
		/// </summary>
		public const string CookieAttributeHttpOnly = "httponly";

		/// <summary>
		/// Cookie attribute name: samesite (RFC6265bis).
		/// </summary>
		public const string CookieAttributeSameSite = "samesite";

		#endregion
		#endregion
	}
}