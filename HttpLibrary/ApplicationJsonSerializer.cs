using System.Text.Json;

namespace HttpLibrary
{
	/// <summary>
	/// Source-generated JSON helper — uses ApplicationJsonContext exclusively.
	/// This library requires source generation for full AOT compatibility; runtime
	/// reflection-based deserialization is intentionally not supported.
	/// </summary>
	public static class ApplicationJsonSerializer
	{
		public static ApplicationSettingsRoot? DeserializeApplicationSettingsRoot(string json)
		{
			return JsonSerializer.Deserialize<ApplicationSettingsRoot>(json, ApplicationJsonContext.Default.ApplicationSettingsRoot);
		}
	}
}