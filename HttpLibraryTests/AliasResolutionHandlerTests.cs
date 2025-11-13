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
	public class AliasResolutionHandlerTests
	{
		[TestInitialize]
		public void Init()
		{
			// Ensure Aliases dictionary is initialized fresh for each test
			HttpLibrary.ServiceConfiguration.AppConfig.Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		[TestMethod]
		public async Task SendAsync_ResolvesAliasToConfiguredBaseUrl()
		{
			// Arrange
			var aliases = HttpLibrary.ServiceConfiguration.AppConfig.Aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			aliases[ "google" ] = "https://google.com";

			Uri? capturedRequestUri = null;

			// Inner handler captures the request URI and returns a simple OK response
			HttpMessageHandler inner = new CaptureRequestUriHandler(uri => capturedRequestUri = uri);

			AliasResolutionHandler aliasHandler = new AliasResolutionHandler(new NullLogger<AliasResolutionHandler>());
			aliasHandler.InnerHandler = inner;

			using HttpMessageInvoker invoker = new HttpMessageInvoker(aliasHandler);

			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "alias://google/search?q=test");

			// Act
			HttpResponseMessage resp = await invoker.SendAsync(req, CancellationToken.None).ConfigureAwait(false);

			// Assert
			Assert.IsNotNull(resp);
			Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
			Assert.IsNotNull(capturedRequestUri);
			Assert.IsTrue(capturedRequestUri!.ToString().StartsWith("https://google.com/search", StringComparison.OrdinalIgnoreCase));
		}

		[TestMethod]
		public async Task SendAsync_ThrowsOnUnknownAlias()
		{
			// Arrange - ensure Aliases map is empty
			HttpLibrary.ServiceConfiguration.AppConfig.Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			AliasResolutionHandler aliasHandler = new AliasResolutionHandler(new NullLogger<AliasResolutionHandler>());
			aliasHandler.InnerHandler = new SimpleOkHandler();
			using HttpMessageInvoker invoker = new HttpMessageInvoker(aliasHandler);

			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "alias://unknown/path");

			// Act & Assert - MSTest may not expose ThrowsExceptionAsync; use try/catch
			try
			{
				HttpResponseMessage r = await invoker.SendAsync(req, CancellationToken.None).ConfigureAwait(false);
				// If we reach here, the handler did not throw as expected
				Assert.Fail("Expected HttpRequestException for unknown alias");
			}
			catch(HttpRequestException)
			{
				// expected
			}
		}

		[TestMethod]
		public async Task SendAsync_DoesNotChangeHttpUri()
		{
			// Arrange
			Uri? capturedRequestUri = null;
			HttpMessageHandler inner = new CaptureRequestUriHandler(uri => capturedRequestUri = uri);

			AliasResolutionHandler aliasHandler = new AliasResolutionHandler(new NullLogger<AliasResolutionHandler>());
			aliasHandler.InnerHandler = inner;

			using HttpMessageInvoker invoker = new HttpMessageInvoker(aliasHandler);

			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/test");

			// Act
			HttpResponseMessage resp = await invoker.SendAsync(req, CancellationToken.None).ConfigureAwait(false);

			// Assert
			Assert.IsNotNull(resp);
			Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
			Assert.IsNotNull(capturedRequestUri);
			Assert.AreEqual("https://example.com/api/test", capturedRequestUri!.ToString());
		}

		// Helper handler that returns OK and captures the incoming request URI
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
				{
					_onRequest(request?.RequestUri);
				}
				catch { }

				HttpResponseMessage r = new HttpResponseMessage(HttpStatusCode.OK);
				return Task.FromResult(r);
			}
		}

		// Simple inner handler that returns OK
		private sealed class SimpleOkHandler : HttpMessageHandler
		{
			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			}
		}
	}
}