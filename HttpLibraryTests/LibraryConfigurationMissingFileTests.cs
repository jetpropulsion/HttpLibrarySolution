using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;

namespace HttpLibraryTests
{
	[TestClass]
	public class LibraryConfigurationMissingFileTests
	{
		private string? _tempDir;

		[TestInitialize]
		public void Initialize()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
		}

		[TestCleanup]
		public void Cleanup()
		{
			try
			{
				if(_tempDir != null && Directory.Exists(_tempDir))
				{
					Directory.Delete(_tempDir, recursive: true);
				}
			}
			catch
			{
				// ignore
			}
		}

		[TestMethod]
		public void LoadPathsFromDirectory_WhenConfigMissing_UsesDefaults()
		{
			// Arrange - do not create HttpLibraryConfiguration.json in temp dir

			// Act
			LibraryFilePaths paths = LibraryConfigurationProvider.LoadPathsFromDirectory(_tempDir);

			// Assert - should return defaults
			Assert.IsNotNull(paths);
			Assert.AreEqual("defaults.json", paths.DefaultsConfigFile);
			Assert.AreEqual("clients.json", paths.ClientsConfigFile);
			Assert.AreEqual("cookies.json", paths.CookiesFile);
		}
	}
}