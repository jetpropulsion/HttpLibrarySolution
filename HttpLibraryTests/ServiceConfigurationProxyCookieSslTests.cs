using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Net;
using System.Net.Http;

namespace HttpLibraryTests
{
	[TestClass]
	public class ServiceConfigurationProxyCookieSslTests
	{
		[TestMethod]
		public void CreateHandlerFromConfig_UseCookiesAndProxyAndSslValidation()
		{
			HttpClientConfig cfg = new HttpClientConfig();
			cfg.UseCookies = true;
			cfg.UseProxy = true;
			cfg.HttpProxy = "http://proxy.local:8080";
			cfg.HttpsProxy = "http://secureproxy.local:8080";
			cfg.DisableSslValidation = true;

			using System.Net.Http.SocketsHttpHandler handler = ServiceConfiguration.CreateHandlerFromConfig(cfg);

			Assert.IsTrue(handler.UseCookies, "Cookies should be enabled on handler");
			Assert.IsTrue(handler.UseProxy, "UseProxy should be set on handler");
			Assert.IsNotNull(handler);
		}
	}
}