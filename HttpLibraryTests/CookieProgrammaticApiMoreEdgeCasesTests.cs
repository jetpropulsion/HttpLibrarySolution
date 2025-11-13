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
	public class CookieProgrammaticApiMoreEdgeCasesTests
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
				File.Delete(_testCookieFile);
		}

		[TestMethod]
		public void InvalidSameSite_String_IsNormalizedToNull()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "edge2";
			CookiePersistence.RegisterClient(client, "https://example.com", enabled: true);

			CookiePersistence.AddCookie(client, "example.com", "/", "ssInvalid", "v", null, secure: true, httpOnly: false, sameSite: "Nope");

			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			PersistedCookie? pc = persisted.FirstOrDefault(c => c.Name == "ssInvalid");
			Assert.IsNotNull(pc);
			Assert.IsNull(pc!.SameSite, "Invalid SameSite tokens should be normalized to null");
		}

		[TestMethod]
		public void CookieName_CaseSensitivity_DoesNotReplaceDifferentCase()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "edge3";
			Uri baseUri = new Uri("https://example.com");
			CookieContainer? container = CookiePersistence.RegisterClient(client, baseUri.ToString(), enabled: true);

			CookiePersistence.AddCookie(client, "example.com", "/", "MyCookie", "v1", null, secure: false, httpOnly: false, sameSite: null);
			CookiePersistence.AddCookie(client, "example.com", "/", "mycookie", "v2", null, secure: false, httpOnly: false, sameSite: null);

			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsTrue(persisted.Any(c => c.Name == "MyCookie"));
			Assert.IsTrue(persisted.Any(c => c.Name == "mycookie"));

			Assert.IsNotNull(container);
			CookieCollection cc = container.GetCookies(baseUri);
			// Runtime CookieContainer may treat names case-insensitively; assert at least one cookie exists case-insensitively
			Assert.IsTrue(cc.Cast<Cookie>().Any(c => string.Equals(c.Name, "MyCookie", StringComparison.OrdinalIgnoreCase)), "At least one cookie should be present in runtime container (case-insensitive)");
		}

		[TestMethod]
		public void DomainMatching_Permutations()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "edge4";
			string baseAddr = "https://sub.example.com/path/";
			CookiePersistence.RegisterClient(client, baseAddr, enabled: true);

			// domain that matches base (example.com) should be accepted
			CookiePersistence.AddCookie(client, "example.com", "/", "d_match", "v", null, secure: false, httpOnly: false, sameSite: null);
			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsTrue(persisted.Any(c => c.Name == "d_match"), "Domain matching parent domain should be accepted");

			// domain that does NOT match base should be rejected
			CookiePersistence.AddCookie(client, "other.com", "/", "d_reject", "v", null, secure: false, httpOnly: false, sameSite: null);
			persisted = CookiePersistence.GetPersistedCookies(client);
			Assert.IsFalse(persisted.Any(c => c.Name == "d_reject"), "Domain not matching base should be rejected when base address is set");

			// when base address is NOT set, a programmatic domain attribute is allowed
			string client2 = "edge4b";
			CookiePersistence.RegisterClient(client2, null, enabled: true);
			CookiePersistence.AddCookie(client2, "other.com", "/", "d_allowed", "v", null, secure: false, httpOnly: false, sameSite: null);
			List<PersistedCookie> persisted2 = CookiePersistence.GetPersistedCookies(client2);
			Assert.IsTrue(persisted2.Any(c => c.Name == "d_allowed"), "Without base address, domain attribute should be accepted programmatically");

			// leading dot and case-insensitivity should normalize to lowercase without dot
			string client3 = "edge4c";
			CookiePersistence.RegisterClient(client3, null, enabled: true);
			CookiePersistence.AddCookie(client3, ".EXAMPLE.COM", "/", "d_norm", "v", null, secure: false, httpOnly: false, sameSite: null);
			List<PersistedCookie> persisted3 = CookiePersistence.GetPersistedCookies(client3);
			PersistedCookie? norm = persisted3.FirstOrDefault(c => c.Name == "d_norm");
			Assert.IsNotNull(norm);
			Assert.AreEqual("example.com", norm!.Domain, "Domain should be normalized to lowercase without leading dot");
		}
	}
}