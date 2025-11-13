using System;
using System.Collections.Generic;

namespace HttpLibrary
{
	/// <summary>
	/// Unified HTTP client configuration that supports both defaults (from defaults.json) and per-client overrides (from clients.json)
	/// This class first loads default values from defaults.json, then patches them with client-specific values from clients.json
	/// </summary>
	public sealed class HttpClientConfig : IVersionedConfiguration
	{
		/// <summary>
		/// Configuration version as integer
		/// </summary>
		public int ConfigVersion { get; set; } = 1;

		/// <summary>
		/// Client name (only used for per-client configs from clients.json, null for defaults)
		/// </summary>
		public string? Name { get; set; }

		/// <summary>
		/// Base URI (only used for per-client configs from clients.json, null for defaults)
		/// </summary>
		public string? Uri { get; set; }

		/// <summary>
		/// Default HTTP version for requests (e.g., "1.1", "2.0", "3.0")
		/// </summary>
		public Version? DefaultRequestVersion { get; set; } = new Version(2, 0);

		/// <summary>
		/// Default request headers (merged: defaults + per-client overrides)
		/// </summary>
		public Dictionary<string, string>? DefaultRequestHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[ "Accept" ] = "text/html"
		};

		/// <summary>
		/// Decompression methods (e.g., "All", "GZip", "Deflate", "Brotli")
		/// </summary>
		public string? Decompression { get; set; } = "All";

		/// <summary>
		/// Maximum lifetime of pooled connections
		/// </summary>
		public TimeSpan? PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(2);

		/// <summary>
		/// Maximum number of concurrent connections per server
		/// </summary>
		public int? MaxConnectionsPerServer { get; set; } = int.MaxValue;

		/// <summary>
		/// Request timeout duration
		/// </summary>
		public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(30);

		/// <summary>
		/// Connection establishment timeout
		/// </summary>
		public TimeSpan? ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Timeout for 100-Continue response
		/// </summary>
		public TimeSpan? Expect100ContinueTimeout { get; set; } = TimeSpan.FromSeconds(1);

		/// <summary>
		/// Delay between keep-alive pings
		/// </summary>
		public TimeSpan? KeepAlivePingDelay { get; set; } = TimeSpan.FromSeconds(15);

		/// <summary>
		/// Timeout for keep-alive ping responses
		/// </summary>
		public TimeSpan? KeepAlivePingTimeout { get; set; } = TimeSpan.FromSeconds(5);

		/// <summary>
		/// Idle timeout for pooled connections
		/// </summary>
		public TimeSpan? PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(1);

		/// <summary>
		/// Initial HTTP/2 stream window size in bytes
		/// </summary>
		public int? InitialHttp2StreamWindowSize { get; set; } = 256 * 1024;

		/// <summary>
		/// Enable multiple HTTP/2 connections to the same server
		/// </summary>
		public bool? EnableMultipleHttp2Connections { get; set; } = false;

		/// <summary>
		/// Enable multiple HTTP/3 connections to the same server
		/// </summary>
		public bool? EnableMultipleHttp3Connections { get; set; } = false;

		/// <summary>
		/// Enable automatic redirect following. When false, redirects are intercepted and passed to RedirectCallback for manual handling.
		/// </summary>
		public bool? AllowAutoRedirect { get; set; } = false;

		/// <summary>
		/// Maximum number of HTTP redirects to follow
		/// </summary>
		public int? MaxRedirections { get; set; } = 10;

		/// <summary>
		/// Enable cookie handling
		/// </summary>
		public bool? UseCookies { get; set; } = false;

		/// <summary>
		/// Enable proxy usage
		/// </summary>
		public bool? UseProxy { get; set; } = false;

		/// <summary>
		/// Use system proxy settings (only for per-client configs)
		/// </summary>
		public bool? UseSystemProxy { get; set; }

		/// <summary>
		/// HTTP proxy URL
		/// </summary>
		public string? HttpProxy { get; set; }

		/// <summary>
		/// HTTPS proxy URL
		/// </summary>
		public string? HttpsProxy { get; set; }

		/// <summary>
		/// Proxy authentication username
		/// </summary>
		public string? ProxyUsername { get; set; }

		/// <summary>
		/// Proxy authentication password
		/// </summary>
		public string? ProxyPassword { get; set; }

