using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;

namespace HttpLibraryTests
{
	[TestClass]
	public class ConfigurationLoaderTests
	{
		[TestMethod]
		public void ConfigurationLoader_ClientMerge_DefaultHeadersApplied()
		{
			string defaultsJson = "{\"ConfigVersion\":1,\"DefaultRequestVersion\":\"2.0\",\"DefaultRequestHeaders\":{\"X-From-Defaults\":\"yes\",\"Accept\":\"text/html\"},\"Decompression\":\"All\"}";

			string clientsJson = "{\"ConfigVersion\":1,\"Clients\":[{\"Name\":\"testClient\",\"Uri\":\"https://example.test\",\"Timeout\":\"00:01:00\"}]}";

			HttpClientConfig defaults = ConfigurationLoader.LoadDefaultClientConfig(defaultsJson, "defaults.json");
			HttpClientConfig[] clients = ConfigurationLoader.LoadClientConfigs(clientsJson, "clients.json", defaults);

			Assert.IsNotNull(clients);
			Assert.AreEqual(1, clients.Length);
			HttpClientConfig merged = clients.First();
			Assert.IsNotNull(merged.DefaultRequestHeaders);
			Assert.IsTrue(merged.DefaultRequestHeaders.ContainsKey("X-From-Defaults"), "Merged client config should contain header from defaults");
			Assert.AreEqual("yes", merged.DefaultRequestHeaders[ "X-From-Defaults" ]);
		}
	}
}