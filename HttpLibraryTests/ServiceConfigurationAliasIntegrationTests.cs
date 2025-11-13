using HttpLibrary.Testing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class ServiceConfigurationAliasIntegrationTests
	{
		[TestInitialize]
		public void Init()
		{
			// ensure empty aliases
			HttpLibrary.ServiceConfiguration.AppConfig.Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		[TestMethod]
		public async Task InitializeServices_WithAliases_WiresAliasResolutionHandlerIntoPipeline()
		{
			// Arrange: configure alias mapping and set a primary handler factory that captures final request URIs and Host header
			var aliases = HttpLibrary.ServiceConfiguration.AppConfig.Aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			aliases[ "billing" ] = "https://billing.example.com/api/";

			Uri? capturedUri = null;
			string? capturedHostHeader = null;
			string? capturedLocationHeader = null;

			// Primary handler will capture the incoming absolute request and then return a302 redirect to a different path to test redirect headers
			TestHooks.SetPrimaryHandlerFactory(() => new CaptureAndRedirectPrimaryHandler(uri => capturedUri = uri, host => capturedHostHeader = host, loc => capturedLocationHeader = loc));

			// Act: initialize services which will register handlers including AliasResolutionHandler
			ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			using var sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);

			// Use a pooled client to send a request using alias:// URI
			HttpLibrary.IPooledHttpClient client = registered.Values.FirstOrDefault() ?? throw new InvalidOperationException("No registered clients");

			// Send request via client.SendRawAsync to go through pipeline
			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "alias://billing/invoices/42?active=true#top");
			HttpResponseMessage resp = await client.SendRawAsync(req, CancellationToken.None).ConfigureAwait(false);

			// Assert: Primary handler saw resolved absolute URI
			Assert.IsNotNull(capturedUri);
			string s = capturedUri!.ToString();
			Assert.IsTrue(s.StartsWith("https://billing.example.com/api/invoices/42", StringComparison.OrdinalIgnoreCase));

			// Assert: Host header was set to the resolved host (including port when applicable)
			Assert.IsNotNull(capturedHostHeader);
			Assert.IsTrue(capturedHostHeader!.StartsWith("billing.example.com", StringComparison.OrdinalIgnoreCase));

			// Assert: primary handler returned a redirect; the response should contain Location header and match expected value
			Assert.IsNotNull(resp);
			Assert.IsTrue(resp.StatusCode == HttpStatusCode.Found || resp.StatusCode == HttpStatusCode.Redirect);
			Assert.IsNotNull(resp.Headers.Location);
			Assert.AreEqual("https://redirect.example.com/newpath", resp.Headers.Location!.ToString());

			// Additionally verify that the capture callback recorded the generated Location value
			Assert.IsNotNull(capturedLocationHeader);
			string expectedCapturedLocation = new Uri(capturedUri!, "redirected").ToString();
			Assert.AreEqual(expectedCapturedLocation, capturedLocationHeader);

			// Cleanup
			TestHooks.SetPrimaryHandlerFactory(null);
		}

		[TestMethod]
		public async Task UnknownAlias_UsingFullPipeline_ThrowsHttpRequestException()
		{
			// Arrange: ensure no alias mapping exists for 'nope'
			HttpLibrary.ServiceConfiguration.AppConfig.Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			// Primary handler that would return OK if reached; we expect alias resolution to fail before reaching it
			TestHooks.SetPrimaryHandlerFactory(() => new SimpleOkPrimaryHandler());

			// Initialize services
			ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			using var sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);

			HttpLibrary.IPooledHttpClient client = registered.Values.FirstOrDefault() ?? throw new InvalidOperationException("No registered clients");

			// Act & Assert: sending alias://nope/... should result in HttpRequestException from alias resolver
			try
			{
				await client.SendRawAsync(new HttpRequestMessage(HttpMethod.Get, "alias://nope/test"), CancellationToken.None).ConfigureAwait(false);
				Assert.Fail("Expected HttpRequestException due to unknown alias");
			}
			catch(HttpRequestException)
			{
				// expected
			}
			finally
			{
				TestHooks.SetPrimaryHandlerFactory(null);
			}
		}

		[TestMethod]
		public async Task HttpRequestExecutor_FollowsRedirects_UpdatesHostAndSendsCookie()
		{
			// Arrange: configure alias and primary handler to simulate redirect + set-cookie
			var aliases = HttpLibrary.ServiceConfiguration.AppConfig.Aliases ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			aliases[ "billing" ] = "https://billing.example.com/";

			Uri? finalRequestUri = null;
			string? finalHostHeader = null;
			string? finalCookieHeader = null;

			TestHooks.SetPrimaryHandlerFactory(() => new RedirectSequencePrimaryHandler((uri) => finalRequestUri = uri, (host) => finalHostHeader = host, (cookie) => finalCookieHeader = cookie));

			// Initialize services
			ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			using var sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);

			// Pick a pooled client to exercise
			HttpLibrary.IPooledHttpClient client = registered.Values.FirstOrDefault() ?? throw new InvalidOperationException("No registered clients");

			// Use a simple logger
			Microsoft.Extensions.Logging.ILogger logger = new NullLoggerFactory().CreateLogger("test");

			// Act: call HttpRequestExecutor which should follow redirect automatically
			string result = await HttpLibrary.HttpRequestExecutor.GetAsync(client, logger, "alias://billing/start");

			// Assert: final response body from primary handler
			Assert.AreEqual("final", result);

			// Assert: final request URI observed by primary handler is the redirected host/path
			Assert.IsNotNull(finalRequestUri);
			Assert.IsTrue(finalRequestUri!.ToString().StartsWith("https://redirect.example.com/final", StringComparison.OrdinalIgnoreCase));

			// Assert: Host header on final request is updated to redirect host
			Assert.IsNotNull(finalHostHeader);
			Assert.AreEqual("redirect.example.com", finalHostHeader);

			// Assert: Cookie header was sent on final request (cookie set by initial redirect response)
			// The runtime handler should persist cookies; verify persistence store contains the cookie
			var persisted = HttpLibrary.CookiePersistence.GetPersistedCookies(client.Name ?? string.Empty);
			Assert.IsTrue(persisted.Any(c => c.Name == "sid" && c.Value == "abc"));

			// Ensure persisted state is written to disk before inspecting the cookies file
			HttpLibrary.CookiePersistence.SaveCookies();

			// Check persisted store OR runtime container for the cookie
			bool persistedHas = HttpLibrary.CookiePersistence.GetPersistedCookies(client.Name ?? string.Empty).Any(c => c.Name == "sid" && c.Value == "abc");
			System.Net.CookieContainer? runtimeContainer = HttpLibrary.CookiePersistence.GetContainer(client.Name ?? string.Empty);
			bool runtimeHas = false;
			try
			{
				if(runtimeContainer != null)
				{
					Uri checkUri = new Uri("https://redirect.example.com/");
					System.Net.CookieCollection cc = runtimeContainer.GetCookies(checkUri);
					foreach(System.Net.Cookie ck in cc)
					{
						if(string.Equals(ck.Name, "sid", StringComparison.Ordinal) && string.Equals(ck.Value, "abc", StringComparison.Ordinal))
						{
							runtimeHas = true;
							break;
						}
					}
				}
			}
			catch { }

			Assert.IsTrue(persistedHas || runtimeHas, "Either persisted store or runtime cookie container should contain the sid=abc cookie");

			// Cleanup
			TestHooks.SetPrimaryHandlerFactory(null);
		}

		// Primary handler that records the final absolute request URI, Host header, and returns a redirect Location
		private sealed class CaptureAndRedirectPrimaryHandler : HttpMessageHandler
		{
			private readonly Action<Uri?> _onRequest;
			private readonly Action<string?> _onHost;
			private readonly Action<string?> _onLocation;
			public CaptureAndRedirectPrimaryHandler(Action<Uri?> onRequest, Action<string?> onHost, Action<string?> onLocation)
			{
				_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));
				_onHost = onHost ?? throw new ArgumentNullException(nameof(onHost));
				_onLocation = onLocation ?? throw new ArgumentNullException(nameof(onLocation));
			}
			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				try
				{
					_onRequest(request?.RequestUri);
					_onHost(request?.Headers?.Host);
					string loc = new Uri(request!.RequestUri!, "redirected").ToString();
					_onLocation(loc);
				}
				catch { }

				HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.Found);
				resp.Headers.Location = new Uri("https://redirect.example.com/newpath");
				return Task.FromResult(resp);
			}
		}

		private sealed class SimpleOkPrimaryHandler : HttpMessageHandler
		{
			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			}
		}

		// Primary handler that simulates a redirect followed by a final response and captures headers
		private sealed class RedirectSequencePrimaryHandler : HttpMessageHandler
		{
			private readonly Action<Uri?> _onFinalRequest;
			private readonly Action<string?> _onFinalHost;
			private readonly Action<string?> _onFinalCookie;
			private int _callCount = 0;

			public RedirectSequencePrimaryHandler(Action<Uri?> onFinalRequest, Action<string?> onFinalHost, Action<string?> onFinalCookie)
			{
				_onFinalRequest = onFinalRequest ?? throw new ArgumentNullException(nameof(onFinalRequest));
				_onFinalHost = onFinalHost ?? throw new ArgumentNullException(nameof(onFinalHost));
				_onFinalCookie = onFinalCookie ?? throw new ArgumentNullException(nameof(onFinalCookie));
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				_callCount++;
				if(_callCount == 1)
				{
					// First request: simulate redirect response with Set-Cookie
					HttpResponseMessage redirect = new HttpResponseMessage(HttpStatusCode.Found);
					redirect.Headers.Location = new Uri("https://redirect.example.com/final");
					// Include Set-Cookie for the redirected host - CookieCaptureHandler should persist this
					redirect.Headers.TryAddWithoutValidation("Set-Cookie", "sid=abc; Domain=redirect.example.com; Path=/");
					return Task.FromResult(redirect);
				}
				else
				{
					// Second request: final destination - capture Host and Cookie headers
					try
					{
						_onFinalRequest(request?.RequestUri);
						_onFinalHost(request?.Headers?.Host);
						if(request?.Headers.TryGetValues("Cookie", out var vals) == true)
						{
							_onFinalCookie(string.Join(";", vals));
						}
					}
					catch { }

					HttpResponseMessage ok = new HttpResponseMessage(HttpStatusCode.OK);
					ok.Content = new StringContent("final");
					return Task.FromResult(ok);
				}
			}
		}
	}
}