		/// <summary>
		/// List of hosts to bypass proxy
		/// </summary>
		public List<string>? ProxyBypassList { get; set; }

		/// <summary>
		/// Enable persistent cookie storage
		/// </summary>
		public bool? CookiePersistenceEnabled { get; set; } = false;

		/// <summary>
		/// Disable SSL certificate validation (INSECURE - use only for testing/development)
		/// Default: false (secure - validates certificates)
		/// </summary>
		public bool? DisableSslValidation { get; set; } = false;

		/// <summary>
		/// Path to PFX/PKCS12 file containing client certificate and private key (cross-platform compatible)
		/// </summary>
		public string? ClientCertificatePath { get; set; }

		/// <summary>
		/// Password to decrypt the PFX file
		/// </summary>
		public string? ClientCertificatePassword { get; set; }

		/// <summary>
		/// Response drain timeout - time to wait for the server to send remaining response body after cancellation
		/// Default: 2 seconds
		/// </summary>
		public TimeSpan? ResponseDrainTimeout { get; set; } = TimeSpan.FromSeconds(2);

		/// <summary>
		/// Keep-alive ping policy (values: "Always", "WithActiveRequests")
		/// Default: "Always"
		/// </summary>
		public string? KeepAlivePingPolicy { get; set; } = "Always";

		/// <summary>
		/// Maximum length of response headers in bytes
		/// Default: 65536 (64 KB)
		/// </summary>
		public int? MaxResponseHeadersLength { get; set; } = 65536;

		/// <summary>
		/// Maximum size to drain from responses before connection reuse in bytes
		/// Default: 1048576 (1 MB)
		/// </summary>
		public int? MaxResponseDrainSize { get; set; } = 1048576;

		/// <summary>
		/// Enable pre-authentication (send credentials without waiting for 401 challenge)
		/// Default: false
		/// </summary>
		public bool? PreAuthenticate { get; set; } = false;

		/// <summary>
		/// Enable automatic handling of 100-Continue responses
		/// Default: true
		/// </summary>
		public bool? Expect100ContinueEnabled { get; set; } = true;

		/// <summary>
		/// Gets the configuration version as an integer
		/// </summary>
		public int GetVersion() => ConfigVersion;

