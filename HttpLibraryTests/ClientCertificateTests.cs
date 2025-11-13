using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for client certificate configuration including PFX/PKCS12 file handling and password management
	/// </summary>
	[TestClass]
	public class ClientCertificateTests
	{
		private string? testDirectory;

		[TestInitialize]
		public void Initialize()
		{
			// Create temporary directory for test certificate files
			testDirectory = Path.Combine(Path.GetTempPath(), $"HttpLibraryCertTests_{Guid.NewGuid():N}");
			Directory.CreateDirectory(testDirectory);
		}

		[TestCleanup]
		public void Cleanup()
		{
			// Clean up test directory
			if(testDirectory != null && Directory.Exists(testDirectory))
			{
				try
				{
					Directory.Delete(testDirectory, recursive: true);
				}
				catch
				{
					// Ignore cleanup errors
				}
			}
		}

		#region Default Configuration Tests

		[TestMethod]
		public void HttpClientConfig_ClientCertificatePath_DefaultIsNull()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsNull(config.ClientCertificatePath);
		}

		[TestMethod]
		public void HttpClientConfig_ClientCertificatePassword_DefaultIsNull()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsNull(config.ClientCertificatePassword);
		}

		[TestMethod]
		public void HttpClientConfig_ClientCertificate_MatchesDefaultsJson()
		{
			// This test verifies that the defaults.json values match expected configuration
			// defaults.json specifies: "ClientCertificatePath": null, "ClientCertificatePassword": null

			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsNull(config.ClientCertificatePath, "ClientCertificatePath should be null in defaults.json");
			Assert.IsNull(config.ClientCertificatePassword, "ClientCertificatePassword should be null in defaults.json");
		}

		#endregion

		#region Certificate Path Configuration Tests

		[TestMethod]
		public void HttpClientConfig_ClientCertificatePath_CanBeSet()
		{
			// Arrange
			string certPath = "C:\\certs\\client.pfx";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePath = certPath
			};

			// Assert
			Assert.AreEqual(certPath, config.ClientCertificatePath);
		}

		[TestMethod]
		public void ClientCertificatePath_RelativePath_Preserved()
		{
			// Arrange
			string relativePath = "certs\\client.pfx";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePath = relativePath
			};

			// Assert
			Assert.AreEqual(relativePath, config.ClientCertificatePath);
		}

		[TestMethod]
		public void ClientCertificatePath_AbsolutePath_Preserved()
		{
			// Arrange
			string absolutePath = "C:\\Program Files\\MyApp\\certs\\client.pfx";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePath = absolutePath
			};

			// Assert
			Assert.AreEqual(absolutePath, config.ClientCertificatePath);
		}

		[TestMethod]
		public void ClientCertificatePath_UnixPath_Preserved()
		{
			// Arrange
			string unixPath = "/var/www/certs/client.pfx";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePath = unixPath
			};

			// Assert
			Assert.AreEqual(unixPath, config.ClientCertificatePath);
		}

		[TestMethod]
		public void ClientCertificatePath_EmptyString_Accepted()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePath = ""
			};

			// Assert
			Assert.AreEqual("", config.ClientCertificatePath);
		}

		#endregion

		#region Certificate Password Configuration Tests

		[TestMethod]
		public void HttpClientConfig_ClientCertificatePassword_CanBeSet()
		{
			// Arrange
			string password = "SecureP@ssw0rd!";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePassword = password
			};

			// Assert
			Assert.AreEqual(password, config.ClientCertificatePassword);
		}

		[TestMethod]
		public void ClientCertificatePassword_SpecialCharacters_Preserved()
		{
			// Arrange
			string complexPassword = "P@$$w0rd!#%&*()_+-=[]{}|;:',.<>?/~`";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePassword = complexPassword
			};

			// Assert
			Assert.AreEqual(complexPassword, config.ClientCertificatePassword);
		}

		[TestMethod]
		public void ClientCertificatePassword_EmptyString_Accepted()
		{
			// Arrange - Empty password is valid (some PFX files have no password)
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePassword = ""
			};

			// Assert
			Assert.AreEqual("", config.ClientCertificatePassword);
		}

		[TestMethod]
		public void ClientCertificatePassword_Unicode_Preserved()
		{
			// Arrange
			string unicodePassword = "??????123";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePassword = unicodePassword
			};

			// Assert
			Assert.AreEqual(unicodePassword, config.ClientCertificatePassword);
		}

		#endregion

		#region File Type Tests

		[TestMethod]
		public void ClientCertificatePath_PfxExtension_Accepted()
		{
			// Arrange
			string pfxPath = "C:\\certs\\client.pfx";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePath = pfxPath
			};

			// Assert
			Assert.IsTrue(config.ClientCertificatePath!.EndsWith(".pfx"));
		}

		[TestMethod]
		public void ClientCertificatePath_P12Extension_Accepted()
		{
			// Arrange - P12 is another extension for PKCS#12 files
			string p12Path = "/etc/ssl/certs/client.p12";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePath = p12Path
			};

			// Assert
			Assert.IsTrue(config.ClientCertificatePath!.EndsWith(".p12"));
		}

		[TestMethod]
		public void ClientCertificatePath_NoExtension_Accepted()
		{
			// Arrange
			string noExtPath = "C:\\certs\\client-cert";
			HttpClientConfig config = new HttpClientConfig
			{
				ClientCertificatePath = noExtPath
			};

			// Assert
			Assert.IsFalse(config.ClientCertificatePath!.Contains("."));
		}

		#endregion

		#region Path Validation Tests

		[TestMethod]
		public void Path_Combine_WorksWithCertificatePaths()
		{
			// Arrange
			string baseDir = "C:\\certs";
			string fileName = "client.pfx";

			// Act
			string fullPath = Path.Combine(baseDir, fileName);

			// Assert
			Assert.AreEqual("C:\\certs\\client.pfx", fullPath);
		}

		[TestMethod]
		public void Path_GetExtension_WorksForPfx()
		{
			// Arrange
			string certPath = "C:\\certs\\client.pfx";

			// Act
			string extension = Path.GetExtension(certPath);

			// Assert
			Assert.AreEqual(".pfx", extension);
		}

		[TestMethod]
		public void Path_GetExtension_WorksForP12()
		{
			// Arrange
			string certPath = "/etc/ssl/certs/client.p12";

			// Act
			string extension = Path.GetExtension(certPath);

			// Assert
			Assert.AreEqual(".p12", extension);
		}

		[TestMethod]
		public void File_Exists_ChecksCertificatePath()
		{
			// Arrange
			string testCertPath = Path.Combine(testDirectory!, "test.pfx");

			// Act - File doesn't exist yet
			bool existsBefore = File.Exists(testCertPath);

			// Create the file
			File.WriteAllBytes(testCertPath, new byte[] { 0x30, 0x82 }); // Mock PFX header

			// Act - File exists now
			bool existsAfter = File.Exists(testCertPath);

			// Assert
			Assert.IsFalse(existsBefore);
			Assert.IsTrue(existsAfter);
		}

		#endregion

		#region Option Definitions Tests

		[TestMethod]
		public void HttpClientConfig_OptionDefinitions_ContainsCertificateOptions()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Act
			Dictionary<string, ConfigOptionDefinition> definitions = config.GetOptionDefinitions();

			// Assert
			Assert.IsTrue(definitions.ContainsKey("ClientCertificatePath"));
			Assert.IsTrue(definitions.ContainsKey("ClientCertificatePassword"));
		}

		[TestMethod]
		public void HttpClientConfig_CertificateOptions_AreOptional()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();
			Dictionary<string, ConfigOptionDefinition> definitions = config.GetOptionDefinitions();

			// Assert
			Assert.IsFalse(definitions[ "ClientCertificatePath" ].IsMandatory);
			Assert.IsFalse(definitions[ "ClientCertificatePassword" ].IsMandatory);
		}

		#endregion

		#region Validation Tests

		[TestMethod]
		public void HttpClientConfig_WithCertificatePath_ValidatesSuccessfully()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "certClient",
				Uri = "https://api.example.com",
				ClientCertificatePath = "C:\\certs\\client.pfx"
			};

			// Act
			bool isValid = config.Validate();

			// Assert
			Assert.IsTrue(isValid);
		}

		[TestMethod]
		public void HttpClientConfig_WithCertificatePathAndPassword_ValidatesSuccessfully()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "certClient",
				Uri = "https://api.example.com",
				ClientCertificatePath = "C:\\certs\\client.pfx",
				ClientCertificatePassword = "SecurePassword123!"
			};

			// Act
			bool isValid = config.Validate();

			// Assert
			Assert.IsTrue(isValid);
		}

		[TestMethod]
		public void HttpClientConfig_WithOnlyCertificatePath_ValidatesSuccessfully()
		{
			// Arrange - Password can be null (some PFX files don't require password)
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "certClient",
				Uri = "https://api.example.com",
				ClientCertificatePath = "C:\\certs\\client.pfx",
				ClientCertificatePassword = null
			};

			// Act
			bool isValid = config.Validate();

			// Assert
			Assert.IsTrue(isValid);
		}

		#endregion

		#region Integration Scenario Tests

		[TestMethod]
		public void CertificateScenario_MutualTlsAuthentication_ConfiguresCorrectly()
		{
			// Arrange - Typical mutual TLS (mTLS) scenario
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "mtlsClient",
				Uri = "https://secure-api.example.com",
				ClientCertificatePath = "C:\\certificates\\company-client.pfx",
				ClientCertificatePassword = "CertP@ssw0rd123!",
				DisableSslValidation = false // Always validate server certificates in production
			};

			// Assert
			Assert.AreEqual("C:\\certificates\\company-client.pfx", config.ClientCertificatePath);
			Assert.AreEqual("CertP@ssw0rd123!", config.ClientCertificatePassword);
			Assert.IsFalse(config.DisableSslValidation);
		}

		[TestMethod]
		public void CertificateScenario_UnixStylePath_ConfiguresCorrectly()
		{
			// Arrange - Unix/Linux style certificate path
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "linuxClient",
				Uri = "https://api.example.com",
				ClientCertificatePath = "/etc/pki/tls/certs/client.p12",
				ClientCertificatePassword = "LinuxCertPass"
			};

			// Assert
			Assert.AreEqual("/etc/pki/tls/certs/client.p12", config.ClientCertificatePath);
			Assert.IsTrue(config.ClientCertificatePath.StartsWith("/"));
		}

		[TestMethod]
		public void CertificateScenario_RelativePathForPortability_ConfiguresCorrectly()
		{
			// Arrange - Relative path for portable applications
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "portableClient",
				Uri = "https://api.example.com",
				ClientCertificatePath = "certs\\client.pfx",
				ClientCertificatePassword = "PortablePass"
			};

			// Assert
			Assert.AreEqual("certs\\client.pfx", config.ClientCertificatePath);
			Assert.IsFalse(Path.IsPathRooted(config.ClientCertificatePath));
		}

		[TestMethod]
		public void CertificateScenario_NoCertificate_ConfiguresCorrectly()
		{
			// Arrange - Standard HTTPS without client certificate
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "standardClient",
				Uri = "https://api.example.com",
				ClientCertificatePath = null,
				ClientCertificatePassword = null
			};

			// Assert
			Assert.IsNull(config.ClientCertificatePath);
			Assert.IsNull(config.ClientCertificatePassword);
		}

		#endregion

		#region SSL Validation Tests (Related)

		[TestMethod]
		public void HttpClientConfig_DisableSslValidation_DefaultIsFalse()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsFalse(config.DisableSslValidation.GetValueOrDefault(), "SSL validation should be enabled by default for security");
		}

		[TestMethod]
		public void DisableSslValidation_CanBeEnabled_ForDevelopment()
		{
			// Arrange - Only for development/testing, NEVER in production
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "devClient",
				Uri = "https://localhost:5001",
				DisableSslValidation = true
			};

			// Assert
			Assert.IsTrue(config.DisableSslValidation);
		}

		[TestMethod]
		public void CertificateWithSslValidation_BothConfigured_WorksTogether()
		{
			// Arrange - Client cert with server validation enabled (recommended)
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "secureClient",
				Uri = "https://secure-api.example.com",
				ClientCertificatePath = "C:\\certs\\client.pfx",
				ClientCertificatePassword = "SecurePass",
				DisableSslValidation = false
			};

			// Assert
			Assert.IsNotNull(config.ClientCertificatePath);
			Assert.IsFalse(config.DisableSslValidation);
		}

		#endregion
	}
}