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
	public class CookieHeaderSessionTests
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
		public void AddCookieFromHeader_SessionCookie_IsSessionTrue()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "hdr1";
			Uri baseUri = new Uri("https://example.com");
			CookiePersistence.RegisterClient(client, baseUri.ToString(), enabled: true);

			string header = "sessionId=abc123; Path=/; Domain=example.com"; // no Expires or Max-Age => session
			CookiePersistence.AddCookieFromHeader(client, header, baseUri);

			List<PersistedCookie> persisted = CookiePersistence.GetPersistedCookies(client);
			PersistedCookie? pc = persisted.FirstOrDefault(c => c.Name == "sessionId");
			Assert.IsNotNull(pc, "Session cookie from header should be persisted as session entry");
			Assert.IsNull(pc!.Expires, "Session cookie should have null Expires");
			Assert.IsTrue(pc.IsSession, "Session cookie should have IsSession = true");
		}

		[TestMethod]
		public void RemoveSessionCookies_Removes_HeaderParsedSessionCookie()
		{
			CookiePersistence.Initialize(_testCookieFile!);
			string client = "hdr2";
			Uri baseUri = new Uri("https://example.com");
			CookiePersistence.RegisterClient(client, baseUri.ToString(), enabled: true);

			string header = "sessionId=abc123; Path=/; Domain=example.com"; // session cookie
			CookiePersistence.AddCookieFromHeader(client, header, baseUri);

			List<PersistedCookie> before = CookiePersistence.GetPersistedCookies(client);
			Assert.IsTrue(before.Any(c => c.Name == "sessionId"));

			int removed = CookiePersistence.RemoveSessionCookies(client);
			Assert.IsTrue(removed >= 1, "RemoveSessionCookies should remove at least one session cookie added from header");

			List<PersistedCookie> after = CookiePersistence.GetPersistedCookies(client);
			Assert.IsFalse(after.Any(c => c.Name == "sessionId"));
		}
	}
}