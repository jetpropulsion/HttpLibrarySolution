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
	public class CookieProgrammaticApiEdgeCaseTests
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
		public void ProgrammaticAdd_RejectsInvalidDomainAttribute()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "edgeClient";
			CookiePersistence.RegisterClient(client, "https://example.com", enabled: true);

			// Attempt to add cookie with domain that does not match request host
			CookiePersistence.AddCookie(client, "evil.com", "/", "bad", "v", null, secure: false, httpOnly: false, sameSite: null);
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsFalse(persisted.Any(c => c.Name == "bad"));
		}

		[TestMethod]
		public void SameSite_Casing_Permutations_AcceptedAndNormalized()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "edgeClient";
			CookiePersistence.RegisterClient(client, "https://example.com", enabled: true);

			CookiePersistence.AddCookie(client, "example.com", "/", "a1", "v1", null, secure: true, httpOnly: false, sameSite: "Strict");
			CookiePersistence.AddCookie(client, "example.com", "/", "a2", "v2", null, secure: true, httpOnly: false, sameSite: "strict");
			CookiePersistence.AddCookie(client, "example.com", "/", "a3", "v3", null, secure: true, httpOnly: false, sameSite: "StRiCt");

			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsTrue(persisted.Where(c => c.Name.StartsWith("a")).All(c => c.SameSite == "Strict"));
		}

		[TestMethod]
		public void Interaction_WithBaseAddress_UsesBaseUriForRuntimeContainer()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "edgeClient";
			string baseAddr = "https://sub.example.com/path/";
			CookieContainer? container = CookiePersistence.RegisterClient(client, baseAddr, enabled: true);

			CookiePersistence.AddCookie(client, "sub.example.com", "/", "b1", "v1", null, secure: false, httpOnly: false, sameSite: null);

			Assert.IsNotNull(container);
			CookieCollection cc = container.GetCookies(new Uri(baseAddr));
			Cookie? found = cc.Cast<Cookie>().FirstOrDefault(c => c.Name == "b1");
			Assert.IsNotNull(found, "Cookie added programmatically should be present in runtime container when base address is set");
		}

		[TestMethod]
		public void RemoveSessionCookies_RemovesOnlySessionCookies()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "edgeClient";
			CookiePersistence.RegisterClient(client, "https://example.com", enabled: true);

			// Add session cookie (Expires = null)
			CookiePersistence.AddCookie(client, "example.com", "/", "s1", "v", null, secure: false, httpOnly: false, sameSite: null);
			// Add persistent cookie
			CookiePersistence.AddCookie(client, "example.com", "/", "p1", "v", DateTime.UtcNow.AddDays(1), secure: false, httpOnly: false, sameSite: null);

			List<PersistedCookie> before = CookiePersistence.GetPersistedCookies(client);
			Assert.IsTrue(before.Any(c => c.Name == "s1"));
			Assert.IsTrue(before.Any(c => c.Name == "p1"));

			int removed = CookiePersistence.RemoveSessionCookies(client);
			Assert.AreEqual(1, removed);

			List<PersistedCookie> after = CookiePersistence.GetPersistedCookies(client);
			Assert.IsFalse(after.Any(c => c.Name == "s1"));
			Assert.IsTrue(after.Any(c => c.Name == "p1"));
		}
	}
}