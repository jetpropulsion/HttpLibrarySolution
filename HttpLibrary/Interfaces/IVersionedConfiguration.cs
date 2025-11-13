using System.Collections.Generic;

namespace HttpLibrary
{
	/// <summary>
	/// Common interface for versioned configuration with option definitions
	/// </summary>
	public interface IVersionedConfiguration
	{
		/// <summary>
		/// Gets the configuration version as an integer
		/// </summary>
		/// <returns>Configuration version (e.g., 1, 2, 3)</returns>
		int GetVersion();

		/// <summary>
		/// Gets the option definitions for this configuration version
		/// </summary>
		/// <returns>Dictionary of option definitions keyed by option name (case-insensitive)</returns>
		Dictionary<string, ConfigOptionDefinition> GetOptionDefinitions();

		/// <summary>
		/// Validates the entire configuration against its option definitions
		/// </summary>
		/// <returns>True if valid, false otherwise</returns>
		bool Validate();
	}
}