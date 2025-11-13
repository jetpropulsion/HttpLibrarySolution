using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;

namespace HttpLibraryTests
{
	[TestClass]
	public class CookieHousekeepingTests
	{
		private string? testCookieFile;

		[TestInitialize]
		public void Initialize()
		{
			testCookieFile = Path.GetTempFileName();
			File.WriteAllText(testCookieFile, "{}");
			CookiePersistence.Reset();
			CookiePersistence.Initialize(testCookieFile);
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
		public void CleanupOrphanedCookies_RemovesOrphanedClients()
		{
			// Arrange
			string currentClient = "activeClient";
			string orphanedClient = "removedClient";

			// Add cookies for both clients
			CookiePersistence.RegisterClient(currentClient, "https://example.com", true);
			CookiePersistence.RegisterClient(orphanedClient, "https://removed.com", true);

			CookiePersistence.AddCookieFromHeader(currentClient, "activeCookie=value1; path=/; domain=example.com", new Uri("https://example.com"));
			CookiePersistence.AddCookieFromHeader(orphanedClient, "orphanedCookie=value2; path=/; domain=removed.com", new Uri("https://removed.com"));

			CookiePersistence.SaveCookies();

			// Act - only keep active client
			HashSet<string> currentClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentClient };
			int removedCount = CookiePersistence.CleanupOrphanedCookies(currentClients);

			// Assert
			Assert.AreEqual(1, removedCount, "Should remove one orphaned client");

			List<PersistedCookie> activeCookies = CookiePersistence.GetPersistedCookies(currentClient);
			Assert.AreEqual(1, activeCookies.Count, "Active client cookies should remain");

			List<PersistedCookie> orphanedCookies = CookiePersistence.GetPersistedCookies(orphanedClient);
			Assert.AreEqual(0, orphanedCookies.Count, "Orphaned client cookies should be removed");
		}

		[TestMethod]
		public void CleanupOrphanedCookies_NoOrphans_ReturnsZero()
		{
			// Arrange
			string client1 = "client1";
			string client2 = "client2";

			CookiePersistence.RegisterClient(client1, "https://example1.com", true);
			CookiePersistence.RegisterClient(client2, "https://example2.com", true);

			HashSet<string> currentClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { client1, client2 };

			// Act
			int removedCount = CookiePersistence.CleanupOrphanedCookies(currentClients);

			// Assert
			Assert.AreEqual(0, removedCount, "Should not remove any clients");
		}

		[TestMethod]
		public void CleanupOrphanedCookies_NullClientNames_ReturnsZero()
		{
			// Act
			int removedCount = 0;
			bool exceptionThrown = false;
			try
			{
				removedCount = CookiePersistence.CleanupOrphanedCookies(null!);
			}
			catch(ArgumentNullException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "Should throw ArgumentNullException for null client names");
		}