		/// <summary>
		/// Gets the option definitions for HttpClientConfig version 1
		/// </summary>
		public Dictionary<string, ConfigOptionDefinition> GetOptionDefinitions()
		{
			Dictionary<string, ConfigOptionDefinition> definitions = new Dictionary<string, ConfigOptionDefinition>(StringComparer.OrdinalIgnoreCase)
			{
				[ "ConfigVersion" ] = new ConfigOptionDefinition("ConfigVersion", typeof(int), true)
				{
					MinValue = 1,
					MaxValue = 1,
					DefaultValue = 1,
					Description = "Configuration file version"
				},
				[ "Name" ] = new ConfigOptionDefinition("Name", typeof(string), false)
				{
					DefaultValue = null,
					Description = "Unique name for this HTTP client (clients.json only)"
				},
				[ "Uri" ] = new ConfigOptionDefinition("Uri", typeof(string), false)
				{
					DefaultValue = null,
					Description = "Base URI for this HTTP client (clients.json only)"
				},
				[ "DefaultRequestVersion" ] = new ConfigOptionDefinition("DefaultRequestVersion", typeof(Version), false)
				{
					DefaultValue = new Version(2, 0),
					Description = "Default HTTP version for requests"
				},
				[ "DefaultRequestHeaders" ] = new ConfigOptionDefinition("DefaultRequestHeaders", typeof(Dictionary<string, string>), false)
				{
					DefaultValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [ "Accept" ] = "text/html" },
					Description = "Default headers for all HTTP requests"
				},
				[ "Decompression" ] = new ConfigOptionDefinition("Decompression", typeof(string), false)
				{
					DefaultValue = "All",
					Description = "Decompression methods (e.g., 'All', 'GZip', 'Deflate', 'Brotli')"
				},
				[ "PooledConnectionLifetime" ] = new ConfigOptionDefinition("PooledConnectionLifetime", typeof(TimeSpan?), false)
				{
					DefaultValue = TimeSpan.FromMinutes(2),
					Description = "Maximum lifetime of pooled connections"
				},
				[ "MaxConnectionsPerServer" ] = new ConfigOptionDefinition("MaxConnectionsPerServer", typeof(int?), false)
				{
					MinValue = 1,
					DefaultValue = int.MaxValue,
					Description = "Maximum number of concurrent connections per server"
				},
				[ "Timeout" ] = new ConfigOptionDefinition("Timeout", typeof(TimeSpan?), false)
				{
					DefaultValue = TimeSpan.FromSeconds(30),
					Description = "Request timeout duration"
				},
				[ "ConnectTimeout" ] = new ConfigOptionDefinition("ConnectTimeout", typeof(TimeSpan?), false)
				{
					DefaultValue = TimeSpan.FromSeconds(10),
					Description = "Connection establishment timeout"
				},
				[ "Expect100ContinueTimeout" ] = new ConfigOptionDefinition("Expect100ContinueTimeout", typeof(TimeSpan?), false)
				{
					DefaultValue = TimeSpan.FromSeconds(1),
					Description = "Timeout for 100-Continue response"
				},
				[ "KeepAlivePingDelay" ] = new ConfigOptionDefinition("KeepAlivePingDelay", typeof(TimeSpan?), false)
				{
					DefaultValue = TimeSpan.FromSeconds(15),
					Description = "Delay between keep-alive pings"
				},
				[ "KeepAlivePingTimeout" ] = new ConfigOptionDefinition("KeepAlivePingTimeout", typeof(TimeSpan?), false)
				{
					DefaultValue = TimeSpan.FromSeconds(5),
					Description = "Timeout for keep-alive ping responses"
				},
				[ "PooledConnectionIdleTimeout" ] = new ConfigOptionDefinition("PooledConnectionIdleTimeout", typeof(TimeSpan?), false)
				{
					DefaultValue = TimeSpan.FromMinutes(1),
					Description = "Idle timeout for pooled connections"
				},
				[ "InitialHttp2StreamWindowSize" ] = new ConfigOptionDefinition("InitialHttp2StreamWindowSize", typeof(int?), false)
				{
					MinValue = 0,
					DefaultValue = 256 * 1024,
					Description = "Initial HTTP/2 stream window size in bytes"
				},
				[ "EnableMultipleHttp2Connections" ] = new ConfigOptionDefinition("EnableMultipleHttp2Connections", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Enable multiple HTTP/2 connections to the same server"
				},
				[ "EnableMultipleHttp3Connections" ] = new ConfigOptionDefinition("EnableMultipleHttp3Connections", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Enable multiple HTTP/3 connections to the same server"
				},
				[ "AllowAutoRedirect" ] = new ConfigOptionDefinition("AllowAutoRedirect", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Enable automatic following of HTTP redirects (when false, enables redirect callback)"
				},
				[ "MaxRedirections" ] = new ConfigOptionDefinition("MaxRedirections", typeof(int?), false)
				{
					MinValue = 0,
					MaxValue = 50,
					DefaultValue = 10,
					Description = "Maximum number of HTTP redirects to follow"
				},
				[ "UseCookies" ] = new ConfigOptionDefinition("UseCookies", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Enable cookie handling"
				},
				[ "UseProxy" ] = new ConfigOptionDefinition("UseProxy", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Enable proxy usage"
				},
				[ "UseSystemProxy" ] = new ConfigOptionDefinition("UseSystemProxy", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Use system proxy settings (clients.json only)"
				},
				[ "HttpProxy" ] = new ConfigOptionDefinition("HttpProxy", typeof(string), false)
				{
					DefaultValue = null,
					Description = "HTTP proxy URL"
				},
				[ "HttpsProxy" ] = new ConfigOptionDefinition("HttpsProxy", typeof(string), false)
				{
					DefaultValue = null,
					Description = "HTTPS proxy URL"
				},
				[ "ProxyUsername" ] = new ConfigOptionDefinition("ProxyUsername", typeof(string), false)
				{
					DefaultValue = null,
					Description = "Proxy authentication username"
				},
				[ "ProxyPassword" ] = new ConfigOptionDefinition("ProxyPassword", typeof(string), false)
				{
					DefaultValue = null,
					Description = "Proxy authentication password"
				},
				[ "ProxyBypassList" ] = new ConfigOptionDefinition("ProxyBypassList", typeof(List<string>), false)
				{
					DefaultValue = null,
					Description = "List of hosts to bypass proxy"
				},
				[ "CookiePersistenceEnabled" ] = new ConfigOptionDefinition("CookiePersistenceEnabled", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Enable persistent cookie storage"
				},
				[ "DisableSslValidation" ] = new ConfigOptionDefinition("DisableSslValidation", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Disable SSL certificate validation (INSECURE - development only)"
				},
				[ "ClientCertificatePath" ] = new ConfigOptionDefinition("ClientCertificatePath", typeof(string), false)
				{
					DefaultValue = null,
					Description = "Path to client certificate file (PFX/PKCS12)"
				},
				[ "ClientCertificatePassword" ] = new ConfigOptionDefinition("ClientCertificatePassword", typeof(string), false)
				{
					DefaultValue = null,
					Description = "Password for client certificate file"
				},
				[ "ResponseDrainTimeout" ] = new ConfigOptionDefinition("ResponseDrainTimeout", typeof(TimeSpan?), false)
				{
					DefaultValue = TimeSpan.FromSeconds(2),
					Description = "Time to wait for server to send remaining response body after cancellation"
				},
				[ "KeepAlivePingPolicy" ] = new ConfigOptionDefinition("KeepAlivePingPolicy", typeof(string), false)
				{
					DefaultValue = "Always",
					Description = "Keep-alive ping policy (Always, WithActiveRequests)"
				},
				[ "MaxResponseHeadersLength" ] = new ConfigOptionDefinition("MaxResponseHeadersLength", typeof(int?), false)
				{
					MinValue = 1024,
					DefaultValue = 65536,
					Description = "Maximum length of response headers in bytes"
				},
				[ "MaxResponseDrainSize" ] = new ConfigOptionDefinition("MaxResponseDrainSize", typeof(int?), false)
				{
					MinValue = 0,
					DefaultValue = 1048576,
					Description = "Maximum size to drain from responses before connection reuse"
				},
				[ "PreAuthenticate" ] = new ConfigOptionDefinition("PreAuthenticate", typeof(bool?), false)
				{
					DefaultValue = false,
					Description = "Enable pre-authentication (send credentials without 401 challenge)"
				},
				[ "Expect100ContinueEnabled" ] = new ConfigOptionDefinition("Expect100ContinueEnabled", typeof(bool?), false)
				{
					DefaultValue = true,
					Description = "Enable automatic handling of 100-Continue responses"
				}
			};

