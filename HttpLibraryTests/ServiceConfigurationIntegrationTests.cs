using HttpLibrary.Testing;

using HttpLibraryTests.TestUtilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class ServiceConfigurationIntegrationTests
	{
		[TestMethod]
		public async Task InitializeServices_CreatesMultipleClients_FromConfig()
		{
			HttpLibrary.LoggerBridge.SetFactory(new NullLoggerFactory());

			TestHooks.SetPrimaryHandlerFactory(() => new InMemoryResponseHandler());

			ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			using ServiceProvider sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);
			registered = registered ?? new ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient>(StringComparer.OrdinalIgnoreCase);
			baseAddrs = baseAddrs ?? new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

			Assert.IsTrue(registered.Count > 0, "No clients were registered");

			IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
			foreach(var kvp in registered)
			{
				HttpClient client = factory.CreateClient(kvp.Key);
				Uri? baseUri = null;
				baseAddrs.TryGetValue(kvp.Key, out baseUri);
				if(baseUri == null)
				{
					baseUri = new Uri("https://example.com/");
				}
				HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, baseUri);
				HttpResponseMessage resp = await client.SendAsync(req);
				Assert.IsNotNull(resp);
			}

			TestHooks.SetPrimaryHandlerFactory(null);
			HttpLibrary.LoggerBridge.SetFactory(new NullLoggerFactory());
		}
	}
}