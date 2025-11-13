using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpLibrary
{
	[JsonSourceGenerationOptions(
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
		AllowTrailingCommas = true)]
	[JsonSerializable(typeof(ApplicationSettingsRoot))]
	[JsonSerializable(typeof(ApplicationConfiguration))]
	[JsonSerializable(typeof(CertificatePinningConfig))]
	[JsonSerializable(typeof(CustomDnsConfig))]
	[JsonSerializable(typeof(SocketOptions))]
	[JsonSerializable(typeof(MutualTlsConfig))]
	[JsonSerializable(typeof(FileUploadConfig))]
	[JsonSerializable(typeof(ProgressDisplayConfig))]
	[JsonSerializable(typeof(HttpClientConfig))]
	[JsonSerializable(typeof(ClientsFile))]
	public partial class ApplicationJsonContext : JsonSerializerContext
	{
	}
}