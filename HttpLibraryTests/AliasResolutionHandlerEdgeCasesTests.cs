using HttpLibrary.Handlers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class AliasResolutionHandlerEdgeCasesTests
	{
		[TestInitialize]
		public void Init()
		{
			HttpLibrary.ServiceConfiguration.AppConfig.Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		[TestMethod]
		public async Task SendAsync_TrailingAndLeadingSlashes_CombineCorrectly()
		{
			var aliases = HttpLibrary.ServiceConfiguration.AppConfig.Aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			aliases[ "svc1" ] = "https://api.example.com/"; // base has trailing slash
			aliases[ "svc2" ] = "https://api2.example.com"; // base without trailing slash

			Uri? captured1 = null;
			Uri? captured2 = null;

			HttpMessageHandler inner1 = new CaptureRequestUriHandler(uri => captured1 = uri);
			HttpMessageHandler inner2 = new CaptureRequestUriHandler(uri => captured2 = uri);

			AliasResolutionHandler h1 = new AliasResolutionHandler(new NullLogger<AliasResolutionHandler>()) { InnerHandler = inner1 };
			AliasResolutionHandler h2 = new AliasResolutionHandler(new NullLogger<AliasResolutionHandler>()) { InnerHandler = inner2 };

			using HttpMessageInvoker inv1 = new HttpMessageInvoker(h1);
			using HttpMessageInvoker inv2 = new HttpMessageInvoker(h2);

			HttpResponseMessage r1 = await inv1.SendAsync(new HttpRequestMessage(HttpMethod.Get, "alias://svc1/api/resource"), CancellationToken.None).ConfigureAwait(false);
			HttpResponseMessage r2 = await inv2.SendAsync(new HttpRequestMessage(HttpMethod.Get, "alias://svc2/api/resource"), CancellationToken.None).ConfigureAwait(false);

			Assert.AreEqual(HttpStatusCode.OK, r1.StatusCode);
			Assert.AreEqual(HttpStatusCode.OK, r2.StatusCode);
			Assert.IsNotNull(captured1);
			Assert.IsNotNull(captured2);
			Assert.IsTrue(captured1!.ToString().StartsWith("https://api.example.com/api/resource", StringComparison.OrdinalIgnoreCase));
			Assert.IsTrue(captured2!.ToString().StartsWith("https://api2.example.com/api/resource", StringComparison.OrdinalIgnoreCase));
		}

		[TestMethod]
		public async Task SendAsync_PortPreservedInResolvedUri()
		{
			var aliases = HttpLibrary.ServiceConfiguration.AppConfig.Aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			aliases[ "svcport" ] = "https://example.com:8443/base";

			Uri? captured = null;
			HttpMessageHandler inner = new CaptureRequestUriHandler(uri => captured = uri);

			AliasResolutionHandler handler = new AliasResolutionHandler(new NullLogger<AliasResolutionHandler>()) { InnerHandler = inner };
			using HttpMessageInvoker inv = new HttpMessageInvoker(handler);

			HttpResponseMessage resp = await inv.SendAsync(new HttpRequestMessage(HttpMethod.Get, "alias://svcport/api"), CancellationToken.None).ConfigureAwait(false);

			Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
			Assert.IsNotNull(captured);
			Assert.IsTrue(captured!.ToString().StartsWith("https://example.com:8443/base/api", StringComparison.OrdinalIgnoreCase));
		}

		[TestMethod]
		public async Task SendAsync_QueryAndFragment_ArePreserved()
		{
			var aliases = HttpLibrary.ServiceConfiguration.AppConfig.Aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			aliases[ "svcq" ] = "https://example.com";

			Uri? captured = null;
			HttpMessageHandler inner = new CaptureRequestUriHandler(uri => captured = uri);

			AliasResolutionHandler handler = new AliasResolutionHandler(new NullLogger<AliasResolutionHandler>()) { InnerHandler = inner };
			using HttpMessageInvoker inv = new HttpMessageInvoker(handler);

			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "alias://svcq/path/subpath?x=1&y=two#section");
			HttpResponseMessage resp = await inv.SendAsync(req, CancellationToken.None).ConfigureAwait(false);

			Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
			Assert.IsNotNull(captured);
			string s = captured!.ToString();
			Assert.IsTrue(s.Contains("?x=1&y=two", StringComparison.OrdinalIgnoreCase));
			Assert.IsTrue(s.Contains("#section", StringComparison.OrdinalIgnoreCase));
		}

		// Helper that captures URI and returns OK
		private sealed class CaptureRequestUriHandler : HttpMessageHandler
		{
			private readonly Action<Uri?> _onRequest;

			public CaptureRequestUriHandler(Action<Uri?> onRequest)
			{
				_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				try
				{ _onRequest(request?.RequestUri); }
				catch { }
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			}
		}
	}
}