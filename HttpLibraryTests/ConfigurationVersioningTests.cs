using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for configuration file loading and versioning
	/// </summary>
	[TestClass]
	public class ConfigurationVersioningTests
	{
		[TestMethod]
		public void HttpClientConfig_Version1_HasCorrectVersion()
		{
			// Arrange & Act
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(1, config.GetVersion());
			Assert.AreEqual(1, config.ConfigVersion);
		}

		[TestMethod]
		public void HttpClientConfig_Validate_WithValidConfig_ReturnsTrue()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				ConfigVersion = 1,
				MaxConnectionsPerServer = 100,
				MaxRedirections = 10
			};

			// Act
			bool result = config.Validate();

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void HttpClientConfig_Validate_WithInvalidVersion_ReturnsFalse()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				ConfigVersion = 0
			};

			// Act
			bool result = config.Validate();

			// Assert
			Assert.IsFalse(result, "ConfigVersion 0 should be invalid");
		}

		[TestMethod]
		public void HttpClientConfig_Validate_WithInvalidMaxConnections_ReturnsFalse()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				ConfigVersion = 1,
				MaxConnectionsPerServer = 0
			};

			// Act
			bool result = config.Validate();

			// Assert
			Assert.IsFalse(result, "MaxConnectionsPerServer must be > 0");
		}

		[TestMethod]
		public void HttpClientConfig_Validate_WithNegativeMaxConnections_ReturnsFalse()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				ConfigVersion = 1,
				MaxConnectionsPerServer = -1
			};

			// Act
			bool result = config.Validate();

			// Assert
			Assert.IsFalse(result, "Negative MaxConnectionsPerServer should be invalid");
		}

		[TestMethod]
		public void HttpClientConfig_Validate_WithMaxRedirectionsAboveLimit_ReturnsFalse()
		{
			// Arrange - MaxRedirections limit is 50 per option definitions
			HttpClientConfig config = new HttpClientConfig
			{
				ConfigVersion = 1,
				MaxRedirections = 51
			};

			// Act
			bool result = config.Validate();

			// Assert
			Assert.IsFalse(result, "MaxRedirections above 50 should be invalid");
		}

		[TestMethod]
		public void HttpClientConfig_Validate_WithNegativeMaxRedirections_ReturnsFalse()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				ConfigVersion = 1,
				MaxRedirections = -1
			};

			// Act
			bool result = config.Validate();

			// Assert
			Assert.IsFalse(result, "Negative MaxRedirections should be invalid");
		}

		[TestMethod]
		public void HttpClientConfig_GetOptionDefinitions_ReturnsAllRequiredOptions()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Act
			System.Collections.Generic.Dictionary<string, ConfigOptionDefinition> definitions = config.GetOptionDefinitions();

			// Assert - check critical options exist in option definitions
			Assert.IsTrue(definitions.ContainsKey("ConfigVersion"));
			Assert.IsTrue(definitions.ContainsKey("DefaultRequestVersion"));
			Assert.IsTrue(definitions.ContainsKey("DefaultRequestHeaders"));
			Assert.IsTrue(definitions.ContainsKey("Timeout"));
			Assert.IsTrue(definitions.ContainsKey("MaxConnectionsPerServer"));
			Assert.IsTrue(definitions.ContainsKey("MaxRedirections"));
			Assert.IsTrue(definitions.ContainsKey("UseCookies"));
			Assert.IsTrue(definitions.ContainsKey("CookiePersistenceEnabled"));
			Assert.IsTrue(definitions.ContainsKey("DisableSslValidation"));
			Assert.IsTrue(definitions.ContainsKey("ClientCertificatePath"));
		}

		[TestMethod]
		public void HttpClientConfig_GetOptionDefinitions_HasCorrectCount()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Act
			System.Collections.Generic.Dictionary<string, ConfigOptionDefinition> definitions = config.GetOptionDefinitions();

			// Assert - HttpClientConfig should have 37 option definitions:
			// 1-ConfigVersion, 2-Name, 3-Uri, 4-DefaultRequestVersion, 5-DefaultRequestHeaders,
			// 6-Decompression, 7-PooledConnectionLifetime, 8-MaxConnectionsPerServer, 9-Timeout,
			// 10-ConnectTimeout, 11-Expect100ContinueTimeout, 12-ResponseDrainTimeout,
			// 13-KeepAlivePingDelay, 14-KeepAlivePingTimeout, 15-PooledConnectionIdleTimeout,
			// 16-InitialHttp2StreamWindowSize, 17-EnableMultipleHttp2Connections,
			// 18-EnableMultipleHttp3Connections, 19-AllowAutoRedirect, 20-MaxRedirections,
			// 21-UseCookies, 22-UseProxy, 23-UseSystemProxy, 24-HttpProxy, 25-HttpsProxy,
			// 26-ProxyUsername, 27-ProxyPassword, 28-ProxyBypassList,
			// 29-CookiePersistenceEnabled, 30-DisableSslValidation, 31-SslProtocols,
			// 32-MaxResponseHeadersLength, 33-MaxResponseDrainSize,
			// 34-ClientCertificatePath, 35-ClientCertificatePassword,
			// 36-ConnectCallback, 37-PlaintextStreamFilter
			// Note: ServerCertificateCustomValidationCallback and LocalCertificateSelectionCallback
			// are not serializable JSON properties, they are runtime-only callbacks
			Assert.AreEqual(37, definitions.Count, "HttpClientConfig should have exactly 37 option definitions");
		}

		[TestMethod]
		public void HttpClientConfig_DefaultRequestVersion_HasCorrectDefault()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();
			System.Collections.Generic.Dictionary<string, ConfigOptionDefinition> definitions = config.GetOptionDefinitions();

			// Act
			ConfigOptionDefinition versionDef = definitions[ "DefaultRequestVersion" ];

			// Assert
			Assert.AreEqual(new Version(2, 0), versionDef.DefaultValue, "DefaultRequestVersion should default to HTTP/2.0");
			Assert.IsFalse(versionDef.IsMandatory, "DefaultRequestVersion is optional in unified config");
		}

		string GetDefaultsJsonPath()
		{
			// Start from test assembly location and navigate to HttpLibraryCLI directory
			string? testDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			if(testDir == null)
			{
				throw new InvalidOperationException("Cannot determine test assembly location");
			}

			string solutionDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
			string defaultsPath = Path.Combine(solutionDir, "HttpLibraryCLI", "defaults.json");
			return defaultsPath;
		}

		[TestMethod]
		public void DefaultsJson_FileExists()
		{
			// Arrange
			string defaultsPath = GetDefaultsJsonPath();

			// Act & Assert
			Assert.IsTrue(File.Exists(defaultsPath),
								 $"defaults.json should exist at path: {defaultsPath}");
		}

		[TestMethod]
		public void DefaultsJson_IsValidJson()
		{
			// Arrange
			string defaultsPath = GetDefaultsJsonPath();
			string json = File.ReadAllText(defaultsPath);

			// Act - attempt to parse JSON
			JsonDocument? doc = null;
			try
			{
				doc = JsonDocument.Parse(json);
			}
			catch(JsonException ex)
			{
				Assert.Fail($"defaults.json contains invalid JSON: {ex.Message}");
			}
			finally
			{
				doc?.Dispose();
			}

			// Assert
			Assert.IsNotNull(doc, "defaults.json should be valid JSON");
		}

		[TestMethod]
		public void DefaultsJson_HasAllRequiredProperties()
		{
			// Arrange
			string defaultsPath = GetDefaultsJsonPath();
			string json = File.ReadAllText(defaultsPath);

			HttpClientConfig config = new HttpClientConfig();
			System.Collections.Generic.Dictionary<string, ConfigOptionDefinition> definitions = config.GetOptionDefinitions();

			using(JsonDocument doc = JsonDocument.Parse(json))
			{
				JsonElement root = doc.RootElement;

				// Act & Assert - verify all mandatory properties exist in JSON
				foreach(System.Collections.Generic.KeyValuePair<string, ConfigOptionDefinition> kvp in definitions)
				{
					if(kvp.Value.IsMandatory)
					{
						Assert.IsTrue(root.TryGetProperty(kvp.Key, out _),
												$"defaults.json is missing mandatory property: {kvp.Key}");
					}
				}
			}
		}

		[TestMethod]
		public void DefaultsJson_ConfigVersionIs1()
		{
			// Arrange
			string defaultsPath = GetDefaultsJsonPath();
			string json = File.ReadAllText(defaultsPath);

			using(JsonDocument doc = JsonDocument.Parse(json))
			{
				JsonElement root = doc.RootElement;

				// Act
				bool hasConfigVersion = root.TryGetProperty("ConfigVersion", out JsonElement configVersionElement);
				int configVersion = hasConfigVersion ? configVersionElement.GetInt32() : -1;

				// Assert
				Assert.IsTrue(hasConfigVersion, "defaults.json should have ConfigVersion property");
				Assert.AreEqual(1, configVersion, "defaults.json ConfigVersion should be 1");
			}
		}

		[TestMethod]
		public void DefaultsJson_HasNoExtraProperties()
		{
			// Arrange
			string defaultsPath = GetDefaultsJsonPath();
			string json = File.ReadAllText(defaultsPath);

			HttpClientConfig config = new HttpClientConfig();
			System.Collections.Generic.Dictionary<string, ConfigOptionDefinition> definitions = config.GetOptionDefinitions();

			using(JsonDocument doc = JsonDocument.Parse(json))
			{
				JsonElement root = doc.RootElement;

				// Act - find properties in JSON that aren't in option definitions
				System.Collections.Generic.List<string> extraProperties = new System.Collections.Generic.List<string>();

				foreach(JsonProperty property in root.EnumerateObject())
				{
					if(!definitions.ContainsKey(property.Name))
					{
						extraProperties.Add(property.Name);
					}
				}

				// Assert - no extra properties should exist
				if(extraProperties.Count > 0)
				{
					Assert.Fail($"defaults.json contains {extraProperties.Count} unexpected properties: {string.Join(", ", extraProperties)}");
				}
			}
		}

		[TestMethod]
		public void DefaultsJson_PropertyTypesMatchDefinitions()
		{
			// Arrange
			string defaultsPath = GetDefaultsJsonPath();
			string json = File.ReadAllText(defaultsPath);

			HttpClientConfig config = new HttpClientConfig();
			System.Collections.Generic.Dictionary<string, ConfigOptionDefinition> definitions = config.GetOptionDefinitions();

			using(JsonDocument doc = JsonDocument.Parse(json))
			{
				JsonElement root = doc.RootElement;

				// Act & Assert - verify property types match expectations
				foreach(System.Collections.Generic.KeyValuePair<string, ConfigOptionDefinition> kvp in definitions)
				{
					if(!root.TryGetProperty(kvp.Key, out JsonElement propertyElement))
					{
						continue; // Skip if property doesn't exist (already checked in other test)
					}

					// Check based on expected type
					if(kvp.Value.OptionType == typeof(int) || kvp.Value.OptionType == typeof(int?))
					{
						Assert.IsTrue(propertyElement.ValueKind == JsonValueKind.Number,
							 $"Property '{kvp.Key}' should be a number in defaults.json");
					}
					else if(kvp.Value.OptionType == typeof(bool) || kvp.Value.OptionType == typeof(bool?))
					{
						Assert.IsTrue(propertyElement.ValueKind == JsonValueKind.True ||
				 propertyElement.ValueKind == JsonValueKind.False,
				$"Property '{kvp.Key}' should be a boolean in defaults.json");
					}
					else if(kvp.Value.OptionType == typeof(string))
					{
						Assert.IsTrue(propertyElement.ValueKind == JsonValueKind.String ||
							 propertyElement.ValueKind == JsonValueKind.Null,
								 $"Property '{kvp.Key}' should be a string or null in defaults.json");
					}
				}
			}
		}

		[TestMethod]
		public void DefaultsJson_CanDeserializeSuccessfully()
		{
			// Arrange
			string defaultsPath = GetDefaultsJsonPath();
			string json = File.ReadAllText(defaultsPath);

			// Act - attempt to load using ConfigurationLoader
			HttpClientConfig? config = null;
			try
			{
				config = ConfigurationLoader.LoadDefaultClientConfig(json, defaultsPath);
			}
			catch(Exception ex)
			{
				Assert.Fail($"Failed to deserialize defaults.json: {ex.Message}");
			}

			// Assert
			Assert.IsNotNull(config, "Deserialized config should not be null");
			Assert.AreEqual(1, config.ConfigVersion, "Deserialized config should have ConfigVersion = 1");
		}

		[TestMethod]
		public void DefaultsJson_ValidatesSuccessfully()
		{
			// Arrange
			string defaultsPath = GetDefaultsJsonPath();
			string json = File.ReadAllText(defaultsPath);
			HttpClientConfig config = ConfigurationLoader.LoadDefaultClientConfig(json, defaultsPath);

			// Act
			bool isValid = config.Validate();

			// Assert
			Assert.IsTrue(isValid, "defaults.json should deserialize to a valid HttpClientConfig");
		}

	}
}