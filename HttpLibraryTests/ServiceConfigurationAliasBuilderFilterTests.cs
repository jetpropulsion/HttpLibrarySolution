using HttpLibrary.Handlers;
using HttpLibrary.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class ServiceConfigurationAliasBuilderFilterTests
	{
		[TestInitialize]
		public void Init()
		{
			// Ensure clean alias mapping
			HttpLibrary.ServiceConfiguration.AppConfig.Aliases = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		[TestMethod]
		public async Task AliasHandler_IsOuterMost_InDIConstructedPipeline()
		{
			// Arrange: configure alias
			var aliases = HttpLibrary.ServiceConfiguration.AppConfig.Aliases ??= new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			aliases[ "google" ] = "https://google.com/";

			// Use a primary handler that captures the final request URI and host
			Uri? capturedUri = null;
			string? capturedHost = null;
			TestHooks.SetPrimaryHandlerFactory(() => new CaptureRequestUriPrimaryHandler(uri => capturedUri = uri, host => capturedHost = host));

			// Act: initialize services and create client via factory
			ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			using var sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);

			HttpLibrary.IPooledHttpClient client = registered.Values.FirstOrDefault() ?? throw new InvalidOperationException("No registered clients");

			// Send a request using alias://; the alias handler should rewrite it to https:// before any logging handler tries to process it
			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "alias://google/search?q=test");
			HttpResponseMessage resp = await client.SendRawAsync(req, CancellationToken.None);

			// Assert: primary handler should have seen an absolute https:// URI
			Assert.IsNotNull(capturedUri);
			Assert.IsTrue(capturedUri!.ToString().StartsWith("https://google.com/search", StringComparison.OrdinalIgnoreCase));

			// Cleanup
			TestHooks.SetPrimaryHandlerFactory(null);
		}

		// Primary handler used in tests
		private sealed class CaptureRequestUriPrimaryHandler : HttpMessageHandler
		{
			private readonly Action<Uri?> _onRequest;
			private readonly Action<string?> _onHost;
			public CaptureRequestUriPrimaryHandler(Action<Uri?> onRequest, Action<string?> onHost)
			{
				_onRequest = onRequest;
				_onHost = onHost;
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				try
				{
					_onRequest(request?.RequestUri);
					_onHost(request?.Headers?.Host);
				}
				catch { }

				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			}
		}
	}
}