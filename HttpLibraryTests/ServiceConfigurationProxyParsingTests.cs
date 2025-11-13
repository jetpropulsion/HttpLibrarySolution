using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

namespace HttpLibraryTests
{
	[TestClass]
	public class ServiceConfigurationProxyParsingTests
	{
		[TestMethod]
		public void CreateHandlerFromConfig_ParsesProxyUriAndSetsUseProxy()
		{
			HttpClientConfig cfg = new HttpClientConfig();
			cfg.UseProxy = true;
			cfg.HttpProxy = "http://proxy.example.com:3128";

			using System.Net.Http.SocketsHttpHandler handler = ServiceConfiguration.CreateHandlerFromConfig(cfg);

			Assert.IsTrue(handler.UseProxy, "Handler should have UseProxy true when a proxy is configured");
			Assert.IsNotNull(handler.Proxy, "Handler.Proxy should be set when a valid proxy URI is provided");
		}

		[TestMethod]
		public void CreateHandlerFromConfig_InvalidProxyUri_DoesNotThrowAndUseProxyTrue()
		{
			HttpClientConfig cfg = new HttpClientConfig();
			cfg.UseProxy = true;
			cfg.HttpProxy = ":not-a-uri";

			using System.Net.Http.SocketsHttpHandler handler = ServiceConfiguration.CreateHandlerFromConfig(cfg);

			Assert.IsTrue(handler.UseProxy, "Handler should have UseProxy true even if proxy URI parsing fails (best-effort)");
		}
	}
}