using System;
using System.Collections.Generic;
using System.Net;

namespace HttpLibrary
{
	public static class CookiePersistence
	{
		private static ICookiePersistence? _impl;

		// Ensure a default implementation is available for consumers and tests
		static CookiePersistence()
		{
			_impl = new CookiePersistenceImpl();
		}

		public static void SetImplementation(ICookiePersistence impl)
		{
			_impl = impl ?? throw new ArgumentNullException(nameof(impl));
		}

		public static void Initialize(string cookieFilePath)
		{
			_impl?.Initialize(cookieFilePath);
		}

		public static void InitializeReadOnly(string cookieFilePath)
		{
			_impl?.InitializeReadOnly(cookieFilePath);
		}

		public static void SetBaseAddress(string clientName, string baseAddress)
		{
			_impl?.SetBaseAddress(clientName, baseAddress);
		}

		public static void AddCookieFromHeader(string clientName, string setCookieHeader, Uri baseUri)
		{
			_impl?.AddCookieFromHeader(clientName, setCookieHeader, baseUri);
		}

		public static void SaveCookies()
		{
			_impl?.SaveCookies();
		}

		/// <summary>
		/// Register a client for cookie tracking. 'enabled' indicates whether cookie persistence is enabled for this client.
		/// </summary>
		public static CookieContainer? RegisterClient(string clientName, string? uri, bool enabled)
		{
			return _impl?.RegisterClient(clientName, uri, enabled);
		}

		public static int CleanupOrphanedCookies(ISet<string> currentClientNames)
		{
			return _impl?.CleanupOrphanedCookies(currentClientNames) ?? 0;
		}

		public static bool HasPersistedEntry(string clientName)
		{
			return _impl?.HasPersistedEntry(clientName) ?? false;
		}

		public static CookieContainer? GetContainer(string clientName)
		{
			return _impl?.GetContainer(clientName);
		}

		public static List<string> GetPersistedClientNames()
		{
			return _impl?.GetPersistedClientNames() ?? new List<string>();
		}

		public static List<PersistedCookie> GetPersistedCookies(string clientName)
		{
			return _impl?.GetPersistedCookies(clientName) ?? new List<PersistedCookie>();
		}

		public static void PruneExpired(string clientName)
		{
			_impl?.PruneExpired(clientName);
		}

		public static void PruneAll()
		{
			_impl?.PruneAll();
		}

		// New programmatic API wrappers
		/// <summary>
		/// Add a new cookie for the client.
		/// </summary>
		public static void AddCookie(string clientName, string domain, string? path, string name, string value, DateTime? expiresUtc, bool secure, bool httpOnly, string? sameSite)
		{
			_impl?.AddCookie(clientName, domain, path, name, value, expiresUtc, secure, httpOnly, sameSite);
		}

		/// <summary>
		/// Remove an existing cookie for the client.
		/// </summary>
		public static bool RemoveCookie(string clientName, string domain, string? path, string name)
		{
			return _impl?.RemoveCookie(clientName, domain, path, name) ?? false;
		}

		/// <summary>
		/// Modify the value of an existing cookie for the client.
		/// </summary>
		public static bool ModifyCookieValue(string clientName, string domain, string? path, string name, string newValue)
		{
			return _impl?.ModifyCookieValue(clientName, domain, path, name, newValue) ?? false;
		}

		// Housekeeping/test helpers
		public static void Reset()
		{
			if(_impl is CookiePersistenceImpl impl)
			{
				impl.Reset();
			}
		}

		public static int MigrateCookies(string oldClientName, string newClientName)
		{
			if(_impl is CookiePersistenceImpl impl)
			{
				return impl.MigrateCookies(oldClientName, newClientName);
			}
			return 0;
		}

		public static System.Collections.Generic.Dictionary<string, int> GenerateReport()
		{
			if(_impl is CookiePersistenceImpl impl)
			{
				return impl.GenerateReport();
			}
			return new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		}

		public static int RemoveAllCookiesForClient(string clientName)
		{
			if(_impl is CookiePersistenceImpl impl)
			{
				return impl.RemoveAllCookiesForClient(clientName);
			}
			return 0;
		}

		/// <summary>
		/// Remove all session cookies (cookies without an Expires) for the specified client.
		/// Returns the number of cookies removed.
		/// </summary>
		public static int RemoveSessionCookies(string clientName)
		{
			return _impl?.RemoveSessionCookies(clientName) ?? 0;
		}
	}
}