			return definitions;
		}

		/// <summary>
		/// Validates the configuration against option definitions
		/// </summary>
		public bool Validate()
		{
			// Validate ConfigVersion
			if(ConfigVersion < 1)
			{
				return false;
			}

			// For client configs (from clients.json), Name and Uri are mandatory
			if(!string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(Uri))
			{
				// If either Name or Uri is present, both must be present and valid
				if(string.IsNullOrWhiteSpace(Name))
				{
					return false;
				}

				if(string.IsNullOrWhiteSpace(Uri))
				{
					return false;
				}

				// Validate URI format
				if(!System.Uri.TryCreate(Uri, UriKind.Absolute, out _))
				{
					return false;
				}
			}

			// Validate DefaultRequestHeaders (if present, must not be null for defaults)
			// For defaults.json, this is mandatory; for clients.json, it's optional
			if(string.IsNullOrWhiteSpace(Name) && DefaultRequestHeaders is null)
			{
				// This is a default config and DefaultRequestHeaders is mandatory
				return false;
			}

			// Validate MaxConnectionsPerServer (if present, must be > 0)
			if(MaxConnectionsPerServer.HasValue && MaxConnectionsPerServer.Value <= 0)
			{
				return false;
			}

			// Validate MaxRedirections if present
			if(MaxRedirections.HasValue)
			{
				Dictionary<string, ConfigOptionDefinition> definitions = GetOptionDefinitions();
				ConfigOptionDefinition maxRedirDef = definitions[ "MaxRedirections" ];
				if(!maxRedirDef.ValidateValue(MaxRedirections.Value))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Creates a copy of this config with values patched from another config
		/// Null values in the patch are ignored (keeping the original value)
		/// </summary>
		public HttpClientConfig PatchFrom(HttpClientConfig patch)
		{
			HttpClientConfig result = new HttpClientConfig
			{
				ConfigVersion = patch.ConfigVersion > 0 ? patch.ConfigVersion : ConfigVersion,
				Name = patch.Name ?? Name,
				Uri = patch.Uri ?? Uri,
				DefaultRequestVersion = patch.DefaultRequestVersion ?? DefaultRequestVersion,
				Decompression = patch.Decompression ?? Decompression,
				PooledConnectionLifetime = patch.PooledConnectionLifetime ?? PooledConnectionLifetime,
				MaxConnectionsPerServer = patch.MaxConnectionsPerServer ?? MaxConnectionsPerServer,
				Timeout = patch.Timeout ?? Timeout,
				ConnectTimeout = patch.ConnectTimeout ?? ConnectTimeout,
				Expect100ContinueTimeout = patch.Expect100ContinueTimeout ?? Expect100ContinueTimeout,
				KeepAlivePingDelay = patch.KeepAlivePingDelay ?? KeepAlivePingDelay,
				KeepAlivePingTimeout = patch.KeepAlivePingTimeout ?? KeepAlivePingTimeout,
				PooledConnectionIdleTimeout = patch.PooledConnectionIdleTimeout ?? PooledConnectionIdleTimeout,
				InitialHttp2StreamWindowSize = patch.InitialHttp2StreamWindowSize ?? InitialHttp2StreamWindowSize,
				EnableMultipleHttp2Connections = patch.EnableMultipleHttp2Connections ?? EnableMultipleHttp2Connections,
				EnableMultipleHttp3Connections = patch.EnableMultipleHttp3Connections ?? EnableMultipleHttp3Connections,
				AllowAutoRedirect = patch.AllowAutoRedirect ?? AllowAutoRedirect,
				MaxRedirections = patch.MaxRedirections ?? MaxRedirections,
				UseCookies = patch.UseCookies ?? UseCookies,
				UseProxy = patch.UseProxy ?? UseProxy,
				UseSystemProxy = patch.UseSystemProxy ?? UseSystemProxy,
				HttpProxy = patch.HttpProxy ?? HttpProxy,
				HttpsProxy = patch.HttpsProxy ?? HttpsProxy,
				ProxyUsername = patch.ProxyUsername ?? ProxyUsername,
				ProxyPassword = patch.ProxyPassword ?? ProxyPassword,
				CookiePersistenceEnabled = patch.CookiePersistenceEnabled ?? CookiePersistenceEnabled,
				DisableSslValidation = patch.DisableSslValidation ?? DisableSslValidation,
				ClientCertificatePath = patch.ClientCertificatePath ?? ClientCertificatePath,
				ClientCertificatePassword = patch.ClientCertificatePassword ?? ClientCertificatePassword,
				ResponseDrainTimeout = patch.ResponseDrainTimeout ?? ResponseDrainTimeout,
				KeepAlivePingPolicy = patch.KeepAlivePingPolicy ?? KeepAlivePingPolicy,
				MaxResponseHeadersLength = patch.MaxResponseHeadersLength ?? MaxResponseHeadersLength,
				MaxResponseDrainSize = patch.MaxResponseDrainSize ?? MaxResponseDrainSize,
				PreAuthenticate = patch.PreAuthenticate ?? PreAuthenticate,
				Expect100ContinueEnabled = patch.Expect100ContinueEnabled ?? Expect100ContinueEnabled
			};

			// Merge DefaultRequestHeaders (start with original, overlay with patch)
			if(DefaultRequestHeaders != null || patch.DefaultRequestHeaders != null)
			{
				result.DefaultRequestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

				if(DefaultRequestHeaders != null)
				{
					foreach(KeyValuePair<string, string> kvp in DefaultRequestHeaders)
					{
						result.DefaultRequestHeaders[ kvp.Key ] = kvp.Value;
					}
				}

				if(patch.DefaultRequestHeaders != null)
				{
					foreach(KeyValuePair<string, string> kvp in patch.DefaultRequestHeaders)
					{
						result.DefaultRequestHeaders[ kvp.Key ] = kvp.Value;
					}
				}
			}

			// Merge ProxyBypassList (use patch if provided, otherwise use original)
			if(patch.ProxyBypassList != null)
			{
				result.ProxyBypassList = new List<string>(patch.ProxyBypassList);
			}
			else if(ProxyBypassList != null)
			{
				result.ProxyBypassList = new List<string>(ProxyBypassList);
			}

			return result;
		}
	}
}