using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpLibrary
{
	// Source-generated JSON context for AOT / trimming-safe serialization.
	[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
	[JsonSerializable(typeof(PooledHttpClientOptions))]
	[JsonSerializable(typeof(HttpClientConfig[]))]
	[JsonSerializable(typeof(HttpClientConfig))]
	[JsonSerializable(typeof(ClientsFile))]
	// Old flat cookie format
	[JsonSerializable(typeof(Dictionary<string, List<PersistedCookie>>))]
	// New hierarchical cookie format
	[JsonSerializable(typeof(Dictionary<string, ClientCookieStore>))]
	[JsonSerializable(typeof(ClientCookieStore))]
	[JsonSerializable(typeof(DomainCookieStore))]
	[JsonSerializable(typeof(Dictionary<string, DomainCookieStore>))]
	[JsonSerializable(typeof(List<PersistedCookie>))]
	[JsonSerializable(typeof(PersistedCookie))]
	// Support LibraryFilePaths used by LibraryConfigurationProvider
	[JsonSerializable(typeof(LibraryFilePaths))]
	public partial class HttpLibraryJsonContext : JsonSerializerContext
	{
	}
}