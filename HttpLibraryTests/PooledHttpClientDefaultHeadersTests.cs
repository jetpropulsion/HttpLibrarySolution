using HttpLibrary;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
	public class PooledHttpClientDefaultHeadersTests
	{
		private class CaptureHandler : HttpMessageHandler
		{
			public HttpRequestMessage? LastRequest { get; private set; }
			public HttpResponseMessage Response { get; set; } = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				LastRequest = request;
				return Task.FromResult(Response);
			}
		}

		[TestMethod]
		public async Task SendRawAsync_AppliesDefaultHeaders_WhenAbsentOnRequest()
		{
			// Arrange
			CaptureHandler handler = new CaptureHandler();
			HttpClient httpClient = new HttpClient(handler);

			PooledHttpClientOptions opts = new PooledHttpClientOptions { Name = "test" };
			opts.DefaultRequestHeaders[ "Accept" ] = "text/html";
			opts.DefaultRequestHeaders[ "User-Agent" ] = "MyTestAgent/1.0";

			PooledHttpClient pooled = new PooledHttpClient(httpClient, Options.Create(opts), new NullLogger<PooledHttpClient>());

			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");

			// Act
			HttpResponseMessage resp = await pooled.SendRawAsync(req);

			// Assert
			Assert.IsNotNull(handler.LastRequest, "Handler did not receive a request");

			IEnumerable<string>? acceptValues;
			bool hasAccept = handler.LastRequest.Headers.TryGetValues("Accept", out acceptValues);
			Assert.IsTrue(hasAccept, "Accept header not present on sent request");
			Assert.IsNotNull(acceptValues);
			Assert.AreEqual("text/html", string.Join(", ", acceptValues));

			IEnumerable<string>? uaValues;
			bool hasUa = handler.LastRequest.Headers.TryGetValues("User-Agent", out uaValues);
			Assert.IsTrue(hasUa, "User-Agent header not present on sent request");
			Assert.IsNotNull(uaValues);
			Assert.AreEqual("MyTestAgent/1.0", string.Join(", ", uaValues));
		}
	}
}