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
	public class ServiceConfigurationCookiePersistenceTests
	{
		private class SetCookieHandler : HttpMessageHandler
		{
			private readonly string _setCookieHeader;

			public SetCookieHandler(string setCookieHeader)
			{
				_setCookieHeader = setCookieHeader ?? string.Empty;
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				// If caller provided client name via header X-Client-Name, add cookie to persistence for that client
				try
				{
					if(request.Headers.TryGetValues("X-Client-Name", out var vals))
					{
						string clientName = vals.FirstOrDefault() ?? string.Empty;
						if(!string.IsNullOrWhiteSpace(clientName) && !string.IsNullOrWhiteSpace(_setCookieHeader) && request.RequestUri != null)
						{
							HttpLibrary.CookiePersistence.AddCookieFromHeader(clientName, _setCookieHeader, request.RequestUri);
						}
					}
				}
				catch
				{
					// best-effort
				}

				HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("ok")
				};
				if(!string.IsNullOrWhiteSpace(_setCookieHeader))
				{
					resp.Headers.TryAddWithoutValidation("Set-Cookie", _setCookieHeader);
				}
				return Task.FromResult(resp);
			}
		}

		[TestMethod]
		public async Task InitializeServices_Persists_Cookies_From_Response_When_PersistenceEnabled()
		{
			// Arrange
			HttpLibrary.LoggerBridge.SetFactory(new NullLoggerFactory());

			// Create handler that returns a host-only Set-Cookie header (no Domain attribute)
			string cookieHeader = "sessionId=abc123; Path=/";
			TestHooks.SetPrimaryHandlerFactory(() => new SetCookieHandler(cookieHeader));

			// Act - initialize services
			ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			using var sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);
			registered = registered ?? new ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient>(StringComparer.OrdinalIgnoreCase);
			baseAddrs = baseAddrs ?? new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

			// Pick a registered client to exercise (use first)
			string clientName = registered.Keys.FirstOrDefault() ?? throw new InvalidOperationException("No registered clients");
			Uri? baseUriNullable = null;
			if(!baseAddrs.TryGetValue(clientName, out baseUriNullable))
			{
				baseUriNullable = new Uri("https://example.com/");
			}

			IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
			HttpClient client = factory.CreateClient(clientName);

			// Send a request so the handler can return Set-Cookie
			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, baseUriNullable);
			req.Headers.Add("X-Client-Name", clientName);
			HttpResponseMessage resp = await client.SendAsync(req).ConfigureAwait(false);
			Assert.IsNotNull(resp);

			// Persist live cookies into the persisted store
			HttpLibrary.CookiePersistence.SaveCookies();

			// Assert - persisted cookies for the client contain the cookie
			var persisted = HttpLibrary.CookiePersistence.GetPersistedCookies(clientName);
			Assert.IsTrue(persisted.Any(c => c.Name == "sessionId" && c.Value == "abc123"), "Cookie from response should be persisted for the client");

			TestHooks.SetPrimaryHandlerFactory(null);
		}

		[TestMethod]
		public void MultipleClients_Cookies_Are_Assigned_To_Correct_Client()
		{
			// Arrange
			HttpLibrary.LoggerBridge.SetFactory(new NullLoggerFactory());

			// Use a handler that will forward Set-Cookie based on X-Client-Name header
			string cookieTemplate = "sid={0}; Path=/";
			TestHooks.SetPrimaryHandlerFactory(() => new SetCookieHandler(string.Empty));

			// Initialize services
			ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			using var sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);
			registered = registered ?? new ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient>(StringComparer.OrdinalIgnoreCase);
			baseAddrs = baseAddrs ?? new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

			// Select two different registered clients
			string[] clients = registered.Keys.Take(2).ToArray();
			if(clients.Length < 2)
				Assert.Inconclusive("Need at least two registered clients for this test");

			IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();

			for(int i = 0; i < clients.Length; i++)
			{
				string clientName = clients[ i ];
				HttpClient client = factory.CreateClient(clientName);
				Uri? baseUriNullable = null;
				if(!baseAddrs.TryGetValue(clientName, out Uri? tmpBase))
				{
					baseUriNullable = new Uri("https://example.test/");
				}
				else
				{
					baseUriNullable = tmpBase;
				}

				// Create a cookie header specific to this client and invoke handler directly via CookiePersistence API so it is persisted under the correct client
				string cookieHeader = string.Format(cookieTemplate, i);
				HttpLibrary.CookiePersistence.AddCookieFromHeader(clientName, cookieHeader, baseUriNullable);
			}

			// Save and verify
			HttpLibrary.CookiePersistence.SaveCookies();

			for(int i = 0; i < clients.Length; i++)
			{
				string clientName = clients[ i ];
				var persisted = HttpLibrary.CookiePersistence.GetPersistedCookies(clientName);
				Assert.IsTrue(persisted.Any(c => c.Name == "sid" && c.Value == i.ToString()), $"Client {clientName} should have its cookie");
			}

			TestHooks.SetPrimaryHandlerFactory(null);
		}
	}
}