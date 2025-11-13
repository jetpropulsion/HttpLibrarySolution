using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace HttpLibraryTests
{
	[TestClass]
	public class CookiePersistenceTests
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
		public void Initialize_CreatesNewFile_WhenNotExists()
		{
			// Arrange & Act
			CookiePersistence.Initialize(testCookieFile!);

			// Assert - no exception thrown, initialization succeeded
		}

		[TestMethod]
		public void RegisterClient_ReturnsContainer_WhenEnabled()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			string baseAddress = "https://example.com";

			// Act
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseAddress, enabled: true);

			// Assert
			Assert.IsNotNull(container);
		}

		[TestMethod]
		public void RegisterClient_ReturnsNull_WhenDisabled()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			string baseAddress = "https://example.com";

			// Act
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseAddress, enabled: false);

			// Assert
			Assert.IsNull(container);
		}

		[TestMethod]
		public void GetContainer_ReturnsSameInstance_AfterRegistration()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			string baseAddress = "https://example.com";
			CookieContainer? registered = CookiePersistence.RegisterClient(clientName, baseAddress, enabled: true);

			// Act
			CookieContainer? retrieved = CookiePersistence.GetContainer(clientName);

			// Assert
			Assert.IsNotNull(retrieved);
			Assert.AreSame(registered, retrieved);
		}

		[TestMethod]
		public void SaveCookies_CreatesCookieFile()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, "https://example.com", enabled: true);

			// Act
			CookiePersistence.SaveCookies();

			// Assert
			Assert.IsTrue(File.Exists(testCookieFile!));
		}

		[TestMethod]
		public void AddCookieFromHeader_ParsesBasicCookie()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);
			string setCookieHeader = "sessionId=abc123; Path=/; Domain=example.com";

			// Act
			CookiePersistence.AddCookieFromHeader(clientName, setCookieHeader, baseUri);

			// Assert
			Assert.IsNotNull(container);
			CookieCollection cookies = container.GetCookies(baseUri);
			Assert.IsNotEmpty(cookies);
			Cookie? cookie = cookies.Cast<Cookie>().FirstOrDefault(c => c.Name == "sessionId");
			Assert.IsNotNull(cookie);
			Assert.AreEqual("abc123", cookie.Value);
		}

		[TestMethod]
		public void LoadPersistedCookies_RestoresFromFile()
		{
			// Arrange - create a cookie file manually in new hierarchical format
			Dictionary<string, ClientCookieStore> testData = new Dictionary<string, ClientCookieStore>
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
																		Name = "testCookie",
																		Value = "testValue",
																		Domain = "example.com",
																		Path = "/",
																		Expires = DateTime.UtcNow.AddDays(1)
																}
														}
						}
					}
				}
			};
			string json = JsonSerializer.Serialize(testData, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
			File.WriteAllText(testCookieFile!, json);

			// Act
			CookiePersistence.Initialize(testCookieFile!);
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies("testClient");

			// Assert
			Assert.HasCount(1, persisted);
			Assert.AreEqual("testCookie", persisted[ 0 ].Name);
			Assert.AreEqual("testValue", persisted[ 0 ].Value);
		}

		[TestMethod]
		public void HasPersistedEntry_ReturnsTrueForExistingClient()
		{
			// Arrange - use new hierarchical format
			Dictionary<string, ClientCookieStore> testData = new Dictionary<string, ClientCookieStore>
			{
				[ "existingClient" ] = new ClientCookieStore
				{
					Domains = new Dictionary<string, DomainCookieStore>()
				}
			};
			string json = JsonSerializer.Serialize(testData, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
			File.WriteAllText(testCookieFile!, json);
			CookiePersistence.Initialize(testCookieFile!);

			// Act
			bool hasEntry = CookiePersistence.HasPersistedEntry("existingClient");

			// Assert
			Assert.IsTrue(hasEntry);
		}

		[TestMethod]
		public void HasPersistedEntry_ReturnsFalseForNonExistingClient()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);

			// Act
			bool hasEntry = CookiePersistence.HasPersistedEntry("nonExistingClient");

			// Assert
			Assert.IsFalse(hasEntry);
		}

		[TestMethod]
		public void GetPersistedClientNames_ReturnsAllClients()
		{
			// Arrange - use new hierarchical format
			Dictionary<string, ClientCookieStore> testData = new Dictionary<string, ClientCookieStore>
			{
				[ "client1" ] = new ClientCookieStore { Domains = new Dictionary<string, DomainCookieStore>() },
				[ "client2" ] = new ClientCookieStore { Domains = new Dictionary<string, DomainCookieStore>() }
			};
			string json = JsonSerializer.Serialize(testData, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
			File.WriteAllText(testCookieFile!, json);
			CookiePersistence.Initialize(testCookieFile!);

			// Act
			List<string> clientNames = CookiePersistence.GetPersistedClientNames();

			// Assert
			Assert.HasCount(2, clientNames);
			CollectionAssert.Contains(clientNames, "client1");
			CollectionAssert.Contains(clientNames, "client2");
		}

		[TestMethod]
		public void PruneExpired_RemovesExpiredCookies()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Add an expired cookie
			Cookie expiredCookie = new Cookie("expired", "value", "/", "example.com")
			{
				Expires = DateTime.UtcNow.AddDays(-1)
			};
			container?.Add(baseUri, expiredCookie);

			// Act
			CookiePersistence.PruneExpired(clientName);

			// Assert
			CookieCollection cookies = container?.GetCookies(baseUri) ?? new CookieCollection();
			Cookie? found = cookies.Cast<Cookie>().FirstOrDefault(c => c.Name == "expired");
			// Note: CookieContainer automatically ignores expired cookies in GetCookies
			Assert.IsNull(found);
		}

		[TestMethod]
		public void AddCookieFromHeader_SkipsExpiredCookie()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://google.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// This is the actual Set-Cookie header from Google that has an intentionally expired date
			string setCookieHeader = "__Secure-STRP=; expires=Mon, 01-Jan-1990 00:00:00 GMT; path=/; domain=.google.com; Secure";

			// Act
			CookiePersistence.AddCookieFromHeader(clientName, setCookieHeader, baseUri);

			// Force save to file
			CookiePersistence.SaveCookies();

			// Assert - cookie should NOT be in the persisted list
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			PersistedCookie? expiredCookie = persisted.FirstOrDefault(c => c.Name == "__Secure-STRP");
			Assert.IsNull(expiredCookie, "Expired cookie should not be persisted");

			// Also verify it's not in the JSON file (now using new hierarchical format)
			if(File.Exists(testCookieFile!))
			{
				string json = File.ReadAllText(testCookieFile!);
				Dictionary<string, ClientCookieStore>? data = JsonSerializer.Deserialize(json, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
				if(data != null && data.TryGetValue(clientName, out ClientCookieStore? store))
				{
					// Check all domains for the cookie
					bool foundInFile = false;
					foreach(KeyValuePair<string, DomainCookieStore> domainKv in store.Domains)
					{
						PersistedCookie? foundCookie = domainKv.Value.Cookies.FirstOrDefault(c => c.Name == "__Secure-STRP");
						if(foundCookie != null)
						{
							foundInFile = true;
							break;
						}
					}
					Assert.IsFalse(foundInFile, "Expired cookie should not be in JSON file");
				}
			}
		}

		[TestMethod]
		public void AddCookieFromHeader_RemovesExistingExpiredCookie()
		{
			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://google.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// First, add a valid cookie
			string validCookieHeader = "__Secure-STRP=validValue; expires=Thu, 01-Jan-2030 00:00:00 GMT; path=/; domain=.google.com; Secure";
			CookiePersistence.AddCookieFromHeader(clientName, validCookieHeader, baseUri);

			// Verify it was added
			List<PersistedCookie> persistedBefore = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, persistedBefore, "Valid cookie should be persisted");
			Assert.AreEqual("__Secure-STRP", persistedBefore[ 0 ].Name);
			Assert.AreEqual("validValue", persistedBefore[ 0 ].Value);

			// Act - Now receive an expired cookie with the same name (this is what Google does)
			string expiredCookieHeader = "__Secure-STRP=; expires=Mon, 01-Jan-1990 00:00:00 GMT; path=/; domain=.google.com; Secure";
			CookiePersistence.AddCookieFromHeader(clientName, expiredCookieHeader, baseUri);

			// Assert - the cookie should be removed
			List<PersistedCookie> persistedAfter = CookiePersistence.GetPersistedCookies(clientName);
			PersistedCookie? found = persistedAfter.FirstOrDefault(c => c.Name == "__Secure-STRP");
			Assert.IsNull(found, "Expired cookie should have removed the existing cookie");
		}

		[TestMethod]
		public void SaveCookies_FiltersExpiredCookiesFromFile()
		{
			// Arrange - create a file with both valid and expired cookies using new hierarchical format
			Dictionary<string, ClientCookieStore> testData = new Dictionary<string, ClientCookieStore>
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
																		Name = "validCookie",
																		Value = "validValue",
																		Domain = "example.com",
																		Path = "/",
																		Expires = DateTime.UtcNow.AddDays(7)
																},
																new PersistedCookie
																{
																		Name = "expiredCookie",
																		Value = "expiredValue",
																		Domain = "example.com",
																		Path = "/",
																		Expires = DateTime.UtcNow.AddDays(-7)
																}
														}
						}
					}
				}
			};
			string json = JsonSerializer.Serialize(testData, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
			File.WriteAllText(testCookieFile!, json);

			// Act
			CookiePersistence.Initialize(testCookieFile!);
			CookieContainer? container = CookiePersistence.RegisterClient("testClient", "https://example.com", enabled: true);
			CookiePersistence.SaveCookies();

			// Assert - read the file and verify only valid cookie remains
			string savedJson = File.ReadAllText(testCookieFile!);
			Dictionary<string, ClientCookieStore>? savedData = JsonSerializer.Deserialize(savedJson, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);

			Assert.IsNotNull(savedData);
			Assert.IsTrue(savedData.ContainsKey("testClient"));
			ClientCookieStore store = savedData[ "testClient" ];

			// Get all cookies from all domains
			List<PersistedCookie> allCookies = new List<PersistedCookie>();
			foreach(KeyValuePair<string, DomainCookieStore> domainKv in store.Domains)
			{
				allCookies.AddRange(domainKv.Value.Cookies);
			}

			Assert.HasCount(1, allCookies, "Only valid cookie should remain");
			Assert.AreEqual("validCookie", allCookies[ 0 ].Name);

			PersistedCookie? expiredFound = allCookies.FirstOrDefault(c => c.Name == "expiredCookie");
			Assert.IsNull(expiredFound, "Expired cookie should not be saved to file");
		}

		[TestMethod]
		public void RegisterClient_SkipsExpiredCookiesWhenLoading()
		{
			// Arrange - create a file with expired cookie using new hierarchical format
			DateTime expiredDate = DateTime.UtcNow.AddDays(-7);
			Dictionary<string, ClientCookieStore> testData = new Dictionary<string, ClientCookieStore>
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
																		Name = "expiredCookie",
																		Value = "expiredValue",
																		Domain = "example.com",
																		Path = "/",
																		Expires = expiredDate
																}
														}
						}
					}
				}
			};
			string json = JsonSerializer.Serialize(testData, HttpLibraryJsonContext.Default.DictionaryStringClientCookieStore);
			File.WriteAllText(testCookieFile!, json);

			// Act
			CookiePersistence.Initialize(testCookieFile!);
			CookieContainer? container = CookiePersistence.RegisterClient("testClient", "https://example.com", enabled: true);

			// Assert - container should not contain the expired cookie
			Assert.IsNotNull(container);
			CookieCollection cookies = container.GetCookies(new Uri("https://example.com"));
			Assert.IsEmpty(cookies, "Expired cookies should not be loaded into container");
		}

		[TestMethod]
		public void CookieName_IsCaseSensitive_RFC6265()
		{
			// RFC 6265 Section 4.1.1: Cookie names are case-sensitive
			// SessionID, sessionid, and SESSIONID should be three different cookies

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookies with same name but different casing
			CookiePersistence.AddCookieFromHeader(clientName, "SessionID=value1; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "sessionid=value2; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "SESSIONID=value3; path=/; domain=example.com", baseUri);

			// Assert - should have 3 separate cookies
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(3, persisted, "Case-sensitive names should create separate cookies per RFC 6265");

			// Verify each cookie exists with correct value
			PersistedCookie? sessionID = persisted.FirstOrDefault(c => c.Name == "SessionID");
			PersistedCookie? sessionid = persisted.FirstOrDefault(c => c.Name == "sessionid");
			PersistedCookie? SESSIONID = persisted.FirstOrDefault(c => c.Name == "SESSIONID");

			Assert.IsNotNull(sessionID, "SessionID cookie should exist");
			Assert.IsNotNull(sessionid, "sessionid cookie should exist");
			Assert.IsNotNull(SESSIONID, "SESSIONID cookie should exist");

			Assert.AreEqual("value1", sessionID.Value);
			Assert.AreEqual("value2", sessionid.Value);
			Assert.AreEqual("value3", SESSIONID.Value);
		}

		[TestMethod]
		public void CookieName_ReplacesExactCaseMatch_RFC6265()
		{
			// RFC 6265: When replacing cookies, name comparison must be case-sensitive

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookie, then replace with exact same case
			CookiePersistence.AddCookieFromHeader(clientName, "SessionID=value1; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "SessionID=value2; path=/; domain=example.com", baseUri);

			// Assert - should have only 1 cookie with updated value
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, persisted, "Same-case cookie should replace existing");
			Assert.AreEqual("SessionID", persisted[ 0 ].Name);
			Assert.AreEqual("value2", persisted[ 0 ].Value, "Cookie value should be updated");
		}

		[TestMethod]
		public void CookieName_DoesNotReplaceDifferentCaseMatch_RFC6265()
		{
			// RFC 6265: Different case names should NOT replace each other

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookie, then add different case version
			CookiePersistence.AddCookieFromHeader(clientName, "SessionID=value1; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "sessionid=value2; path=/; domain=example.com", baseUri);

			// Assert - should have 2 cookies
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(2, persisted, "Different-case cookies should coexist");

			PersistedCookie? upper = persisted.FirstOrDefault(c => c.Name == "SessionID");
			PersistedCookie? lower = persisted.FirstOrDefault(c => c.Name == "sessionid");

			Assert.IsNotNull(upper);
			Assert.IsNotNull(lower);
			Assert.AreEqual("value1", upper.Value, "Original cookie should remain unchanged");
			Assert.AreEqual("value2", lower.Value, "New cookie should be added separately");
		}

		[TestMethod]
		public void EmptyCookieValue_IsValid_RFC6265()
		{
			// RFC 6265 Section 4.1.1: Empty cookie values are valid
			// Often used with expired date to delete cookies

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookie with empty value
			string header = "cookieName=; path=/; domain=example.com";
			CookiePersistence.AddCookieFromHeader(clientName, header, baseUri);

			// Assert - should accept empty value
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, persisted, "Empty cookie value should be accepted");
			Assert.AreEqual("cookieName", persisted[ 0 ].Name);
			Assert.AreEqual("", persisted[ 0 ].Value, "Cookie value should be empty string");
		}

		[TestMethod]
		public void ExpiredCookie_WithEmptyValue_RemovesExisting_RFC6265()
		{
			// RFC 6265: Common pattern for cookie deletion is empty value + expired date

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Add a valid cookie
			CookiePersistence.AddCookieFromHeader(clientName, "sessionId=abc123; path=/; domain=example.com; expires=Thu, 01-Jan-2030 00:00:00 GMT", baseUri);
			List<PersistedCookie> before = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, before);

			// Act - Delete cookie using empty value + expired date (standard pattern)
			CookiePersistence.AddCookieFromHeader(clientName, "sessionId=; path=/; domain=example.com; expires=Mon, 01-Jan-1990 00:00:00 GMT", baseUri);

			// Assert - cookie should be removed
			List<PersistedCookie> after = CookiePersistence.GetPersistedCookies(clientName);
			PersistedCookie? found = after.FirstOrDefault(c => c.Name == "sessionId");
			Assert.IsNull(found, "Expired cookie with empty value should delete existing cookie");
		}

		[TestMethod]
		public void SecureAttribute_IsParsedAndPersisted_RFC6265()
		{
			// RFC 6265 Section 5.2.5: Secure attribute

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookie with Secure attribute
			CookiePersistence.AddCookieFromHeader(clientName, "secureSession=value123; path=/; domain=example.com; Secure", baseUri);

			// Assert
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, persisted);
			Assert.AreEqual("secureSession", persisted[ 0 ].Name);
			Assert.IsTrue(persisted[ 0 ].Secure, "Secure attribute should be true");
		}

		[TestMethod]
		public void HttpOnlyAttribute_IsParsedAndPersisted_RFC6265()
		{
			// RFC 6265 Section 5.2.6: HttpOnly attribute

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookie with HttpOnly attribute
			CookiePersistence.AddCookieFromHeader(clientName, "httpSession=value456; path=/; domain=example.com; HttpOnly", baseUri);

			// Assert
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, persisted);
			Assert.AreEqual("httpSession", persisted[ 0 ].Name);
			Assert.IsTrue(persisted[ 0 ].HttpOnly, "HttpOnly attribute should be true");
		}

		[TestMethod]
		public void SameSiteAttribute_IsParsedAndPersisted_RFC6265bis()
		{
			// RFC 6265bis: SameSite attribute (Strict, Lax, None)

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookies with different SameSite values
			CookiePersistence.AddCookieFromHeader(clientName, "strictCookie=val1; path=/; domain=example.com; SameSite=Strict", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "laxCookie=val2; path=/; domain=example.com; SameSite=Lax", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "noneCookie=val3; path=/; domain=example.com; SameSite=None; Secure", baseUri);

			// Assert
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(3, persisted);

			PersistedCookie? strict = persisted.FirstOrDefault(c => c.Name == "strictCookie");
			PersistedCookie? lax = persisted.FirstOrDefault(c => c.Name == "laxCookie");
			PersistedCookie? none = persisted.FirstOrDefault(c => c.Name == "noneCookie");

			Assert.IsNotNull(strict);
			Assert.IsNotNull(lax);
			Assert.IsNotNull(none);

			Assert.AreEqual("Strict", strict.SameSite);
			Assert.AreEqual("Lax", lax.SameSite);
			Assert.AreEqual("None", none.SameSite);
		}

		[TestMethod]
		public void SameSiteAttribute_NormalizesToTitleCase_RFC6265bis()
		{
			// SameSite values should be normalized to title case

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookie with lowercase samesite
			CookiePersistence.AddCookieFromHeader(clientName, "cookie1=val; path=/; domain=example.com; samesite=strict", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "cookie2=val; path=/; domain=example.com; SameSite=LAX", baseUri);

			// Assert
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(2, persisted);

			Assert.AreEqual("Strict", persisted[ 0 ].SameSite, "Should normalize to title case");
			Assert.AreEqual("Lax", persisted[ 1 ].SameSite, "Should normalize to title case");
		}

		[TestMethod]
		public void AllSecurityAttributes_CanBeCombined_RFC6265()
		{
			// All security attributes should work together

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookie with all security attributes
			CookiePersistence.AddCookieFromHeader(clientName, "fullCookie=value; path=/; domain=example.com; Secure; HttpOnly; SameSite=Strict", baseUri);

			// Assert
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, persisted);

			PersistedCookie cookie = persisted[ 0 ];
			Assert.AreEqual("fullCookie", cookie.Name);
			Assert.IsTrue(cookie.Secure, "Secure should be true");
			Assert.IsTrue(cookie.HttpOnly, "HttpOnly should be true");
			Assert.AreEqual("Strict", cookie.SameSite, "SameSite should be Strict");
		}

		[TestMethod]
		public void MaxAge_TakesPrecedenceOverExpires_RFC6265()
		{
			// RFC 6265 Section 5.2.2: Max-Age takes precedence over Expires

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Both attributes present, Max-Age should win
			DateTime nowBefore = DateTime.UtcNow;
			CookiePersistence.AddCookieFromHeader(clientName, "cookie=value; path=/; domain=example.com; expires=Thu, 01-Jan-2030 00:00:00 GMT; max-age=60", baseUri);
			DateTime nowAfter = DateTime.UtcNow;

			// Assert
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, persisted);

			PersistedCookie cookie = persisted[ 0 ];
			Assert.IsNotNull(cookie.Expires);

			// Expiration should be ~60 seconds from now (max-age), not 2030 (expires)
			TimeSpan diff = cookie.Expires.Value - nowBefore;
			Assert.IsTrue(diff.TotalSeconds >= 55 && diff.TotalSeconds <= 65, $"Max-Age should set expiration to ~60 seconds, got {diff.TotalSeconds}");
		}

		[TestMethod]
		public void MaxAge_Zero_CreatesExpiredCookie_RFC6265()
		{
			// RFC 6265: Max-Age=0 means delete cookie immediately

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Add a valid cookie first
			CookiePersistence.AddCookieFromHeader(clientName, "cookie=value; path=/; domain=example.com; max-age=3600", baseUri);
			List<PersistedCookie> before = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, before);

			// Act - Delete with max-age=0
			CookiePersistence.AddCookieFromHeader(clientName, "cookie=; path=/; domain=example.com; max-age=0", baseUri);

			// Assert
			List<PersistedCookie> after = CookiePersistence.GetPersistedCookies(clientName);
			PersistedCookie? found = after.FirstOrDefault(c => c.Name == "cookie");
			Assert.IsNull(found, "Max-Age=0 should delete the cookie");
		}

		[TestMethod]
		public void MaxAge_Negative_CreatesExpiredCookie_RFC6265()
		{
			// RFC 6265: Negative Max-Age means delete cookie immediately

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Add a valid cookie first
			CookiePersistence.AddCookieFromHeader(clientName, "cookie=value; path=/; domain=example.com", baseUri);
			List<PersistedCookie> before = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(1, before);

			// Act - Delete with negative max-age
			CookiePersistence.AddCookieFromHeader(clientName, "cookie=; path=/; domain=example.com; max-age=-1", baseUri);

			// Assert
			List<PersistedCookie> after = CookiePersistence.GetPersistedCookies(clientName);
			PersistedCookie? found = after.FirstOrDefault(c => c.Name == "cookie");
			Assert.IsNull(found, "Negative Max-Age should delete the cookie");
		}

		[TestMethod]
		public void InvalidCookieName_IsRejected_RFC6265()
		{
			// RFC 6265 Section 4.1.1: Cookie names must not contain separators or control chars

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Try to add cookies with invalid names
			CookiePersistence.AddCookieFromHeader(clientName, "invalid;name=value; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "invalid name=value; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "invalid,name=value; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "invalid\"name=value; path=/; domain=example.com", baseUri);

			// Assert - no cookies should be added
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.IsEmpty(persisted, "Invalid cookie names should be rejected");
		}

		[TestMethod]
		public void ValidCookieName_IsAccepted_RFC6265()
		{
			// RFC 6265: Valid cookie names (token characters)

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Add cookies with valid names
			CookiePersistence.AddCookieFromHeader(clientName, "valid-name=value1; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "valid_name=value2; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "valid.name=value3; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "VALID123=value4; path=/; domain=example.com", baseUri);

			// Assert
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.HasCount(4, persisted, "Valid cookie names should be accepted");
		}

		[TestMethod]
		public void InvalidCookieValue_WithControlChars_IsRejected_RFC6265()
		{
			// RFC 6265 Section 4.1.1: Cookie values must not contain control characters

			// Arrange
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			// Act - Try to add cookie with control character in value
			CookiePersistence.AddCookieFromHeader(clientName, "cookie=val\x01ue; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "cookie2=val\nue; path=/; domain=example.com", baseUri);

			// Assert
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);
			Assert.IsEmpty(persisted, "Cookie values with control characters should be rejected");
		}

		[TestMethod]
		public void SecurityAttributes_AreRestoredOnLoad_RFC6265()
		{
			// Security attributes should persist across save/load cycles

			// Arrange - Create and save cookies with security attributes
			CookiePersistence.Initialize(testCookieFile!);
			string clientName = "testClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(clientName, baseUri.ToString(), enabled: true);

			CookiePersistence.AddCookieFromHeader(clientName, "cookie1=val1; path=/; domain=example.com; Secure; HttpOnly; SameSite=Strict", baseUri);
			CookiePersistence.AddCookieFromHeader(clientName, "cookie2=val2; path=/; domain=example.com; Secure", baseUri);
			CookiePersistence.SaveCookies();

			// Act - Reset and reload
			CookiePersistence.Reset();
			CookiePersistence.Initialize(testCookieFile!);
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(clientName);

			// Assert
			Assert.HasCount(2, persisted);

			PersistedCookie? cookie1 = persisted.FirstOrDefault(c => c.Name == "cookie1");
			PersistedCookie? cookie2 = persisted.FirstOrDefault(c => c.Name == "cookie2");

			Assert.IsNotNull(cookie1);
			Assert.IsTrue(cookie1.Secure);
			Assert.IsTrue(cookie1.HttpOnly);
			Assert.AreEqual("Strict", cookie1.SameSite);

			Assert.IsNotNull(cookie2);
			Assert.IsTrue(cookie2.Secure);
			Assert.IsFalse(cookie2.HttpOnly);
			Assert.IsNull(cookie2.SameSite);
		}
	}
}