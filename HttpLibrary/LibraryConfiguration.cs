using Serilog;

using System;
using System.IO;
using System.Text.Json;

namespace HttpLibrary
{
	public sealed class LibraryFilePaths
	{
		public string? DefaultsConfigFile { get; set; }
		public string? ClientsConfigFile { get; set; }
		public string? CookiesFile { get; set; }
		public string? ApplicationConfigFile { get; set; }
	}

	public static class LibraryConfigurationProvider
	{
		public static LibraryFilePaths Paths => LoadPathsFromDirectory(null);

		/// <summary>
		/// Load library file paths from the specified base directory. If <paramref name="baseDirectory"/> is null,
		/// the application base directory is used. This method is internal to allow unit tests (via InternalsVisibleTo)
		/// to exercise missing-file behavior without forcing static initialization.
		/// </summary>
		internal static LibraryFilePaths LoadPathsFromDirectory(string? baseDirectory)
		{
			LibraryFilePaths paths = new LibraryFilePaths
			{
				DefaultsConfigFile = Constants.DefaultsConfigFile,
				ClientsConfigFile = Constants.ClientsConfigFile,
				CookiesFile = Constants.CookiesFile,
				ApplicationConfigFile = Constants.ApplicationConfigFile
			};

			try
			{
				string baseDir = baseDirectory ?? AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
				string filePath = Path.Combine(baseDir, Constants.LibraryConfigurationFile);
				if(File.Exists(filePath))
				{
					string txt = File.ReadAllText(filePath);
					LibraryFilePaths? loaded = JsonSerializer.Deserialize<HttpLibrary.LibraryFilePaths>(txt, HttpLibraryJsonContext.Default.LibraryFilePaths);
					if(loaded != null)
					{
						if(!string.IsNullOrWhiteSpace(loaded.DefaultsConfigFile))
							paths.DefaultsConfigFile = loaded.DefaultsConfigFile;
						if(!string.IsNullOrWhiteSpace(loaded.ClientsConfigFile))
							paths.ClientsConfigFile = loaded.ClientsConfigFile;
						if(!string.IsNullOrWhiteSpace(loaded.CookiesFile))
							paths.CookiesFile = loaded.CookiesFile;
						if(!string.IsNullOrWhiteSpace(loaded.ApplicationConfigFile))
							paths.ApplicationConfigFile = loaded.ApplicationConfigFile;
					}
				}
			}
			catch(Exception ex)
			{
				try
				{ Log.Warning(ex, "Failed to load {ConfigFile} from application base directory", Constants.LibraryConfigurationFile); }
				catch { }
			}

			return paths;
		}
	}
}