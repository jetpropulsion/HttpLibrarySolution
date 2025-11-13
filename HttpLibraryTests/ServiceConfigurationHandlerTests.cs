using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

namespace HttpLibraryTests
{
	[TestClass]
	public class ServiceConfigurationHandlerTests
	{
		[TestMethod]
		public void CreateHandlerFromConfig_AppliesDecompressionAndTimeouts()
		{
			HttpClientConfig cfg = new HttpClientConfig();
			cfg.Decompression = "GZip, Deflate";
			cfg.ConnectTimeout = TimeSpan.FromSeconds(5);
			cfg.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(3);
			cfg.Expect100ContinueTimeout = TimeSpan.FromSeconds(1);
			cfg.MaxResponseHeadersLength = 128;

			using System.Net.Http.SocketsHttpHandler handler = ServiceConfiguration.CreateHandlerFromConfig(cfg);

			Assert.AreEqual(System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate, handler.AutomaticDecompression);
			Assert.AreEqual(TimeSpan.FromSeconds(5), handler.ConnectTimeout);
			Assert.AreEqual(TimeSpan.FromMinutes(3), handler.PooledConnectionIdleTimeout);
			Assert.AreEqual(TimeSpan.FromSeconds(1), handler.Expect100ContinueTimeout);
			Assert.AreEqual(128, handler.MaxResponseHeadersLength);
		}
	}
}