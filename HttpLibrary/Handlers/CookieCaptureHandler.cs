using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary.Handlers
{
	/// <summary>
	/// DelegatingHandler that captures Set-Cookie response headers and forwards them to the cookie persistence implementation.
	/// This ensures cookies set for arbitrary request hosts are recorded in the persisted store.
	/// </summary>
	internal sealed class CookieCaptureHandler : DelegatingHandler
	{
		private readonly ICookiePersistence _cookiePersistence;
		private readonly string _clientName;

		public CookieCaptureHandler(ICookiePersistence cookiePersistence, string clientName)
		{
			_cookiePersistence = cookiePersistence ?? throw new ArgumentNullException(nameof(cookiePersistence));
			_clientName = clientName ?? string.Empty;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			HttpResponseMessage? responseNullable = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			if(responseNullable is null)
			{
				throw new InvalidOperationException("DelegatingHandler returned null response");
			}

			HttpResponseMessage response = responseNullable;

			try
			{
				if(response.Headers != null && request?.RequestUri != null)
				{
					System.Net.CookieContainer? runtimeContainer = null;
					try
					{ runtimeContainer = _cookiePersistence.GetContainer(_clientName); }
					catch { /* best-effort */ }

					bool saved = false;
					foreach(var header in response.Headers)
					{
						if(string.Equals(header.Key, Constants.HeaderSetCookie, StringComparison.OrdinalIgnoreCase))
						{
							foreach(string rawVal in header.Value)
							{
								try
								{
									IEnumerable<string> parts = SplitSetCookieHeader(rawVal);
									foreach(string part in parts)
									{
										try
										{
											// Determine if Set-Cookie contains a Domain attribute; when present use that domain as baseUri for persistence
											Uri baseForCookie = request.RequestUri;
											string domainAttr = ExtractDomainAttribute(part);
											if(!string.IsNullOrWhiteSpace(domainAttr))
											{
												string normalized = domainAttr.Trim();
												if(normalized.StartsWith('.'))
													normalized = normalized.Substring(1);
												try
												{
													baseForCookie = new Uri($"{request.RequestUri.Scheme}://{normalized}/");
												}
												catch
												{
													baseForCookie = request.RequestUri; // fallback
												}
											}

											_cookiePersistence.AddCookieFromHeader(_clientName, part, baseForCookie);

											// Also try to set cookie into runtime container using the domain-based URI so cookies are available immediately
											if(runtimeContainer != null)
											{
												try
												{
													Uri setCookieUri = baseForCookie;
													runtimeContainer.SetCookies(setCookieUri, part);
												}
												catch
												{
													// ignore
												}
											}

											saved = true;
										}
										catch
										{
											// best-effort
										}
									}
								}
								catch
								{
									// best-effort
								}
							}
						}
					}

					// Best-effort: save after processing headers
					try
					{
						if(saved)
						{
							_cookiePersistence.SaveCookies();
						}
					}
					catch { }
				}
			}
			catch
			{
				// best-effort
			}

			return response;
		}

		// Splits a possibly-combined Set-Cookie header value into individual Set-Cookie strings.
		// Heuristic: split on commas that start a new cookie (i.e., the token following comma contains '=' before any ';').
		private static IEnumerable<string> SplitSetCookieHeader(string headerValue)
		{
			if(string.IsNullOrEmpty(headerValue))
			{
				yield break;
			}

			int len = headerValue.Length;
			int last = 0;
			for(int i = 0; i < len; i++)
			{
				char c = headerValue[ i ];
				if(c == ',')
				{
					// Look ahead to determine if this comma separates cookies
					int j = i + 1;
					// skip whitespace
					while(j < len && char.IsWhiteSpace(headerValue[ j ]))
						j++;
					// Now scan until '=' or ';' or ',' or end
					int k = j;
					bool foundEq = false;
					for(; k < len; k++)
					{
						if(headerValue[ k ] == '=')
						{ foundEq = true; break; }
						if(headerValue[ k ] == ';' || headerValue[ k ] == ',')
							break;
					}

					if(foundEq)
					{
						// comma separates cookies
						string part = headerValue.Substring(last, i - last).Trim();
						if(part.Length > 0)
							yield return part;
						last = i + 1;
					}
				}
			}

			// yield final chunk
			string finalPart = headerValue.Substring(last).Trim();
			if(finalPart.Length > 0)
				yield return finalPart;
		}

		private static string ExtractDomainAttribute(string setCookie)
		{
			if(string.IsNullOrEmpty(setCookie))
				return string.Empty;
			string lower = setCookie.ToLowerInvariant();
			int idx = lower.IndexOf("domain=");
			if(idx < 0)
				return string.Empty;
			int start = idx + "domain=".Length;
			int end = start;
			while(end < setCookie.Length && setCookie[ end ] != ';')
				end++;
			string domain = setCookie.Substring(start, end - start).Trim();
			return domain;
		}
	}
}