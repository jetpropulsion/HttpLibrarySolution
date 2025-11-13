using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HttpLibraryTests
{
	[TestClass]
	public class CookieMigrationTests
	{
		private string? testCookieFile;

		[TestInitialize]
		public void Initialize()
		{
			testCookieFile = Path.GetTempFileName();
			File.WriteAllText(testCookieFile, "{}");
			CookiePersistence.Reset();
		}

		[TestCleanup]
		public void Cleanup()
		{
			CookiePersistence.Reset();
			if(testCookieFile != null && File.Exists(testCookieFile))
			{
				File.Delete(testCookieFile);
			}
		}

		[TestMethod]
		public void LoadOldFlatFormat_MigratesSuccessfully()
		{
			// Arrange - old format: Dictionary<string, List<PersistedCookie>>
			string oldFormatJson = @"{
				""testClient"": [
					{
						""Name"": ""sessionId"",
						""Value"": ""abc123"",
						""Domain"": ""example.com"",
						""Path"": ""/"",
						""Expires"": null,
						""Secure"": false,
						""HttpOnly"": false,
						""SameSite"": null
					},
					{
						""Name"": ""userId"",
						""Value"": ""user456"",
						""Domain"": ""example.com"",
						""Path"": ""/api"",
						""Expires"": ""2030-01-01T00:00:00Z"",
						""Secure"": true,
						""HttpOnly"": true,
						""SameSite"": ""Strict""
					}
				]
			}";

			File.WriteAllText(testCookieFile!, oldFormatJson);

			// Act
			CookiePersistence.Initialize(testCookieFile!);

			// Assert - should load successfully
			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies("testClient");
			Assert.AreEqual(2, cookies.Count);

			// Verify cookie data preserved
			PersistedCookie? sessionCookie = cookies.FirstOrDefault(c => c.Name == "sessionId");
			PersistedCookie? userCookie = cookies.FirstOrDefault(c => c.Name == "userId");

			Assert.IsNotNull(sessionCookie);
			Assert.AreEqual("abc123", sessionCookie.Value);
			Assert.AreEqual("example.com", sessionCookie.Domain);

			Assert.IsNotNull(userCookie);
			Assert.AreEqual("user456", userCookie.Value);
			Assert.IsTrue(userCookie.Secure);
			Assert.IsTrue(userCookie.HttpOnly);
			Assert.AreEqual("Strict", userCookie.SameSite);
		}

		[TestMethod]
		public void LoadNewHierarchicalFormat_LoadsCorrectly()
		{
			// Arrange - new format: Dictionary<string, ClientCookieStore>
			Dictionary<string, ClientCookieStore> newFormatData = new Dictionary<string, ClientCookieStore>
			{
				[ "testClient" ] = new ClientCookieStore
				{
					Domains = new Dictionary<string, DomainCookieStore>
					{
						[ "example.com" ] = new DomainCookieStore
						{
							Cookies = new List<PersistedCookie>
														{
																new PersistedCookie
																{
																		Name = "cookie1",
																		Value = "value1",
																		Domain = "example.com",
																		Path = "/",
																		Secure = true
																}
														}
						},
						[ "api.example.com" ] = new DomainCookieStore
						{
							Cookies = new List<PersistedCookie>
														{
																new PersistedCookie
																{
																		Name = "cookie2",
																		Value = "value2",
																		Domain = "api.example.com",
																		Path = "/api"
																}
														}
						}
					}
				}
			};

			string json = JsonSerializer.Serialize(newFormatData, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
			File.WriteAllText(testCookieFile!, json);

			// Act
			CookiePersistence.Initialize(testCookieFile!);

			// Assert
			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies("testClient");
			Assert.AreEqual(2, cookies.Count);

			// Verify both cookies from different domains loaded
			PersistedCookie? cookie1 = cookies.FirstOrDefault(c => c.Name == "cookie1");
			PersistedCookie? cookie2 = cookies.FirstOrDefault(c => c.Name == "cookie2");

			Assert.IsNotNull(cookie1);
			Assert.AreEqual("example.com", cookie1.Domain);

			Assert.IsNotNull(cookie2);
			Assert.AreEqual("api.example.com", cookie2.Domain);
		}

		[TestMethod]
		public void MigrateOldFormat_PreservesAllCookieAttributes()
		{
			// Arrange - old format with all attributes
			string oldFormatJson = @"{
				""testClient"": [
					{
						""Name"": ""fullCookie"",
						""Value"": ""fullValue"",
						""Domain"": ""secure.example.com"",
						""Path"": ""/secure"",
						""Expires"": ""2030-12-31T23:59:59Z"",
						""Secure"": true,
						""HttpOnly"": true,
						""SameSite"": ""Strict""
					}
				]
			}";

			File.WriteAllText(testCookieFile!, oldFormatJson);

			// Act
			CookiePersistence.Initialize(testCookieFile!);
			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies("testClient");

			// Assert - all attributes preserved
			Assert.AreEqual(1, cookies.Count);
			PersistedCookie cookie = cookies[ 0 ];

			Assert.AreEqual("fullCookie", cookie.Name);
			Assert.AreEqual("fullValue", cookie.Value);
			Assert.AreEqual("secure.example.com", cookie.Domain);
			Assert.AreEqual("/secure", cookie.Path);
			Assert.IsNotNull(cookie.Expires);
			Assert.IsTrue(cookie.Secure);
			Assert.IsTrue(cookie.HttpOnly);
			Assert.AreEqual("Strict", cookie.SameSite);
		}

		[TestMethod]
		public void MigrateOldFormat_GroupsByDomain()
		{
			// Arrange - old format with cookies from multiple domains
			string oldFormatJson = @"{
				""testClient"": [
					{
						""Name"": ""cookie1"",
						""Value"": ""value1"",
						""Domain"": ""domain1.com"",
						""Path"": ""/""
					},
					{
						""Name"": ""cookie2"",
						""Value"": ""value2"",
						""Domain"": ""domain1.com"",
						""Path"": ""/api""
					},
					{
						""Name"": ""cookie3"",
						""Value"": ""value3"",
						""Domain"": ""domain2.com"",
						""Path"": ""/""
					}
				]
			}";

			File.WriteAllText(testCookieFile!, oldFormatJson);

			// Act
			CookiePersistence.Initialize(testCookieFile!);

			// Trigger save to verify new format
			CookiePersistence.SaveCookies();

			// Assert - verify new hierarchical format in file
			string savedJson = File.ReadAllText(testCookieFile!);
			Dictionary<string, ClientCookieStore>? savedData = JsonSerializer.Deserialize(savedJson, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);

			Assert.IsNotNull(savedData);
			Assert.IsTrue(savedData.ContainsKey("testClient"));

			ClientCookieStore store = savedData[ "testClient" ];
			Assert.AreEqual(2, store.Domains.Count, "Should have 2 domains");

			// Verify domain grouping
			Assert.IsTrue(store.Domains.ContainsKey("domain1.com"));
			Assert.IsTrue(store.Domains.ContainsKey("domain2.com"));

			Assert.AreEqual(2, store.Domains[ "domain1.com" ].Cookies.Count, "domain1.com should have 2 cookies");
			Assert.AreEqual(1, store.Domains[ "domain2.com" ].Cookies.Count, "domain2.com should have 1 cookie");
		}

		[TestMethod]
		public void SaveAfterMigration_UsesNewFormat()
		{
			// Arrange - start with old format
			string oldFormatJson = @"{
				""testClient"": [
					{
						""Name"": ""testCookie"",
						""Value"": ""testValue"",
						""Domain"": ""example.com"",
						""Path"": ""/""
					}
				]
			}";

			File.WriteAllText(testCookieFile!, oldFormatJson);
			CookiePersistence.Initialize(testCookieFile!);

			// Act - save (should convert to new format)
			CookiePersistence.SaveCookies();

			// Assert - verify file contains new hierarchical format
			string savedJson = File.ReadAllText(testCookieFile!);

			// New format should have "domains" key (camelCase due to JsonNamingPolicy)
			Assert.IsTrue(savedJson.Contains("\"domains\""), "Saved format should contain 'domains' key (camelCase)");

			// Verify can deserialize as new format
			Dictionary<string, ClientCookieStore>? newData = JsonSerializer.Deserialize(savedJson, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
			Assert.IsNotNull(newData);
			Assert.IsTrue(newData.ContainsKey("testClient"));
		}

		[TestMethod]
		public void LoadCorruptedOldFormat_HandlesGracefully()
		{
			// Arrange - corrupted old format JSON
			string corruptedJson = @"{
				""testClient"": [
					{
						""Name"": ""cookie1"",
						""Value"": ""value1""
						// Missing closing braces
			";

			File.WriteAllText(testCookieFile!, corruptedJson);

			// Act & Assert - should not throw, just log warning
			CookiePersistence.Initialize(testCookieFile!);

			// No cookies should be loaded from corrupted file
			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies("testClient");
			Assert.AreEqual(0, cookies.Count);
		}

		[TestMethod]
		public void MigrationPreservesMultipleClients()
		{
			// Arrange - old format with multiple clients
			string oldFormatJson = @"{
				""client1"": [
					{
						""Name"": ""cookie1"",
						""Value"": ""value1"",
						""Domain"": ""domain1.com"",
						""Path"": ""/""
					}
				],
				""client2"": [
					{
						""Name"": ""cookie2"",
						""Value"": ""value2"",
						""Domain"": ""domain2.com"",
						""Path"": ""/""
					}
				]
			}";

			File.WriteAllText(testCookieFile!, oldFormatJson);

			// Act
			CookiePersistence.Initialize(testCookieFile!);

			// Assert - both clients preserved
			List<string> clientNames = CookiePersistence.GetPersistedClientNames();
			Assert.AreEqual(2, clientNames.Count);
			CollectionAssert.Contains(clientNames, "client1");
			CollectionAssert.Contains(clientNames, "client2");

			// Verify cookies for each client
			List<PersistedCookie> client1Cookies = CookiePersistence.GetPersistedCookies("client1");
			List<PersistedCookie> client2Cookies = CookiePersistence.GetPersistedCookies("client2");

			Assert.AreEqual(1, client1Cookies.Count);
			Assert.AreEqual(1, client2Cookies.Count);
		}

		[TestMethod]
		public void MigrationHandlesEmptyDomain()
		{
			// Arrange - old format with empty domain
			string oldFormatJson = @"{
				""testClient"": [
					{
						""Name"": ""cookie1"",
						""Value"": ""value1"",
						""Domain"": """",
						""Path"": ""/""
					}
				]
			}";

			File.WriteAllText(testCookieFile!, oldFormatJson);

			// Act
			CookiePersistence.Initialize(testCookieFile!);

			// Assert - should handle empty domain gracefully
			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies("testClient");
			Assert.AreEqual(1, cookies.Count);
			Assert.AreEqual("", cookies[ 0 ].Domain);
		}

		[TestMethod]
		public void NewFormatWithMultipleDomains_LoadsAllCookies()
		{
			// Arrange - new format with 3 domains
			Dictionary<string, ClientCookieStore> data = new Dictionary<string, ClientCookieStore>
			{
				[ "testClient" ] = new ClientCookieStore
				{
					Domains = new Dictionary<string, DomainCookieStore>
					{
						[ "domain1.com" ] = new DomainCookieStore
						{
							Cookies = new List<PersistedCookie>
														{
																new PersistedCookie { Name = "c1", Value = "v1", Domain = "domain1.com", Path = "/" }
														}
						},
						[ "domain2.com" ] = new DomainCookieStore
						{
							Cookies = new List<PersistedCookie>
														{
																new PersistedCookie { Name = "c2", Value = "v2", Domain = "domain2.com", Path = "/" },
																new PersistedCookie { Name = "c3", Value = "v3", Domain = "domain2.com", Path = "/api" }
														}
						},
						[ "domain3.com" ] = new DomainCookieStore
						{
							Cookies = new List<PersistedCookie>
														{
																new PersistedCookie { Name = "c4", Value = "v4", Domain = "domain3.com", Path = "/" }
														}
						}
					}
				}
			};

			string json = JsonSerializer.Serialize(data, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
			File.WriteAllText(testCookieFile!, json);

			// Act
			CookiePersistence.Initialize(testCookieFile!);

			// Assert
			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies("testClient");
			Assert.AreEqual(4, cookies.Count, "Should load all 4 cookies from 3 domains");
		}
	}
}