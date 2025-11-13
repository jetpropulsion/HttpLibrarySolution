using System;
using System.IO;
using System.Text.Json;

namespace HttpLibrary
{
	internal static class ApplicationConfigurationLoader
	{
		public static ApplicationConfiguration? Load(string configPath)
		{
			if(string.IsNullOrWhiteSpace(configPath))
				throw new ArgumentException("configPath is required", nameof(configPath));

			// Try provided path first
			string? resolved = ResolvePath(configPath);
			if(resolved == null)
			{
				LoggerBridge.LogWarning("Application configuration file not found: {Path}", configPath);
				return null;
			}

			try
			{
				string text = File.ReadAllText(resolved);
				ApplicationConfiguration? config = JsonSerializer.Deserialize<ApplicationConfiguration>(text, ApplicationJsonContext.Default.ApplicationConfiguration);
				LoggerBridge.LogInformation("Loaded application configuration from: {Path}", resolved);
				return config;
			}
			catch(JsonException)
			{
				LoggerBridge.LogWarning("Invalid application configuration format in: {Path}", resolved);
				return null;
			}
			catch(Exception ex)
			{
				LoggerBridge.LogError(ex, "Failed to load application configuration from: {Path}", resolved);
				return null;
			}
		}

		private static string? ResolvePath(string fileName)
		{
			if(File.Exists(fileName))
				return fileName;

			// Try application base directory
			string appBasePath = Path.Combine(AppContext.BaseDirectory, fileName);
			if(File.Exists(appBasePath))
				return appBasePath;

			// Try current working directory
			string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
			if(File.Exists(cwdPath))
				return cwdPath;

			return null;
		}
	}
}