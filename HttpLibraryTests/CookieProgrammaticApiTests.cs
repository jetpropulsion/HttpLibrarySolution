using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace HttpLibraryTests
{
	[TestClass]
	public class CookieProgrammaticApiTests
	{
		private string? _testCookieFile;

		[TestInitialize]
		public void Initialize()
		{
			_testCookieFile = Path.GetTempFileName();
			File.WriteAllText(_testCookieFile, "{}");
			CookiePersistence.Reset();
		}

		[TestCleanup]
		public void Cleanup()
		{
			CookiePersistence.Reset();
			if(_testCookieFile != null && File.Exists(_testCookieFile))
			{
				File.Delete(_testCookieFile);
			}
		}

		[TestMethod]
		public void AddCookie_Programmatic_AddsCookieAndContainer()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "progClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(client, baseUri.ToString(), enabled: true);

			CookiePersistence.AddCookie(client, "example.com", "/", "prog", "value1", null, secure: false, httpOnly: false, sameSite: null);

			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsTrue(persisted.Any(c => c.Name == "prog" && c.Value == "value1"));

			Assert.IsNotNull(container);
			CookieCollection cookies = container.GetCookies(baseUri);
			Cookie? found = cookies.Cast<Cookie>().FirstOrDefault(c => c.Name == "prog");
			Assert.IsNotNull(found);
			Assert.AreEqual("value1", found.Value);
		}

		[TestMethod]
		public void AddCookie_Rejects__Secure_without_SecureFlag()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "progClient";
			CookiePersistence.RegisterClient(client, "https://example.com", enabled: true);

			CookiePersistence.AddCookie(client, "example.com", "/", "__Secure-test", "v", null, secure: false, httpOnly: false, sameSite: null);

			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsFalse(persisted.Any(c => c.Name == "__Secure-test"));
		}

		[TestMethod]
		public void AddCookie__Host_Enforcement()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "progClient";
			CookiePersistence.RegisterClient(client, "https://example.com", enabled: true);

			// __Host- with domain provided should be rejected
			CookiePersistence.AddCookie(client, "example.com", "/", "__Host-test", "v", null, secure: true, httpOnly: false, sameSite: null);
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsFalse(persisted.Any(c => c.Name == "__Host-test"), "__Host- cookie with Domain should be rejected");

			// __Host- with empty domain and path=/ and secure should be accepted
			CookiePersistence.AddCookie(client, "", "/", "__Host-accept", "v2", null, secure: true, httpOnly: false, sameSite: null);
			persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsTrue(persisted.Any(c => c.Name == "__Host-accept"), "Valid __Host- cookie should be accepted");
		}

		[TestMethod]
		public void AddCookie_SameSiteNone_RequiresSecure()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "progClient";
			CookiePersistence.RegisterClient(client, "https://example.com", enabled: true);

			// SameSite=None without secure -> reject
			CookiePersistence.AddCookie(client, "example.com", "/", "snone", "v", null, secure: false, httpOnly: false, sameSite: "None");
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsFalse(persisted.Any(c => c.Name == "snone"));

			// SameSite=None with secure -> accept and normalize
			CookiePersistence.AddCookie(client, "example.com", "/", "snone2", "v2", null, secure: true, httpOnly: false, sameSite: "none");
			persisted = CookiePersistence.GetPersistedCookies(client);
			PersistedCookie? pc = persisted.FirstOrDefault(c => c.Name == "snone2");
			Assert.IsNotNull(pc);
			Assert.AreEqual("None", pc.SameSite);
		}

		[TestMethod]
		public void AddCookie_ExpiryInPast_DeletesCookie()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "progClient";
			CookiePersistence.RegisterClient(client, "https://example.com", enabled: true);

			DateTime future = DateTime.UtcNow.AddMinutes(10);
			CookiePersistence.AddCookie(client, "example.com", "/", "todel", "val", future, secure: false, httpOnly: false, sameSite: null);
			List<PersistedCookie> before = CookiePersistence.GetPersistedCookies(client);
			Assert.IsTrue(before.Any(c => c.Name == "todel"));

			// now add with past expiry -> should remove
			DateTime past = DateTime.UtcNow.AddSeconds(-10);
			CookiePersistence.AddCookie(client, "example.com", "/", "todel", "", past, secure: false, httpOnly: false, sameSite: null);
			List<PersistedCookie> after = CookiePersistence.GetPersistedCookies(client);
			Assert.IsFalse(after.Any(c => c.Name == "todel"));
		}

		[TestMethod]
		public void ModifyCookieValue_UpdatesPersistedAndContainer()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "progClient";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(client, baseUri.ToString(), enabled: true);

			CookiePersistence.AddCookie(client, "example.com", "/", "cm", "v1", null, secure: false, httpOnly: false, sameSite: null);
			bool modified = CookiePersistence.ModifyCookieValue(client, "example.com", "/", "cm", "v2");
			Assert.IsTrue(modified, "ModifyCookieValue should return true when modified");

			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			PersistedCookie? pc = persisted.FirstOrDefault(c => c.Name == "cm");
			Assert.IsNotNull(pc);
			Assert.AreEqual("v2", pc.Value);

			Assert.IsNotNull(container);
			CookieCollection cookies = container.GetCookies(baseUri);
			Cookie? runtime = cookies.Cast<Cookie>().FirstOrDefault(c => c.Name == "cm");
			Assert.IsNotNull(runtime);
			Assert.AreEqual("v2", runtime.Value);
		}
	}
}