		[TestMethod]
		public void MigrateCookies_SuccessfulMigration()
		{
			// Arrange
			string oldName = "oldClient";
			string newName = "newClient";
			Uri baseUri = new Uri("https://example.com");

			CookiePersistence.RegisterClient(oldName, baseUri.ToString(), true);
			CookiePersistence.AddCookieFromHeader(oldName, "session=abc123; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(oldName, "user=john; path=/api; domain=example.com", baseUri);
			CookiePersistence.SaveCookies();

			// Act
			int migratedCount = CookiePersistence.MigrateCookies(oldName, newName);

			// Assert
			Assert.AreEqual(2, migratedCount, "Should migrate 2 cookies");

			List<PersistedCookie> oldCookies = CookiePersistence.GetPersistedCookies(oldName);
			Assert.AreEqual(0, oldCookies.Count, "Old client should have no cookies");

			List<PersistedCookie> newCookies = CookiePersistence.GetPersistedCookies(newName);
			Assert.AreEqual(2, newCookies.Count, "New client should have migrated cookies");

			PersistedCookie? sessionCookie = newCookies.Find(c => c.Name == "session");
			Assert.IsNotNull(sessionCookie, "Session cookie should be migrated");
			Assert.AreEqual("abc123", sessionCookie.Value);

			PersistedCookie? userCookie = newCookies.Find(c => c.Name == "user");
			Assert.IsNotNull(userCookie, "User cookie should be migrated");
			Assert.AreEqual("john", userCookie.Value);
		}

		[TestMethod]
		public void MigrateCookies_NoOldCookies_ReturnsZero()
		{
			// Arrange
			string oldName = "nonExistentClient";
			string newName = "newClient";

			// Act
			int migratedCount = CookiePersistence.MigrateCookies(oldName, newName);

			// Assert
			Assert.AreEqual(0, migratedCount, "Should return 0 when no cookies to migrate");
		}

		[TestMethod]
		public void MigrateCookies_SameClientName_ReturnsZero()
		{
			// Arrange
			string clientName = "sameClient";

			// Act
			int migratedCount = CookiePersistence.MigrateCookies(clientName, clientName);

			// Assert
			Assert.AreEqual(0, migratedCount, "Should return 0 for same client name");
		}

		[TestMethod]
		public void MigrateCookies_NullOldName_ThrowsException()
		{
			// Act
			bool exceptionThrown = false;
			try
			{
				CookiePersistence.MigrateCookies(null!, "newClient");
			}
			catch(ArgumentException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "Should throw ArgumentException for null old name");
		}

		[TestMethod]
		public void MigrateCookies_NullNewName_ThrowsException()
		{
			// Act
			bool exceptionThrown = false;
			try
			{
				CookiePersistence.MigrateCookies("oldClient", null!);
			}
			catch(ArgumentException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "Should throw ArgumentException for null new name");
		}

		[TestMethod]
		public void MigrateCookies_MergesWithExistingCookies()
		{
			// Arrange
			string oldName = "oldClient";
			string newName = "newClient";
			Uri baseUri = new Uri("https://example.com");

			// Setup old client with cookies
			CookiePersistence.RegisterClient(oldName, baseUri.ToString(), true);
			CookiePersistence.AddCookieFromHeader(oldName, "oldCookie=oldValue; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(oldName, "sharedCookie=fromOld; path=/; domain=example.com", baseUri);

			// Setup new client with existing cookies
			CookiePersistence.RegisterClient(newName, baseUri.ToString(), true);
			CookiePersistence.AddCookieFromHeader(newName, "newCookie=newValue; path=/; domain=example.com", baseUri);
			CookiePersistence.AddCookieFromHeader(newName, "sharedCookie=fromNew; path=/; domain=example.com", baseUri);

			CookiePersistence.SaveCookies();

			// Act
			int migratedCount = CookiePersistence.MigrateCookies(oldName, newName);

			// Assert
			Assert.AreEqual(2, migratedCount, "Should migrate 2 cookies from old client");

			List<PersistedCookie> newCookies = CookiePersistence.GetPersistedCookies(newName);
			Assert.AreEqual(3, newCookies.Count, "New client should have 3 total cookies (1 original + 2 migrated, with shared cookie replaced)");

			PersistedCookie? oldCookie = newCookies.Find(c => c.Name == "oldCookie");
			Assert.IsNotNull(oldCookie, "Old cookie should be migrated");

			PersistedCookie? newCookie = newCookies.Find(c => c.Name == "newCookie");
			Assert.IsNotNull(newCookie, "New cookie should still exist");

			PersistedCookie? sharedCookie = newCookies.Find(c => c.Name == "sharedCookie");
			Assert.IsNotNull(sharedCookie, "Shared cookie should exist");
			Assert.AreEqual("fromOld", sharedCookie.Value, "Shared cookie should be replaced with value from old client");
		}

		[TestMethod]
		public void GenerateReport_ReturnsCorrectStatistics()
		{
			// Arrange
			CookiePersistence.RegisterClient("client1", "https://example1.com", true);
			CookiePersistence.RegisterClient("client2", "https://example2.com", true);
			CookiePersistence.RegisterClient("client3", "https://example3.com", true);

			CookiePersistence.AddCookieFromHeader("client1", "cookie1=value1; domain=example1.com", new Uri("https://example1.com"));
			CookiePersistence.AddCookieFromHeader("client1", "cookie2=value2; domain=example1.com", new Uri("https://example1.com"));
			CookiePersistence.AddCookieFromHeader("client2", "cookie3=value3; domain=example2.com", new Uri("https://example2.com"));

			CookiePersistence.SaveCookies();

			// Act
			Dictionary<string, int> report = CookiePersistence.GenerateReport();

			// Assert
			// Only clients with actual persisted cookies will appear in the report
			// client3 was registered but has no cookies added, so it won't be persisted
			Assert.AreEqual(2, report.Count, "Report should include 2 clients (those with cookies)");
			Assert.AreEqual(2, report[ "client1" ], "client1 should have 2 cookies");
			Assert.AreEqual(1, report[ "client2" ], "client2 should have 1 cookie");
		}

		[TestMethod]
		public void RemoveAllCookiesForClient_RemovesCookies()
		{
			// Arrange
			string clientName = "testClient";
			CookiePersistence.RegisterClient(clientName, "https://example.com", true);
			CookiePersistence.AddCookieFromHeader(clientName, "cookie1=value1; domain=example.com", new Uri("https://example.com"));
			CookiePersistence.AddCookieFromHeader(clientName, "cookie2=value2; domain=example.com", new Uri("https://example.com"));
			CookiePersistence.SaveCookies();

			// Act
			int removedCount = CookiePersistence.RemoveAllCookiesForClient(clientName);

			// Assert
			Assert.AreEqual(2, removedCount, "Should remove 2 cookies");

			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies(clientName);
			Assert.AreEqual(0, cookies.Count, "Client should have no cookies after removal");
		}

		[TestMethod]
		public void RemoveAllCookiesForClient_NoCookies_ReturnsZero()
		{
			// Arrange
			string clientName = "emptyClient";

			// Act
			int removedCount = CookiePersistence.RemoveAllCookiesForClient(clientName);

			// Assert
			Assert.AreEqual(0, removedCount, "Should return 0 when no cookies exist");
		}

		[TestMethod]
		public void RemoveAllCookiesForClient_NullClientName_ThrowsException()
		{
			// Act
			bool exceptionThrown = false;
			try
			{
				CookiePersistence.RemoveAllCookiesForClient(null!);
			}
			catch(ArgumentException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "Should throw ArgumentException for null client name");
		}

		[TestMethod]
		public void MigrateCookies_PreservesAllCookieAttributes()
		{
			// Arrange
			string oldName = "oldClient";
			string newName = "newClient";
			Uri baseUri = new Uri("https://example.com");

			CookiePersistence.RegisterClient(oldName, baseUri.ToString(), true);
			CookiePersistence.AddCookieFromHeader(oldName,
					"secure=value; path=/api; domain=example.com; Secure; HttpOnly; SameSite=Strict; Max-Age=3600",
					baseUri);
			CookiePersistence.SaveCookies();

			// Act
			int migratedCount = CookiePersistence.MigrateCookies(oldName, newName);

			// Assert
			Assert.AreEqual(1, migratedCount, "Should migrate 1 cookie");

			List<PersistedCookie> newCookies = CookiePersistence.GetPersistedCookies(newName);
			Assert.AreEqual(1, newCookies.Count, "New client should have 1 cookie");

			PersistedCookie migratedCookie = newCookies[ 0 ];
			Assert.AreEqual("secure", migratedCookie.Name);
			Assert.AreEqual("value", migratedCookie.Value);
			Assert.AreEqual("/api", migratedCookie.Path);
			Assert.AreEqual("example.com", migratedCookie.Domain);
			Assert.IsTrue(migratedCookie.Secure, "Secure flag should be preserved");
			Assert.IsTrue(migratedCookie.HttpOnly, "HttpOnly flag should be preserved");
			Assert.AreEqual("Strict", migratedCookie.SameSite, "SameSite attribute should be preserved");
			Assert.IsNotNull(migratedCookie.Expires, "Expiration should be preserved");
		}

		[TestMethod]
		public void CleanupOrphanedCookies_CaseInsensitiveComparison()
		{
			// Arrange
			string client = "TestClient";
			CookiePersistence.RegisterClient(client, "https://example.com", true);
			CookiePersistence.AddCookieFromHeader(client, "cookie=value; domain=example.com", new Uri("https://example.com"));
			CookiePersistence.SaveCookies();

			// Act - use different casing
			HashSet<string> currentClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "testclient" };
			int removedCount = CookiePersistence.CleanupOrphanedCookies(currentClients);

			// Assert
			Assert.AreEqual(0, removedCount, "Should not remove client due to case-insensitive matching");

			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies(client);
			Assert.AreEqual(1, cookies.Count, "Cookies should still exist");
		}

		[TestMethod]
		public void CleanupOrphanedCookies_MultipleDomains()
		{
			// Arrange
			string orphanedClient = "oldClient";
			CookiePersistence.RegisterClient(orphanedClient, "https://example.com", true);
			CookiePersistence.AddCookieFromHeader(orphanedClient, "cookie1=value1; domain=example.com", new Uri("https://example.com"));
			CookiePersistence.AddCookieFromHeader(orphanedClient, "cookie2=value2; domain=.example.com", new Uri("https://example.com"));
			CookiePersistence.AddCookieFromHeader(orphanedClient, "cookie3=value3; domain=subdomain.example.com", new Uri("https://subdomain.example.com"));
			CookiePersistence.SaveCookies();

			// Act
			HashSet<string> currentClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			int removedCount = CookiePersistence.CleanupOrphanedCookies(currentClients);

			// Assert
			Assert.AreEqual(1, removedCount, "Should remove 1 orphaned client");

			List<PersistedCookie> cookies = CookiePersistence.GetPersistedCookies(orphanedClient);
			Assert.AreEqual(0, cookies.Count, "All cookies across all domains should be removed");
		}
	}
}