using System;
using System.Collections.Generic;
using System.Net;

namespace HttpLibrary
{
	public interface ICookiePersistence
	{
		/// <summary>
		/// Initialize cookie persistence using the specified cookie file path.
		/// This will load persisted cookies and enable periodic saves and process-exit save handlers.
		/// </summary>
		/// <param name="cookieFilePath">Path to the cookie JSON file.</param>
		void Initialize(string cookieFilePath);

		/// <summary>
		/// Initialize cookie persistence in read-only mode using the specified cookie file path.
		/// </summary>
		void InitializeReadOnly(string cookieFilePath);

		void SetBaseAddress(string clientName, string baseAddress);
		void AddCookieFromHeader(string clientName, string setCookieHeader, Uri baseUri);
		void SaveCookies();
		CookieContainer? RegisterClient(string clientName, string? uri, bool persist);
		int CleanupOrphanedCookies(ISet<string> currentClientNames);
		bool HasPersistedEntry(string clientName);
		CookieContainer? GetContainer(string clientName);
		List<string> GetPersistedClientNames();
		List<PersistedCookie> GetPersistedCookies(string clientName);
		void PruneExpired(string clientName);
		void PruneAll();

		// New programmatic cookie manipulation API
		void AddCookie(string clientName, string domain, string? path, string name, string value, DateTime? expiresUtc, bool secure, bool httpOnly, string? sameSite);
		bool RemoveCookie(string clientName, string domain, string? path, string name);
		bool ModifyCookieValue(string clientName, string domain, string? path, string name, string newValue);
		int RemoveSessionCookies(string clientName);
	}
}