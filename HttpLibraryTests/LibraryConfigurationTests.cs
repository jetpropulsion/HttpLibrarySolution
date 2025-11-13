using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;

namespace HttpLibraryTests
{
	[TestClass]
	public class LibraryConfigurationTests
	{
		private string? _configPath;

		[TestInitialize]
		public void Initialize()
		{
			_configPath = Path.Combine(AppContext.BaseDirectory, "HttpLibraryConfiguration.json");

			// Ensure no pre-existing file interferes
			if(File.Exists(_configPath))
			{
				File.Delete(_configPath);
			}
		}

		[TestCleanup]
		public void Cleanup()
		{
			try
			{
				if(_configPath != null && File.Exists(_configPath))
				{
					File.Delete(_configPath);
				}
			}
			catch
			{
				// ignore cleanup failures
			}
		}

		[TestMethod]
		public void LoadsValuesFrom_HttpLibraryConfigurationJson()
		{
			// Arrange - create a configuration file in the application base directory BEFORE the static type is accessed
			string json = "{\n \"DefaultsConfigFile\": \"mydefaults.json\",\n \"ClientsConfigFile\": \"myclients.json\",\n \"CookiesFile\": \"mycookies.json\"\n}\n";
			File.WriteAllText(_configPath!, json);

			// Act - read directly from file using the internal loader to avoid static-caching issues
			LibraryFilePaths paths = LibraryConfigurationProvider.LoadPathsFromDirectory(null);

			// Assert
			Assert.IsNotNull(paths);
			Assert.AreEqual("mydefaults.json", paths.DefaultsConfigFile);
			Assert.AreEqual("myclients.json", paths.ClientsConfigFile);
			Assert.AreEqual("mycookies.json", paths.CookiesFile);
		}
	}
}