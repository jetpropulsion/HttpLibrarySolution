using System;
using System.Collections.Generic;

namespace HttpLibrary
{
	/// <summary>
	/// Represents cookies for a specific domain within a client's cookie store.
	/// This is the 3rd level in the hierarchy: Client -> Domain -> DomainCookieStore
	/// </summary>
	public sealed class DomainCookieStore
	{
		/// <summary>
		/// List of cookies for this domain
		/// </summary>
		public List<PersistedCookie> Cookies { get; set; } = new List<PersistedCookie>();
	}

	/// <summary>
	/// Represents all cookies for a specific client, organized by domain.
	/// This is the 2nd level in the hierarchy: Client -> ClientCookieStore
	/// </summary>
	public sealed class ClientCookieStore
	{
		/// <summary>
		/// Dictionary of domain -> cookies for that domain
		/// Key is the domain (e.g., ".example.com", "example.com")
		/// Value is the DomainCookieStore containing all cookies for that domain
		/// </summary>
		public Dictionary<string, DomainCookieStore> Domains { get; set; } = new Dictionary<string, DomainCookieStore>(StringComparer.OrdinalIgnoreCase);
	}
}