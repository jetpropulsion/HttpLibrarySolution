using Microsoft.Extensions.Logging;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary.Handlers
{
	/// <summary>
	/// Delegating handler that resolves requests using the alias:// scheme to configured base URLs.
	/// This handler is opt-in: it only rewrites requests whose URI scheme is exactly "alias".
	/// Expected form: alias://{alias}/{path}?{query}
	/// The alias-to-base mapping is read from ServiceConfiguration.AppConfig.Aliases.
	/// </summary>
	public sealed class AliasResolutionHandler : DelegatingHandler
	{
		readonly ILogger<AliasResolutionHandler> _logger;

		public AliasResolutionHandler(ILogger<AliasResolutionHandler> logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Try to resolve an alias URI to a final absolute URI using application configuration aliases.
		/// Returns true when resolution succeeded and outputs the resolved URI.
		/// This method does not modify the provided HttpRequestMessage; callers may set headers after resolution.
		/// </summary>
		public static bool TryResolveAlias(Uri aliasUri, out Uri? resolvedUri, out string? aliasName, out string? error)
		{
			resolvedUri = null;
			aliasName = null;
			error = null;

			if(aliasUri == null)
			{
				error = "aliasUri is null";
				return false;
			}

			if(!string.Equals(aliasUri.Scheme, "alias", StringComparison.OrdinalIgnoreCase))
			{
				error = "URI scheme is not 'alias'";
				return false;
			}

			aliasName = aliasUri.Host ?? string.Empty;
			if(string.IsNullOrWhiteSpace(aliasName))
			{
				error = "Alias URI does not contain an authority component (alias name).";
				return false;
			}

			var aliases = ServiceConfiguration.AppConfig?.Aliases;
			string? baseUrl = null;
			if(aliases != null)
			{
				aliases.TryGetValue(aliasName, out baseUrl);
			}
			// Fallback: if no alias mapping was present in application config, try registered client base addresses
			if(string.IsNullOrWhiteSpace(baseUrl))
			{
				try
				{
					if(ServiceConfiguration.RegisteredClientBaseAddresses != null && ServiceConfiguration.RegisteredClientBaseAddresses.TryGetValue(aliasName, out Uri? addr))
					{
						baseUrl = addr.ToString();
					}
				}
				catch { }
			}

			if(string.IsNullOrWhiteSpace(baseUrl))
			{
				// Try to match by host name in registered base addresses (e.g., alias 'google' -> base 'https://google.com')
				try
				{
					if(ServiceConfiguration.RegisteredClientBaseAddresses != null)
					{
						foreach(var kv in ServiceConfiguration.RegisteredClientBaseAddresses)
						{
							Uri? candidate = kv.Value;
							if(candidate != null)
							{
								string host = candidate.Host ?? string.Empty;
								if(string.Equals(host, aliasName, StringComparison.OrdinalIgnoreCase) || host.StartsWith(aliasName + ".", StringComparison.OrdinalIgnoreCase) || host.Contains(aliasName, StringComparison.OrdinalIgnoreCase))
								{
									baseUrl = candidate.ToString();
									break;
								}
							}
						}
					}
				}
				catch { }
			}

			if(string.IsNullOrWhiteSpace(baseUrl))
			{
				error = $"Unknown alias: {aliasName}";
				return false;
			}

			if(!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
			{
				error = $"Configured base URL for alias '{aliasName}' is not a valid absolute URI: {baseUrl}";
				return false;
			}

			string pathAndQuery = aliasUri.PathAndQuery ?? string.Empty;
			string fragment = aliasUri.Fragment ?? string.Empty;

			string relative = pathAndQuery;
			if(relative.StartsWith("/"))
			{
				relative = relative.Substring(1);
			}

			string baseStr = baseUri.ToString();
			if(!baseStr.EndsWith("/"))
				baseStr += "/";

			string combined = baseStr + relative;
			if(!string.IsNullOrEmpty(fragment))
				combined += fragment;

			try
			{
				resolvedUri = new Uri(combined);
				return true;
			}
			catch(Exception ex)
			{
				error = ex.Message;
				return false;
			}
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);

			if(request.RequestUri != null && string.Equals(request.RequestUri.Scheme, "alias", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string aliasNameLocal = request.RequestUri.Host ?? string.Empty;
					if(string.IsNullOrWhiteSpace(aliasNameLocal))
					{
						throw new HttpRequestException("Alias URI does not contain an authority component (alias name).");
					}

					var aliases = ServiceConfiguration.AppConfig?.Aliases;
					string? baseUrl = null;
					if(aliases != null)
					{
						aliases.TryGetValue(aliasNameLocal, out baseUrl);
					}
					// Fallback: if no alias mapping was present in application config, try registered client base addresses
					if(string.IsNullOrWhiteSpace(baseUrl))
					{
						try
						{
							if(ServiceConfiguration.RegisteredClientBaseAddresses != null && ServiceConfiguration.RegisteredClientBaseAddresses.TryGetValue(aliasNameLocal, out Uri? addr))
							{
								baseUrl = addr?.ToString();
							}
						}
						catch { }
					}

					if(string.IsNullOrWhiteSpace(baseUrl))
					{
						// Try to match by host name in registered base addresses (e.g., alias 'google' -> base 'https://google.com')
						try
						{
							if(ServiceConfiguration.RegisteredClientBaseAddresses != null)
							{
								foreach(var kv in ServiceConfiguration.RegisteredClientBaseAddresses)
								{
									Uri? candidate = kv.Value;
									if(candidate != null)
									{
										string host = candidate.Host ?? string.Empty;
										if(string.Equals(host, aliasNameLocal, StringComparison.OrdinalIgnoreCase) || host.StartsWith(aliasNameLocal + ".", StringComparison.OrdinalIgnoreCase) || host.Contains(aliasNameLocal, StringComparison.OrdinalIgnoreCase))
										{
											baseUrl = candidate.ToString();
											break;
										}
									}
								}
							}
						}
						catch { }
					}

					if(string.IsNullOrWhiteSpace(baseUrl))
					{
						_logger.LogWarning("Unknown alias '{Alias}' requested and no mapping found in application configuration.", aliasNameLocal);
						throw new HttpRequestException($"Unknown alias: {aliasNameLocal}");
					}

					if(!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
					{
						_logger.LogWarning("Configured base URL for alias '{Alias}' is not a valid absolute URI: {BaseUrl}", aliasNameLocal, baseUrl);
						throw new HttpRequestException($"Invalid base URL configured for alias: {aliasNameLocal}");
					}

					string pathAndQuery = request.RequestUri.PathAndQuery ?? string.Empty;
					string fragment = request.RequestUri.Fragment ?? string.Empty; // preserve fragment for diagnostics/tests

					string relative = pathAndQuery;
					if(relative.StartsWith("/"))
					{
						relative = relative.Substring(1);
					}

					string baseStr = baseUri.ToString();
					if(!baseStr.EndsWith("/"))
						baseStr += "/";

					string combined = baseStr + relative;
					if(!string.IsNullOrEmpty(fragment))
						combined += fragment;

					Uri finalUri = new Uri(combined);

					// Ensure Host header matches the resolved host so the server receives correct Host
					try
					{
						string hostHeader = finalUri.IsDefaultPort ? finalUri.Host : $"{finalUri.Host}:{finalUri.Port}";
						request.Headers.Host = hostHeader;
					}
					catch
					{
						// best-effort; ignore failures setting Host header
					}

					_logger.LogDebug("Resolved alias://{Alias}{Path} -> {Resolved}", aliasNameLocal, pathAndQuery + fragment, finalUri.ToString());
					request.RequestUri = finalUri;
				}
				catch(Exception ex)
				{
					throw new HttpRequestException("Failed to resolve alias URI", ex);
				}
			}

			return base.SendAsync(request, cancellationToken);
		}
	}
}