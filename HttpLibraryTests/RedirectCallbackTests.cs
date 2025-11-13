using HttpLibrary;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class RedirectCallbackTests
	{
		[TestMethod]
		[TestCategory("Redirects")]
		public async Task RedirectCallback_IsInvoked_WhenRedirectEncountered()
		{
			string originalUrl = "http://example.com/start";
			string redirectUrl = "http://example.com/redirected";

			int callCount = 0;

			HttpMessageHandler handler = new TestRedirectHandler((request) =>
			{
				callCount++;
				if(request.RequestUri!.ToString().Equals(originalUrl, StringComparison.OrdinalIgnoreCase))
				{
					HttpResponseMessage redirect = new HttpResponseMessage(HttpStatusCode.Found);
					redirect.Headers.Location = new Uri(redirectUrl);
					return Task.FromResult(redirect);
				}

				HttpResponseMessage ok = new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("final")
				};
				return Task.FromResult(ok);
			});

			HttpClient httpClient = new HttpClient(handler, disposeHandler: true)
			{
				Timeout = TimeSpan.FromSeconds(30)
			};

			PooledHttpClientOptions opts = new PooledHttpClientOptions
			{
				Name = "test-client",
				MaxRedirections = 5
			};

			ILogger<PooledHttpClient> logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PooledHttpClient>.Instance;
			PooledHttpClient pooled = new PooledHttpClient(httpClient, Microsoft.Extensions.Options.Options.Create(opts), logger);

			HttpRedirectInfo? capturedInfo = null;
			bool callbackInvoked = false;
			pooled.RedirectCallback = (info) =>
			{
				capturedInfo = info;
				callbackInvoked = true;
				return RedirectAction.Follow;
			};

			ILogger execLogger = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger("exec");
			await HttpRequestExecutor.GetAsync(pooled, execLogger, originalUrl, timeout: TimeSpan.FromSeconds(30), redirectCount: 0, cancellationToken: CancellationToken.None).ConfigureAwait(false);

			Assert.IsTrue(callbackInvoked, "Redirect callback should have been invoked during redirect handling");
			Assert.IsNotNull(capturedInfo, "RedirectInfo should have been provided to the callback");

			Assert.AreEqual("test-client", capturedInfo!.ClientName, "RedirectInfo.ClientName should match client name");
			Assert.AreEqual(originalUrl, capturedInfo.OriginalUrl, "RedirectInfo.OriginalUrl should match original request URL");
			Assert.AreEqual(redirectUrl, capturedInfo.RedirectUrl, "RedirectInfo.RedirectUrl should match Location header");
			Assert.AreEqual(302, capturedInfo.StatusCode, "RedirectInfo.StatusCode should be302 for Found");
			Assert.AreEqual(0, capturedInfo.RedirectCount, "RedirectInfo.RedirectCount should be initial value0");
			Assert.AreEqual(HttpMethod.Get, capturedInfo.Method, "RedirectInfo.Method should be GET for original request");
			Assert.IsFalse(capturedInfo.CancellationToken.CanBeCanceled, "RedirectInfo.CancellationToken should not be cancellable in this test");

			Assert.IsTrue(callCount >= 2, "Handler should have been called for original and redirected requests");
		}
	}

	internal class TestRedirectHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

		public TestRedirectHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
		{
			_responder = responder ?? throw new ArgumentNullException(nameof(responder));
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return _responder(request);
		}
	}
}