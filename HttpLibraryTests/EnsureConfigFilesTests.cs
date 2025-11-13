using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace HttpLibraryTests
{
	[TestClass]
	public class EnsureConfigFilesTests
	{
		private string _tempDir = string.Empty;

		[TestInitialize]
		public void Init()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), "httplibrary_test_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
		}

		[TestCleanup]
		public void Cleanup()
		{
			try
			{
				if(Directory.Exists(_tempDir))
					Directory.Delete(_tempDir, true);
			}
			catch { }
		}

		[TestMethod]
		public void EnsureConfigurationFilesExist_CreatesFiles_WhenMissing()
		{
			// Arrange: create a custom LibraryFilePaths JSON in the temp dir
			string libConfigPath = Path.Combine(_tempDir, HttpLibrary.Constants.LibraryConfigurationFile);
			var paths = new HttpLibrary.LibraryFilePaths
			{
				DefaultsConfigFile = "defaults.test.json",
				ClientsConfigFile = "clients.test.json",
				CookiesFile = "cookies.test.json",
				ApplicationConfigFile = "app.test.json"
			};

			File.WriteAllText(libConfigPath, JsonSerializer.Serialize(paths, HttpLibrary.HttpLibraryJsonContext.Default.LibraryFilePaths));

			// Act: load provider with test base dir and invoke EnsureConfigurationFilesExist via InitializeServices indirect behavior
			HttpLibrary.LibraryFilePaths loaded = HttpLibrary.LibraryConfigurationProvider.LoadPathsFromDirectory(_tempDir);

			// Make sure expected files don't exist yet
			string defaultsPath = Path.Combine(_tempDir, loaded.DefaultsConfigFile ?? HttpLibrary.Constants.DefaultsConfigFile);
			string clientsPath = Path.Combine(_tempDir, loaded.ClientsConfigFile ?? HttpLibrary.Constants.ClientsConfigFile);
			string cookiesPath = Path.Combine(_tempDir, loaded.CookiesFile ?? HttpLibrary.Constants.CookiesFile);
			string appPath = Path.Combine(_tempDir, loaded.ApplicationConfigFile ?? "application.json");

			if(File.Exists(defaultsPath))
				File.Delete(defaultsPath);
			if(File.Exists(clientsPath))
				File.Delete(clientsPath);
			if(File.Exists(cookiesPath))
				File.Delete(cookiesPath);
			if(File.Exists(appPath))
				File.Delete(appPath);

			string dropPath = Path.Combine(AppContext.BaseDirectory, HttpLibrary.Constants.LibraryConfigurationFile);
			string? backup = null;
			if(File.Exists(dropPath))
			{
				backup = Path.Combine(Path.GetTempPath(), "httplib_backup_" + Guid.NewGuid().ToString("N"));
				File.Copy(dropPath, backup);
			}

			try
			{
				File.Copy(libConfigPath, dropPath, overwrite: true);

				// Now call InitializeServices which will call EnsureConfigurationFilesExist and create files in AppContext.BaseDirectory
				_ = HttpLibrary.ServiceConfiguration.InitializeServices(out ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> names, out ConcurrentDictionary<string, Uri> ba);

				// Assert: files created in AppContext.BaseDirectory (but named per our test config)
				string createdDefaults = Path.Combine(AppContext.BaseDirectory, loaded.DefaultsConfigFile ?? HttpLibrary.Constants.DefaultsConfigFile);
				string createdClients = Path.Combine(AppContext.BaseDirectory, loaded.ClientsConfigFile ?? HttpLibrary.Constants.ClientsConfigFile);
				string createdCookies = Path.Combine(AppContext.BaseDirectory, loaded.CookiesFile ?? HttpLibrary.Constants.CookiesFile);
				string createdApp = Path.Combine(AppContext.BaseDirectory, loaded.ApplicationConfigFile ?? "application.json");

				Assert.IsTrue(File.Exists(createdDefaults), "defaults file should be created");
				Assert.IsTrue(File.Exists(createdClients), "clients file should be created");
				Assert.IsTrue(File.Exists(createdCookies), "cookies file should be created");
				Assert.IsTrue(File.Exists(createdApp), "application file should be created");
			}
			finally
			{
				// restore backup
				try
				{
					if(backup != null)
					{
						File.Copy(backup, dropPath, overwrite: true);
						File.Delete(backup);
					}
					else
					{
						File.Delete(dropPath);
					}
				}
				catch { }
			}
		}
	}
}