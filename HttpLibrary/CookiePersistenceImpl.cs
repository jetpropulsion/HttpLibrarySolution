using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace HttpLibrary
{
	internal sealed class CookiePersistenceImpl : ICookiePersistence, IDisposable
	{
		private readonly object _storeLock = new object();
		private string _cookieFilePath;
		private Timer? _saveTimer;
		private bool _initialized;
		private bool _disposed;

		// track last saved serialized JSON to avoid duplicate writes/logs
		private string? _lastSavedJson;

		// ClientName -> CookieContainer (live runtime containers)
		private readonly ConcurrentDictionary<string, CookieContainer> _containers = new(StringComparer.OrdinalIgnoreCase);
		// ClientName -> base address string (used when extracting cookies from Container)
		private readonly ConcurrentDictionary<string, string> _baseAddresses = new(StringComparer.OrdinalIgnoreCase);
		// Persisted hierarchical store: ClientName -> ClientCookieStore (Domain -> DomainCookieStore)
		private readonly ConcurrentDictionary<string, ClientCookieStore> _persistedStores = new(StringComparer.OrdinalIgnoreCase);

		public CookiePersistenceImpl()
		{
			_cookieFilePath = Constants.CookiesFile;
		}

		public void Initialize(string cookieFilePath)
		{
			if(string.IsNullOrWhiteSpace(cookieFilePath))
				throw new ArgumentException("cookieFilePath is required", nameof(cookieFilePath));

			// Resolve relative paths to application base directory to ensure consistent location
			string resolvedPath = cookieFilePath;
			try
			{
				if(!System.IO.Path.IsPathRooted(cookieFilePath))
				{
					string baseDir = AppContext.BaseDirectory ?? System.IO.Directory.GetCurrentDirectory();
					resolvedPath = System.IO.Path.Combine(baseDir, cookieFilePath);
				}
			}
			catch
			{
				resolvedPath = cookieFilePath; // fallback to provided value
			}

			_cookieFilePath = resolvedPath;

			lock(_storeLock)
			{
				if(_initialized)
					return;

				// Load persisted cookies (new hierarchical format preferred)
				try
				{
					if(File.Exists(_cookieFilePath))
					{
						string json = File.ReadAllText(_cookieFilePath);
						if(!string.IsNullOrWhiteSpace(json))
						{
							try
							{
								Dictionary<string, ClientCookieStore>? map = JsonSerializer.Deserialize(json, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
								if(map != null)
								{
									int clients = 0;
									int total = 0;
									foreach(var kv in map)
									{
										ClientCookieStore normalized = new ClientCookieStore();
										if(kv.Value?.Domains != null)
										{
											foreach(var d in kv.Value.Domains)
											{
												string domainKey = ( d.Key ?? string.Empty ).Trim();
												if(domainKey.StartsWith('.'))
													domainKey = domainKey.Substring(1);
												domainKey = domainKey.ToLowerInvariant();
												DomainCookieStore dc = new DomainCookieStore();
												if(d.Value?.Cookies != null)
												{
													dc.Cookies = new List<PersistedCookie>(d.Value.Cookies);
												}
												normalized.Domains[ domainKey ] = dc;
												total += dc.Cookies?.Count ?? 0;
											}
										}
										_persistedStores[ kv.Key ] = normalized;
										clients++;
									}
									LoggerBridge.LogInformation("Loaded {ClientCount} clients with {TotalCookies} cookies from '{Path}'", clients, total, _cookieFilePath);
									_lastSavedJson = json.Trim();
								}
							}
							catch(JsonException)
							{
								// Attempt to read old flat format and migrate
								try
								{
									Dictionary<string, List<PersistedCookie>>? oldMap = JsonSerializer.Deserialize(json, HttpLibraryJsonContext.Default.DictionaryStringListPersistedCookie);
									if(oldMap != null)
									{
										int clients = 0;
										int total = 0;
										foreach(var kv in oldMap)
										{
											ClientCookieStore store = MigrateFromOldFormat(kv.Value);
											ClientCookieStore normalized = new ClientCookieStore();
											foreach(var d in store.Domains)
											{
												string domainKey = ( d.Key ?? string.Empty ).Trim();
												if(domainKey.StartsWith('.'))
													domainKey = domainKey.Substring(1);
												domainKey = domainKey.ToLowerInvariant();
												DomainCookieStore dc = new DomainCookieStore();
												dc.Cookies = new List<PersistedCookie>(d.Value.Cookies);
												normalized.Domains[ domainKey ] = dc;
												total += dc.Cookies?.Count ?? 0;
											}
											_persistedStores[ kv.Key ] = normalized;
											clients++;
										}
										LoggerBridge.LogInformation("Migrated {ClientCount} clients with {TotalCookies} cookies from old format to new hierarchical format", clients, total);
										// Save migrated format
										SaveCookies();
										_lastSavedJson = File.Exists(_cookieFilePath) ? File.ReadAllText(_cookieFilePath).Trim() : null;
									}
								}
								catch
								{
									// ignore migration errors
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					LoggerBridge.LogWarning("Failed to load persisted cookies from '{Path}': {Message}", _cookieFilePath, ex.Message);
				}

				// Register process exit save and periodic save timer
				try
				{
					AppDomain.CurrentDomain.ProcessExit += (s, e) => SaveCookies();
					_saveTimer = new Timer(_ => TimerTick(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
				}
				catch(Exception ex)
				{
					// best-effort
					LoggerBridge.LogWarning("Failed to initialize cookie save timer: {Message}", ex.Message);
				}

				_initialized = true;
			}
		}

		// Expose cookie file path for tests
		internal string GetCookieFilePath()
		{
			return _cookieFilePath;
		}

		/// <summary>
		/// Extracts base address and registers it for the client (used when saving cookies)
		/// </summary>
		public void SetBaseAddress(string clientName, string baseAddress)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				return;
			if(string.IsNullOrWhiteSpace(baseAddress))
				return;
			_baseAddresses[ clientName ] = baseAddress;
		}

		/// <summary>
		/// Registers a client runtime container and populates it from persisted store if present.
		/// </summary>
		public CookieContainer? RegisterClient(string clientName, string? uri, bool persist)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				throw new ArgumentException("clientName is required", nameof(clientName));
			if(!string.IsNullOrWhiteSpace(uri))
				_baseAddresses[ clientName ] = uri!;
			if(!persist)
				return null;

			CookieContainer container = _containers.GetOrAdd(clientName, _ => new CookieContainer());

			// Populate from persisted store (skip expired)
			if(_persistedStores.TryGetValue(clientName, out ClientCookieStore? store) && store != null)
			{
				int added = 0;
				foreach(var domainKv in store.Domains)
				{
					string domain = domainKv.Key;
					foreach(PersistedCookie pc in domainKv.Value.Cookies)
					{
						if(pc.Expires.HasValue && pc.Expires.Value.ToUniversalTime() <= DateTime.UtcNow)
							continue;
						try
						{
							Cookie c = new Cookie(pc.Name, pc.Value, pc.Path ?? Constants.DefaultCookiePath, pc.Domain);
							if(pc.Expires.HasValue)
								c.Expires = pc.Expires.Value.ToLocalTime();
							c.Secure = pc.Secure;
							c.HttpOnly = pc.HttpOnly;
							// Add to container using a best-effort URI
							if(!string.IsNullOrWhiteSpace(pc.Domain))
							{
								string d = pc.Domain.StartsWith('.') ? pc.Domain.Substring(1) : pc.Domain;
								Uri baseUri = new Uri(Constants.HttpsScheme + d + "/");
								container.Add(baseUri, c);
							}
							else
							{
								container.Add(c);
							}
							added++;
						}
						catch
						{
							// ignore add errors
						}
					}
				}
				LoggerBridge.LogInformation("Populated cookie container for '{ClientName}' with {Count} persisted cookies", clientName, added);
			}

			return container;
		}

		private static string ComputeDefaultPath(Uri baseUri)
		{
			if(baseUri == null)
				return Constants.DefaultCookiePath;
			string path = baseUri.AbsolutePath;
			if(string.IsNullOrEmpty(path) || !path.StartsWith("/"))
				return Constants.DefaultCookiePath;
			int lastSlash = path.LastIndexOf('/');
			if(lastSlash <= 0)
				return "/";
			if(lastSlash == 0)
				return "/";
			return path.Substring(0, lastSlash);
		}

		private static string NormalizeDomainAttribute(string domain)
		{
			if(string.IsNullOrWhiteSpace(domain))
				return string.Empty;
			domain = domain.Trim();
			if(domain.StartsWith('.'))
				domain = domain.Substring(1);
			return domain.ToLowerInvariant();
		}

		private static bool DomainMatchesRequestHost(string domainAttr, string requestHost)
		{
			if(string.IsNullOrWhiteSpace(domainAttr) || string.IsNullOrWhiteSpace(requestHost))
				return false;
			// domainAttr is normalized (no leading dot)
			if(string.Equals(requestHost, domainAttr, StringComparison.OrdinalIgnoreCase))
				return true;
			// requestHost must be a subdomain of domainAttr
			if(requestHost.EndsWith("." + domainAttr, StringComparison.OrdinalIgnoreCase))
				return true;
			return false;
		}

		/// <summary>
		/// Parse Set-Cookie header according to RFC6265 and update container & persisted store.
		/// This parser implements core RFC6265 rules and handles Max-Age, Expires, Domain, Path, Secure, HttpOnly, SameSite.
		/// It also enforces cookie-prefix rules (__Secure-, __Host-) and SameSite semantics.
		/// </summary>
		public void AddCookieFromHeader(string clientName, string setCookieHeader, Uri baseUri)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				return;
			if(string.IsNullOrEmpty(setCookieHeader))
				return;
			if(baseUri == null)
				return;

			string header = setCookieHeader;
			List<string> parts = new List<string>();
			int pos = 0;
			while(pos < header.Length)
			{
				int semi = header.IndexOf(';', pos);
				if(semi == -1)
				{
					parts.Add(header.Substring(pos).Trim());
					break;
				}
				string token = header.Substring(pos, semi - pos).Trim();
				parts.Add(token);
				pos = semi + 1;
			}

			if(parts.Count == 0)
				return;

			string nameValue = parts[ 0 ];
			int eq = nameValue.IndexOf('=');
			if(eq <= 0)
				return; // invalid
			string name = nameValue.Substring(0, eq).Trim();
			string value = nameValue.Substring(eq + 1).Trim();
			if(value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
			{
				value = value.Substring(1, value.Length - 2);
			}

			// Log received Set-Cookie header parsing result (best-effort)
			try
			{
				LoggerBridge.LogInformation("Received Set-Cookie for client '{Client}': {Name}={ValueRaw}", clientName, name, value);
			}
			catch { }

			// Validate name/value per RFC6265
			if(!IsValidCookieName(name))
			{
				try
				{ LoggerBridge.LogWarning("Rejected cookie '{Name}' for client '{Client}': invalid cookie name", name, clientName); }
				catch { }
				return;
			}
			if(!IsValidCookieValue(value))
			{
				try
				{ LoggerBridge.LogWarning("Rejected cookie '{Name}' for client '{Client}': invalid cookie value", name, clientName); }
				catch { }
				return;
			}

			string requestHost = baseUri.Host;
			string domain = requestHost; // default: host-only cookie
			string path = ComputeDefaultPath(baseUri);
			DateTime? expiresFromExpires = null;
			DateTime? expiresFromMaxAge = null;
			bool secure = false, httpOnly = false;
			string? sameSite = null;
			bool domainAttributePresent = false;

			for(int i = 1; i < parts.Count; i++)
			{
				string attr = parts[ i ];
				int ai = attr.IndexOf('=');
				string key = ai > 0 ? attr.Substring(0, ai).Trim() : attr.Trim();
				string val = ai > 0 ? attr.Substring(ai + 1).Trim() : string.Empty;
				switch(key.ToLowerInvariant())
				{
					case Constants.CookieAttributeDomain:
					if(!string.IsNullOrWhiteSpace(val))
					{
						domainAttributePresent = true;
						string normalized = NormalizeDomainAttribute(val);
						// If domain attribute does not domain-match request host, reject the cookie (per RFC)
						if(DomainMatchesRequestHost(normalized, requestHost))
						{
							domain = normalized;
						}
						else
						{
							// Non-matching domain attribute; per RFC6265, reject the cookie
							LoggerBridge.LogWarning("Rejected cookie '{Name}' for client '{Client}' due to invalid Domain attribute '{DomainAttr}' not matching request host '{Host}'", name, clientName, val, requestHost);
							return;
						}
					}
					break;
					case Constants.CookieAttributePath:
					if(!string.IsNullOrWhiteSpace(val))
						path = val;
					break;
					case Constants.CookieAttributeExpires:
					// permissive parsing: try invariant then current culture
					if(DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime dt))
					{
						expiresFromExpires = dt.ToUniversalTime();
					}
					else if(DateTime.TryParse(val, out DateTime dt2))
					{
						expiresFromExpires = dt2.ToUniversalTime();
					}
					break;
					case Constants.CookieAttributeMaxAge:
					if(int.TryParse(val, out int seconds))
					{
						if(seconds <= 0)
							expiresFromMaxAge = DateTime.UtcNow.AddSeconds(-1);
						else
							expiresFromMaxAge = DateTime.UtcNow.AddSeconds(seconds);
					}
					break;
					case Constants.CookieAttributeSecure:
					secure = true;
					break;
					case Constants.CookieAttributeHttpOnly:
					httpOnly = true;
					break;
					case Constants.CookieAttributeSameSite:
					if(!string.IsNullOrWhiteSpace(val))
					{
						string norm = val.Trim();
						if(string.Equals(norm, Constants.SameSiteStrict, StringComparison.OrdinalIgnoreCase) || string.Equals(norm, Constants.SameSiteLax, StringComparison.OrdinalIgnoreCase) || string.Equals(norm, Constants.SameSiteNone, StringComparison.OrdinalIgnoreCase))
						{
							sameSite = char.ToUpperInvariant(norm[ 0 ]) + norm.Substring(1).ToLowerInvariant();
						}
					}
					break;
					default:
					// unknown attribute - ignore
					break;
				}
			}

			DateTime? expires = expiresFromMaxAge ?? expiresFromExpires;

			// Enforce cookie-prefix policies
			if(name.StartsWith("__Secure-", StringComparison.Ordinal))
			{
				if(!secure || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
				{
					try
					{ LoggerBridge.LogWarning("Rejected __Secure- cookie '{Name}' for client '{Client}': must be Secure and use HTTPS", name, clientName); }
					catch { }
					return;
				}
			}
			if(name.StartsWith("__Host-", StringComparison.Ordinal))
			{
				// __Host- cookies must be Secure, have Path=/, and must not have a Domain attribute
				if(!secure || path != "/" || domainAttributePresent)
				{
					try
					{ LoggerBridge.LogWarning("Rejected __Host- cookie '{Name}' for client '{Client}': must be Secure, Path=/, and have no Domain attribute", name, clientName); }
					catch { }
					return;
				}
			}

			// SameSite=None requires Secure
			if(string.Equals(sameSite, Constants.SameSiteNone, StringComparison.OrdinalIgnoreCase) && !secure)
			{
				try
				{ LoggerBridge.LogWarning("Rejected cookie '{Name}' for client '{Client}': SameSite=None requires Secure", name, clientName); }
				catch { }
				return;
			}

			// Check if cookie is already expired -> deletion behavior
			if(expires.HasValue && expires.Value.ToUniversalTime() <= DateTime.UtcNow)
			{
				try
				{ LoggerBridge.LogInformation("Cookie '{Name}' for client '{Client}' indicates deletion (expired/max-age=0); removing", name, clientName); }
				catch { }
				// Remove from persisted store
				try
				{
					if(_persistedStores.TryGetValue(clientName, out ClientCookieStore? store) && store != null)
					{
						string domainKey = NormalizeDomainAttribute(domain);
						if(store.Domains.TryGetValue(domainKey, out DomainCookieStore? domainStore) && domainStore != null)
						{
							for(int i = domainStore.Cookies.Count - 1; i >= 0; i--)
							{
								PersistedCookie pc = domainStore.Cookies[ i ];
								if(string.Equals(pc.Name, name, StringComparison.Ordinal) && string.Equals(NormalizeDomainAttribute(pc.Domain), domainKey, StringComparison.OrdinalIgnoreCase) && string.Equals(pc.Path ?? Constants.DefaultCookiePath, path, StringComparison.Ordinal))
								{
									domainStore.Cookies.RemoveAt(i);
								}
							}
						}
					}
				}
				catch { }

				// Remove from container by adding expired cookie for immediate removal
				CookieContainer? cont = GetContainer(clientName);
				if(cont != null)
				{
					try
					{
						Cookie expired = new Cookie(name, value, path, domain) { Expires = expires.Value.ToLocalTime(), Secure = secure, HttpOnly = httpOnly };
						cont.Add(baseUri, expired);
					}
					catch { }
				}

				return;
			}

			// Add to runtime container
			CookieContainer? container = GetContainer(clientName);
			if(container != null)
			{
				try
				{
					Cookie cookie = new Cookie(name, value, path, domain);
					if(expires.HasValue)
						cookie.Expires = expires.Value.ToLocalTime();
					cookie.Secure = secure;
					cookie.HttpOnly = httpOnly;
					container.Add(baseUri, cookie);
					try
					{
						LoggerBridge.LogInformation("Added cookie to runtime container for client '{Client}': {Name}={ValueRaw} (Domain={Domain}, Path={Path})", clientName, name, value, domain, path);
					}
					catch { }
				}
				catch { }
			}

			// Persist in hierarchical store
			try
			{
				ClientCookieStore store = _persistedStores.GetOrAdd(clientName, _ => new ClientCookieStore());
				string domainKey = NormalizeDomainAttribute(domain);
				DomainCookieStore domainStore = store.Domains.GetValueOrDefault(domainKey) ?? new DomainCookieStore();
				store.Domains[ domainKey ] = domainStore;

				// Remove existing same name/domain/path (RFC: name case-sensitive)
				for(int i = domainStore.Cookies.Count - 1; i >= 0; i--)
				{
					PersistedCookie pc = domainStore.Cookies[ i ];
					if(string.Equals(pc.Name, name, StringComparison.Ordinal) && string.Equals(NormalizeDomainAttribute(pc.Domain), domainKey, StringComparison.OrdinalIgnoreCase) && string.Equals(pc.Path ?? Constants.DefaultCookiePath, path, StringComparison.Ordinal))
					{
						domainStore.Cookies.RemoveAt(i);
					}
				}

				PersistedCookie newPc = new PersistedCookie { Name = name, Value = value, Domain = domainKey, Path = path, Expires = expires, Secure = secure, HttpOnly = httpOnly, SameSite = sameSite, IsSession = !expires.HasValue };
				domainStore.Cookies.Add(newPc);

				// Log persistence
				try
				{
					LoggerBridge.LogInformation("Persisted cookie for client '{Client}': {Name}={ValueRaw} (Domain={Domain}, Path={Path}, Expires={Expires})", clientName, name, value, domainKey, path ?? Constants.DefaultCookiePath, expires?.ToString(Constants.Iso8601FormatSpecifier) ?? Constants.SessionCookieText);
				}
				catch { }
			}
			catch { }
		}

		public CookieContainer? GetContainer(string clientName)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				return null;
			if(_containers.TryGetValue(clientName, out CookieContainer? c))
				return c;
			return null;
		}

		public List<string> GetPersistedClientNames()
		{
			return new List<string>(_persistedStores.Keys);
		}

		public List<PersistedCookie> GetPersistedCookies(string clientName)
		{
			List<PersistedCookie> result = new List<PersistedCookie>();
			if(string.IsNullOrWhiteSpace(clientName))
				return result;
			if(_persistedStores.TryGetValue(clientName, out ClientCookieStore? store) && store != null)
			{
				foreach(var domainKv in store.Domains)
				{
					if(domainKv.Value?.Cookies != null)
					{
						result.AddRange(domainKv.Value.Cookies);
					}
				}
			}
			return result;
		}

		public int CleanupOrphanedCookies(ISet<string> currentClientNames)
		{
			if(currentClientNames == null)
				throw new ArgumentNullException(nameof(currentClientNames));
			List<string> toRemove = new List<string>();
			foreach(var kv in _persistedStores)
			{
				if(!currentClientNames.Contains(kv.Key))
					toRemove.Add(kv.Key);
			}
			int removed = 0;
			foreach(string k in toRemove)
			{
				_persistedStores.TryRemove(k, out _);
				removed++;
			}
			if(removed > 0)
				SaveCookies();
			return removed;
		}

		public bool HasPersistedEntry(string clientName)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				return false;
			return _persistedStores.ContainsKey(clientName);
		}

		public void PruneExpired(string clientName)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				return;
			if(!_containers.TryGetValue(clientName, out CookieContainer? container) || container == null)
				return;
			if(!_baseAddresses.TryGetValue(clientName, out string? baseAddr) || string.IsNullOrWhiteSpace(baseAddr) || !Uri.TryCreate(baseAddr, UriKind.Absolute, out Uri? baseUri))
				return;
			try
			{
				CookieCollection cookies = container.GetCookies(baseUri);
				List<Cookie> toRemove = new List<Cookie>();
				foreach(Cookie cookie in cookies)
				{
					if(cookie.Expires != DateTime.MinValue && cookie.Expires.ToUniversalTime() <= DateTime.UtcNow)
						toRemove.Add(cookie);
				}
				foreach(Cookie c in toRemove)
				{
					try
					{ cookies.Remove(c); }
					catch { }
				}
				if(toRemove.Count > 0)
					SaveCookies();
			}
			catch { }
		}

		public void PruneAll()
		{
			foreach(var kv in _persistedStores.Keys.ToList())
				PruneExpired(kv);
		}

		public void SaveCookies()
		{
			if(_disposed)
				return;
			lock(_storeLock)
			{
				if(_disposed)
					return;
				try
				{
					Dictionary<string, ClientCookieStore> outMap = new Dictionary<string, ClientCookieStore>(StringComparer.OrdinalIgnoreCase);

					// Start with persisted stores copy
					foreach(var kv in _persistedStores)
					{
						ClientCookieStore copy = new ClientCookieStore();
						foreach(var d in kv.Value.Domains)
						{
							DomainCookieStore dc = new DomainCookieStore();
							dc.Cookies = new List<PersistedCookie>(d.Value.Cookies);
							copy.Domains[ d.Key ] = dc;
						}
						outMap[ kv.Key ] = copy;
					}

					// Merge live cookies from containers
					foreach(var kv in _containers)
					{
						string clientKey = kv.Key;
						ClientCookieStore store = outMap.GetValueOrDefault(clientKey) ?? new ClientCookieStore();
						outMap[ clientKey ] = store;

						string? baseAddr = _baseAddresses.GetValueOrDefault(clientKey);
						if(!string.IsNullOrWhiteSpace(baseAddr) && Uri.TryCreate(baseAddr, UriKind.Absolute, out Uri? baseUri))
						{
							CookieCollection cookies = kv.Value.GetCookies(baseUri);
							foreach(Cookie cookie in cookies)
							{
								DateTime? expires = null;
								if(cookie.Expires != DateTime.MinValue)
									expires = cookie.Expires.ToUniversalTime();
								if(expires.HasValue && expires.Value <= DateTime.UtcNow)
									continue; // skip expired

								string domain = cookie.Domain ?? string.Empty;
								string domainKey = NormalizeDomainAttribute(domain);
								DomainCookieStore domainStore = store.Domains.GetValueOrDefault(domainKey) ?? new DomainCookieStore();
								store.Domains[ domainKey ] = domainStore;

								// Preserve SameSite if existing persisted cookie had it (search BEFORE removing duplicates)
								string? sameSite = null;
								for(int j = 0; j < domainStore.Cookies.Count; j++)
								{
									var existing = domainStore.Cookies[ j ];
									if(string.Equals(existing.Name, cookie.Name, StringComparison.Ordinal) && string.Equals(NormalizeDomainAttribute(existing.Domain), domainKey, StringComparison.OrdinalIgnoreCase) && string.Equals(existing.Path ?? Constants.DefaultCookiePath, cookie.Path, StringComparison.Ordinal))
									{
										sameSite = existing.SameSite;
										break;
									}
								}

								// Remove duplicates same name/domain/path
								for(int i = domainStore.Cookies.Count - 1; i >= 0; i--)
								{
									PersistedCookie pc = domainStore.Cookies[ i ];
									if(string.Equals(pc.Name, cookie.Name, StringComparison.Ordinal) && string.Equals(NormalizeDomainAttribute(pc.Domain), domainKey, StringComparison.OrdinalIgnoreCase) && string.Equals(pc.Path ?? Constants.DefaultCookiePath, cookie.Path, StringComparison.Ordinal))
									{
										domainStore.Cookies.RemoveAt(i);
									}
								}

								domainStore.Cookies.Add(new PersistedCookie { Name = cookie.Name, Value = cookie.Value, Domain = domainKey, Path = cookie.Path, Expires = expires, Secure = cookie.Secure, HttpOnly = cookie.HttpOnly, SameSite = sameSite, IsSession = !expires.HasValue });
							}
						}
					}

					// Remove expired cookies from outMap
					foreach(var kvp in outMap)
					{
						foreach(var domainKvp in kvp.Value.Domains)
						{
							for(int i = domainKvp.Value.Cookies.Count - 1; i >= 0; i--)
							{
								PersistedCookie pc = domainKvp.Value.Cookies[ i ];
								if(pc.Expires.HasValue && pc.Expires.Value.ToUniversalTime() <= DateTime.UtcNow)
								{
									domainKvp.Value.Cookies.RemoveAt(i);
								}
							}
						}
					}

					// Build deterministic ordered map for stable serialization
					Dictionary<string, ClientCookieStore> orderedMap = new Dictionary<string, ClientCookieStore>(StringComparer.OrdinalIgnoreCase);
					foreach(var clientKv in outMap.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
					{
						ClientCookieStore orderedStore = new ClientCookieStore();
						foreach(var domainKv in clientKv.Value.Domains.OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase))
						{
							DomainCookieStore orderedDomain = new DomainCookieStore();
							// Sort cookies by Name, Path to ensure stable ordering
							orderedDomain.Cookies = domainKv.Value.Cookies.OrderBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Path ?? Constants.DefaultCookiePath, StringComparer.Ordinal).ToList();
							orderedStore.Domains[ domainKv.Key ] = orderedDomain;
						}
						orderedMap[ clientKv.Key ] = orderedStore;
					}

					string outJson = JsonSerializer.Serialize(orderedMap, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);

					// If nothing changed since last save, skip writing to avoid duplicate saves
					string outJsonTrimmed = outJson.Trim();
					if(_lastSavedJson != null && string.Equals(_lastSavedJson, outJsonTrimmed, StringComparison.Ordinal))
					{
						// no-op
						return;
					}

					// Atomic write
					string tempPath = _cookieFilePath + Constants.TempFileExtension;
					File.WriteAllText(tempPath, outJson);
					try
					{
						if(File.Exists(_cookieFilePath))
							File.Replace(tempPath, _cookieFilePath, null);
						else
							File.Move(tempPath, _cookieFilePath);
						_lastSavedJson = outJsonTrimmed;
						LoggerBridge.LogInformation("Saved {ClientCount} clients with hierarchical cookie stores to '{Path}'", orderedMap.Count, _cookieFilePath);
					}
					catch(Exception ex)
					{
						LoggerBridge.LogWarning("Failed to replace cookie file, attempting fallback write: {Message}", ex.Message);
						try
						{ File.WriteAllText(_cookieFilePath, outJson); _lastSavedJson = outJsonTrimmed; LoggerBridge.LogInformation("Saved {ClientCount} clients with hierarchical cookie stores to '{Path}' (fallback)", orderedMap.Count, _cookieFilePath); }
						catch(Exception ex2) { LoggerBridge.LogWarning("Failed to save persisted cookies to '{Path}': {Message}", _cookieFilePath, ex2.Message); }
					}
				}
				catch(Exception ex)
				{
					LoggerBridge.LogWarning("Failed to save persisted cookies to '{Path}': {Message}", _cookieFilePath, ex.Message);
				}
			}
		}

		private void TimerTick()
		{
			try
			{ LoggerBridge.LogInformation("Cookie save timer tick at {Time}", DateTime.UtcNow.ToString(Constants.Iso8601FormatSpecifier)); }
			catch { }
			SaveCookies();
		}

		public void Reset()
		{
			lock(_storeLock)
			{
				_saveTimer?.Dispose();
				_saveTimer = null;
				_containers.Clear();
				_baseAddresses.Clear();
				_persistedStores.Clear();
				_initialized = false;
				_cookieFilePath = Constants.CookiesFile;
				_lastSavedJson = null;
			}
		}

		public int MigrateCookies(string oldClientName, string newClientName)
		{
			if(string.IsNullOrWhiteSpace(oldClientName))
				throw new ArgumentNullException(nameof(oldClientName));
			if(string.IsNullOrWhiteSpace(newClientName))
				throw new ArgumentNullException(nameof(newClientName));
			if(string.Equals(oldClientName, newClientName, StringComparison.OrdinalIgnoreCase))
				return 0;
			if(!_persistedStores.TryRemove(oldClientName, out ClientCookieStore? oldStore))
				return 0;
			if(oldStore == null)
				return 0;
			ClientCookieStore newStore = _persistedStores.GetOrAdd(newClientName, _ => new ClientCookieStore());
			int migrated = 0;
			foreach(var domainKv in oldStore.Domains)
			{
				DomainCookieStore newDomain = newStore.Domains.GetValueOrDefault(domainKv.Key) ?? new DomainCookieStore();
				newStore.Domains[ domainKv.Key ] = newDomain;
				foreach(var cookie in domainKv.Value.Cookies)
				{
					// replace existing if same name/domain/path
					bool exists = false;
					for(int i = 0; i < newDomain.Cookies.Count; i++)
					{
						var existing = newDomain.Cookies[ i ];
						if(string.Equals(existing.Name, cookie.Name, StringComparison.Ordinal) && string.Equals(existing.Domain, cookie.Domain, StringComparison.OrdinalIgnoreCase) && string.Equals(existing.Path ?? Constants.DefaultCookiePath, cookie.Path ?? Constants.DefaultCookiePath, StringComparison.Ordinal))
						{
							newDomain.Cookies[ i ] = cookie;
							exists = true;
							break;
						}
					}
					if(!exists)
					{
						newDomain.Cookies.Add(cookie);
					}
					migrated++;
				}
			}
			SaveCookies();
			return migrated;
		}

		public System.Collections.Generic.Dictionary<string, int> GenerateReport()
		{
			var report = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach(var kv in _persistedStores)
			{
				int count = 0;
				foreach(var d in kv.Value.Domains)
					count += d.Value.Cookies?.Count ?? 0;
				report[ kv.Key ] = count;
			}
			return report;
		}

		public int RemoveAllCookiesForClient(string clientName)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				throw new ArgumentException(nameof(clientName));
			int count = GetPersistedCookies(clientName).Count;
			_persistedStores.TryRemove(clientName, out _);
			SaveCookies();
			return count;
		}

		// Programmatic cookie API implementations
		public void AddCookie(string clientName, string domain, string? path, string name, string value, DateTime? expiresUtc, bool secure, bool httpOnly, string? sameSite)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				throw new ArgumentException(nameof(clientName));
			if(string.IsNullOrWhiteSpace(name))
				throw new ArgumentException(nameof(name));

			if(!IsValidCookieName(name) || !IsValidCookieValue(value))
				return;

			string domainKey = NormalizeDomainAttribute(domain ?? string.Empty);
			string cookiePath = string.IsNullOrWhiteSpace(path) ? Constants.DefaultCookiePath : path!;

			// If a domain was supplied programmatically and we have a registered base address for the client,
			// ensure the domain attribute domain-matches the base address host per RFC. Reject if it does not.
			if(!string.IsNullOrEmpty(domainKey) && _baseAddresses.TryGetValue(clientName, out string? baseAddr) && !string.IsNullOrWhiteSpace(baseAddr) && Uri.TryCreate(baseAddr, UriKind.Absolute, out Uri? baseUri))
			{
				if(!DomainMatchesRequestHost(domainKey, baseUri.Host))
				{
					// reject per RFC
					return;
				}
			}

			// prefix enforcement
			if(name.StartsWith("__Secure-", StringComparison.Ordinal) && !secure)
			{
				try
				{ LoggerBridge.LogWarning("Rejected __Secure- cookie '{Name}' for client '{Client}': __Secure- cookies must have Secure attribute", name, clientName); }
				catch { }
				return;
			}
			if(name.StartsWith("__Host-", StringComparison.Ordinal) && ( !secure || cookiePath != "/" || !string.IsNullOrEmpty(domainKey) ))
			{
				try
				{ LoggerBridge.LogWarning("Rejected __Host- cookie '{Name}' for client '{Client}': __Host- cookies must be Secure, Path=/, and have no Domain", name, clientName); }
				catch { }
				return;
			}

			if(!string.IsNullOrWhiteSpace(sameSite))
			{
				string norm = sameSite!.Trim();
				if(string.Equals(norm, Constants.SameSiteStrict, StringComparison.OrdinalIgnoreCase) || string.Equals(norm, Constants.SameSiteLax, StringComparison.OrdinalIgnoreCase) || string.Equals(norm, Constants.SameSiteNone, StringComparison.OrdinalIgnoreCase))
				{
					sameSite = char.ToUpperInvariant(norm[ 0 ]) + norm.Substring(1).ToLowerInvariant();
				}
				else
				{
					try
					{ LoggerBridge.LogWarning("Rejected cookie '{Name}' for client '{Client}': invalid SameSite token '{SameSite}'", name, clientName, sameSite); }
					catch { }
					sameSite = null;
				}
			}

			if(string.Equals(sameSite, Constants.SameSiteNone, StringComparison.OrdinalIgnoreCase) && !secure)
			{
				try
				{ LoggerBridge.LogWarning("Rejected cookie '{Name}' for client '{Client}': SameSite=None requires Secure", name, clientName); }
				catch { }
				return;
			}

			// expiry deletion
			if(expiresUtc.HasValue && expiresUtc.Value.ToUniversalTime() <= DateTime.UtcNow)
			{
				try
				{ LoggerBridge.LogInformation("Cookie '{Name}' for client '{Client}' indicates deletion (expires/max-age<=0); removing", name, clientName); }
				catch { }
				RemoveCookie(clientName, domainKey, cookiePath, name);
				return;
			}

			// add to runtime container
			CookieContainer? container = GetContainer(clientName);
			try
			{
				Cookie cookie = new Cookie(name, value, cookiePath, string.IsNullOrEmpty(domainKey) ? string.Empty : domainKey);
				if(expiresUtc.HasValue)
					cookie.Expires = expiresUtc.Value.ToLocalTime();
				cookie.Secure = secure;
				cookie.HttpOnly = httpOnly;
				if(container != null)
				{
					if(_baseAddresses.TryGetValue(clientName, out string? baseAddr2) && Uri.TryCreate(baseAddr2, UriKind.Absolute, out Uri? baseUri2))
						container.Add(baseUri2, cookie);
					else
					{
						try
						{
							Uri uri = new Uri(Constants.HttpsScheme + ( domainKey.StartsWith('.') ? domainKey.Substring(1) : domainKey ) + "/");
							container.Add(uri, cookie);
						}
						catch { container.Add(cookie); }
					}
				}
			}
			catch { }

			// persist
			try
			{
				ClientCookieStore store = _persistedStores.GetOrAdd(clientName, _ => new ClientCookieStore());
				DomainCookieStore domainStore = store.Domains.GetValueOrDefault(domainKey) ?? new DomainCookieStore();
				store.Domains[ domainKey ] = domainStore;

				for(int i = domainStore.Cookies.Count - 1; i >= 0; i--)
				{
					PersistedCookie pc = domainStore.Cookies[ i ];
					if(string.Equals(pc.Name, name, StringComparison.Ordinal) && string.Equals(NormalizeDomainAttribute(pc.Domain), domainKey, StringComparison.OrdinalIgnoreCase) && string.Equals(pc.Path ?? Constants.DefaultCookiePath, cookiePath, StringComparison.Ordinal))
					{
						domainStore.Cookies.RemoveAt(i);
					}
				}

				PersistedCookie newPc = new PersistedCookie { Name = name, Value = value, Domain = domainKey, Path = cookiePath, Expires = expiresUtc, Secure = secure, HttpOnly = httpOnly, SameSite = sameSite, IsSession = !expiresUtc.HasValue };
				domainStore.Cookies.Add(newPc);
				SaveCookies();
			}
			catch { }
		}

		public bool RemoveCookie(string clientName, string domain, string? path, string name)
		{
			if(string.IsNullOrWhiteSpace(clientName) || string.IsNullOrWhiteSpace(name))
				return false;
			string domainKey = NormalizeDomainAttribute(domain ?? string.Empty);
			string cookiePath = string.IsNullOrWhiteSpace(path) ? Constants.DefaultCookiePath : path!;
			bool removed = false;

			if(_persistedStores.TryGetValue(clientName, out ClientCookieStore? store) && store != null)
			{
				if(store.Domains.TryGetValue(domainKey, out DomainCookieStore? domainStore) && domainStore != null)
				{
					for(int i = domainStore.Cookies.Count - 1; i >= 0; i--)
					{
						if(string.Equals(domainStore.Cookies[ i ].Name, name, StringComparison.Ordinal) && string.Equals(domainStore.Cookies[ i ].Path ?? Constants.DefaultCookiePath, cookiePath, StringComparison.Ordinal))
						{
							domainStore.Cookies.RemoveAt(i);
							removed = true;
						}
					}
					if(domainStore.Cookies.Count == 0)
						store.Domains.Remove(domainKey);
				}
			}

			// remove from runtime container by adding expired cookie
			CookieContainer? container = GetContainer(clientName);
			if(container != null)
			{
				try
				{
					Uri uri = new Uri(Constants.HttpsScheme + ( domainKey.StartsWith('.') ? domainKey.Substring(1) : domainKey ) + "/");
					Cookie expired = new Cookie(name, string.Empty, cookiePath, domainKey) { Expires = DateTime.UtcNow.AddSeconds(-1) };
					container.Add(uri, expired);
					removed = true;
				}
				catch { }
			}

			if(removed)
				SaveCookies();
			return removed;
		}

		public bool ModifyCookieValue(string clientName, string domain, string? path, string name, string newValue)
		{
			if(string.IsNullOrWhiteSpace(clientName) || string.IsNullOrWhiteSpace(name))
				return false;
			if(!IsValidCookieValue(newValue))
				return false;
			string domainKey = NormalizeDomainAttribute(domain ?? string.Empty);
			string cookiePath = string.IsNullOrWhiteSpace(path) ? Constants.DefaultCookiePath : path!;
			bool modified = false;

			// modify persisted store
			if(_persistedStores.TryGetValue(clientName, out ClientCookieStore? store) && store != null)
			{
				if(store.Domains.TryGetValue(domainKey, out DomainCookieStore? domainStore) && domainStore != null)
				{
					for(int i = 0; i < domainStore.Cookies.Count; i++)
					{
						if(string.Equals(domainStore.Cookies[ i ].Name, name, StringComparison.Ordinal) && string.Equals(domainStore.Cookies[ i ].Path ?? Constants.DefaultCookiePath, cookiePath, StringComparison.Ordinal))
						{
							domainStore.Cookies[ i ].Value = newValue;
							modified = true;
							break;
						}
					}
				}
			}

			// modify runtime container
			CookieContainer? container = GetContainer(clientName);
			if(container != null)
			{
				try
				{
					Uri uri = new Uri(Constants.HttpsScheme + ( domainKey.StartsWith('.') ? domainKey.Substring(1) : domainKey ) + "/");
					CookieCollection cc = container.GetCookies(uri);
					foreach(Cookie c in cc)
					{
						if(string.Equals(c.Name, name, StringComparison.Ordinal) && string.Equals(c.Path ?? Constants.DefaultCookiePath, cookiePath, StringComparison.Ordinal))
						{
							Cookie updated = new Cookie(c.Name, newValue, c.Path, c.Domain) { Secure = c.Secure, HttpOnly = c.HttpOnly };
							container.Add(uri, updated);
							modified = true;
						}
					}
				}
				catch { }
			}

			if(modified)
				SaveCookies();
			return modified;
		}

		public int RemoveSessionCookies(string clientName)
		{
			if(string.IsNullOrWhiteSpace(clientName))
				throw new ArgumentNullException(nameof(clientName));
			int removed = 0;
			if(_persistedStores.TryGetValue(clientName, out ClientCookieStore? store) && store != null)
			{
				foreach(var domainKv in store.Domains.ToList())
				{
					var cookies = domainKv.Value?.Cookies;
					if(cookies == null)
						continue;
					for(int i = cookies.Count - 1; i >= 0; i--)
					{
						if(cookies[ i ].IsSession)
						{
							cookies.RemoveAt(i);
							removed++;
						}
					}
					if(cookies.Count == 0)
						store.Domains.Remove(domainKv.Key);
				}
				if(removed > 0)
					SaveCookies();
			}

			// best-effort runtime cleanup
			if(_containers.TryGetValue(clientName, out CookieContainer? container) && container != null)
			{
				string? baseAddr = _baseAddresses.GetValueOrDefault(clientName);
				if(!string.IsNullOrWhiteSpace(baseAddr) && Uri.TryCreate(baseAddr, UriKind.Absolute, out Uri? baseUri))
				{
					try
					{
						CookieCollection cc = container.GetCookies(baseUri);
						List<Cookie> toRemove = new List<Cookie>();
						foreach(Cookie c in cc)
						{
							if(c.Expires == DateTime.MinValue)
								toRemove.Add(c);
						}
						foreach(Cookie r in toRemove)
						{
							try
							{ cc.Remove(r); }
							catch { }
						}
					}
					catch { }
				}
			}

			return removed;
		}

		private ClientCookieStore MigrateFromOldFormat(List<PersistedCookie> cookies)
		{
			ClientCookieStore store = new ClientCookieStore();
			foreach(var pc in cookies)
			{
				string domain = pc.Domain ?? string.Empty;
				if(!store.Domains.TryGetValue(domain, out DomainCookieStore? ds))
				{
					ds = new DomainCookieStore();
					store.Domains[ domain ] = ds;
				}
				ds.Cookies.Add(pc);
			}
			return store;
		}

		private static bool IsValidCookieName(string name)
		{
			if(string.IsNullOrWhiteSpace(name))
				return false;
			foreach(char c in name)
			{
				if(char.IsControl(c) || char.IsWhiteSpace(c) || Constants.Rfc6265CookieSeparators.Contains(c))
					return false;
			}
			return true;
		}

		private static bool IsValidCookieValue(string value)
		{
			if(value is null)
				return false;
			if(value.Length == 0)
				return true;
			foreach(char c in value)
				if(char.IsControl(c))
					return false;
			return true;
		}

		public void Dispose()
		{
			_disposed = true;
			_saveTimer?.Dispose();
			_saveTimer = null;
		}

		public void InitializeReadOnly(string cookieFilePath)
		{
			if(string.IsNullOrWhiteSpace(cookieFilePath))
				throw new ArgumentException(nameof(cookieFilePath));
			_cookieFilePath = cookieFilePath;

			lock(_storeLock)
			{
				if(_initialized)
					return;
				try
				{
					LoadFromFile();
				}
				catch(Exception ex)
				{
					LoggerBridge.LogWarning("Failed to load persisted cookies (read-only) from '{Path}': {Message}", _cookieFilePath, ex.Message);
				}
				// Do not start timers or register ProcessExit in read-only mode
				_initialized = true;
			}
		}

		private void LoadFromFile()
		{
			if(!File.Exists(_cookieFilePath))
				return;
			string json = File.ReadAllText(_cookieFilePath);
			if(string.IsNullOrWhiteSpace(json))
				return;

			try
			{
				Dictionary<string, ClientCookieStore>? map = JsonSerializer.Deserialize(json, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
				if(map != null)
				{
					int clients = 0;
					int total = 0;
					foreach(var kv in map)
					{
						ClientCookieStore normalized = new ClientCookieStore();
						if(kv.Value?.Domains != null)
						{
							foreach(var d in kv.Value.Domains)
							{
								string domainKey = ( d.Key ?? string.Empty ).Trim();
								if(domainKey.StartsWith('.'))
									domainKey = domainKey.Substring(1);
								domainKey = domainKey.ToLowerInvariant();
								DomainCookieStore dc = new DomainCookieStore();
								if(d.Value?.Cookies != null)
								{
									dc.Cookies = new List<PersistedCookie>(d.Value.Cookies);
								}
								normalized.Domains[ domainKey ] = dc;
								total += dc.Cookies?.Count ?? 0;
							}
						}
						_persistedStores[ kv.Key ] = normalized;
						clients++;
					}
					LoggerBridge.LogInformation("Loaded {ClientCount} clients with {TotalCookies} cookies from '{Path}'", clients, total, _cookieFilePath);
					_lastSavedJson = json.Trim();
				}
			}
			catch(JsonException)
			{
				// attempt to migrate old format
				try
				{
					Dictionary<string, List<PersistedCookie>>? oldMap = JsonSerializer.Deserialize(json, HttpLibraryJsonContext.Default.DictionaryStringListPersistedCookie);
					if(oldMap != null)
					{
						int clients = 0;
						int total = 0;
						foreach(var kv in oldMap)
						{
							ClientCookieStore store = MigrateFromOldFormat(kv.Value);
							ClientCookieStore normalized = new ClientCookieStore();
							foreach(var d in store.Domains)
							{
								string domainKey = ( d.Key ?? string.Empty ).Trim();
								if(domainKey.StartsWith('.'))
									domainKey = domainKey.Substring(1);
								domainKey = domainKey.ToLowerInvariant();
								DomainCookieStore dc = new DomainCookieStore();
								dc.Cookies = new List<PersistedCookie>(d.Value.Cookies);
								normalized.Domains[ domainKey ] = dc;
								total += dc.Cookies?.Count ?? 0;
							}
							_persistedStores[ kv.Key ] = normalized;
							clients++;
						}
						LoggerBridge.LogInformation("Migrated {ClientCount} clients with {TotalCookies} cookies from old format to new hierarchical format", clients, total);
						// Save migrated format
						SaveCookies();
						_lastSavedJson = File.Exists(_cookieFilePath) ? File.ReadAllText(_cookieFilePath).Trim() : null;
					}
				}
				catch
				{
					// ignore migration errors
				}
			}
		}
	}
}