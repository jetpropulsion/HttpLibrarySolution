using System.Collections.Generic;

namespace HttpLibrary
{
	/// <summary>
	/// Represents the root object for clients.json used by the configuration loader.
	/// </summary>
	public sealed class ClientsFile
	{
		public int ConfigVersion { get; set; } = 1;
		public List<HttpClientConfig> Clients { get; set; } = new List<HttpClientConfig>();
	}
}