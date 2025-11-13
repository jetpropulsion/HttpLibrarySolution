using System;

namespace HttpLibrary
{
	public sealed class PersistedCookie
	{
		public string Name { get; set; } = string.Empty;
		public string Value { get; set; } = string.Empty;
		public string Domain { get; set; } = string.Empty;
		public string? Path { get; set; }
		public DateTime? Expires { get; set; }

		// RFC6265 Security Attributes
		public bool Secure { get; set; }
		public bool HttpOnly { get; set; }

		// RFC6265bis (draft) - SameSite attribute
		// Valid values: "Strict", "Lax", "None", or null (not specified)
		public string? SameSite { get; set; }

		// Internal flag indicating whether this cookie is a session cookie (no Expires)
		public bool IsSession { get; set; }
	}
}