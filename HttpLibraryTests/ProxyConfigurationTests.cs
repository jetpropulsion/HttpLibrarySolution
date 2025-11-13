using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for proxy configuration including system proxy, custom proxies, authentication, and bypass lists
	/// </summary>
	[TestClass]
	public class ProxyConfigurationTests
	{
		#region Default Configuration Tests

		[TestMethod]
		public void HttpClientConfig_UseProxy_DefaultIsFalse()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(false, config.UseProxy, "UseProxy should default to false");
		}

		[TestMethod]
		public void HttpClientConfig_UseProxy_MatchesDefaultsJson()
		{
			// This test verifies that the defaults.json value matches expected configuration
			// defaults.json specifies: "UseProxy": false

			// Arrange
			bool expectedUseProxy = false;

			// Act
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(expectedUseProxy, config.UseProxy);
		}

		[TestMethod]
		public void HttpClientConfig_HttpProxy_DefaultIsNull()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsNull(config.HttpProxy, "HttpProxy should default to null");
		}

		[TestMethod]
		public void HttpClientConfig_HttpsProxy_DefaultIsNull()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsNull(config.HttpsProxy, "HttpsProxy should default to null");
		}

		[TestMethod]
		public void HttpClientConfig_ProxyUsername_DefaultIsNull()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsNull(config.ProxyUsername);
		}

		[TestMethod]
		public void HttpClientConfig_ProxyPassword_DefaultIsNull()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsNull(config.ProxyPassword);
		}

		[TestMethod]
		public void HttpClientConfig_ProxyBypassList_DefaultIsNull()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsNull(config.ProxyBypassList);
		}

		#endregion

		#region Integration Scenario Tests

		[TestMethod]
		public void ProxyScenario_CorporateProxyWithBypass_ConfiguresCorrectly()
		{
			// Arrange - Typical corporate scenario
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "corporateClient",
				Uri = "https://external-api.example.com",
				UseProxy = true,
				HttpProxy = "http://corporate-proxy.company.local:8080",
				HttpsProxy = "http://corporate-proxy.company.local:8080",
				ProxyUsername = "DOMAIN\\username",
				ProxyPassword = "SecurePassword123!",
				ProxyBypassList = new List<string>
								{
										"localhost",
										"127.0.0.1",
										"*.company.local",
										"*.internal.company.com"
								}
			};

			// Assert
			Assert.IsTrue(config.UseProxy);
			Assert.AreEqual("http://corporate-proxy.company.local:8080", config.HttpProxy);
			Assert.AreEqual("DOMAIN\\username", config.ProxyUsername);
			Assert.AreEqual(4, config.ProxyBypassList!.Count);
		}

		[TestMethod]
		public void ProxyScenario_SystemProxyForInternalServices_ConfiguresCorrectly()
		{
			// Arrange - Internal services use system proxy
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "internalClient",
				Uri = "https://internal-api.company.local",
				UseSystemProxy = true
			};

			// Assert
			Assert.IsTrue(config.UseSystemProxy);
			Assert.IsNull(config.HttpProxy); // No custom proxy needed
		}

		[TestMethod]
		public void ProxyScenario_DirectConnectionNoProxy_ConfiguresCorrectly()
		{
			// Arrange - Direct connection (no proxy)
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "directClient",
				Uri = "https://api.example.com",
				UseProxy = false,
				UseSystemProxy = false
			};

			// Assert
			Assert.IsFalse(config.UseProxy);
			Assert.IsFalse(config.UseSystemProxy);
		}

		#endregion
	}
}