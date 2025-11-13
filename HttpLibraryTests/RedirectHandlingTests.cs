using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for HTTP redirect handling according to RFC 7231
	/// </summary>
	[TestClass]
	public class RedirectHandlingTests
	{
		[TestMethod]
		public void RedirectAction_Follow_HasCorrectValue()
		{
			// Arrange & Act
			RedirectAction action = RedirectAction.Follow;

			// Assert
			Assert.AreEqual(0, (int)action, "RedirectAction.Follow should be 0");
		}

		[TestMethod]
		public void RedirectAction_Stop_HasCorrectValue()
		{
			// Arrange & Act
			RedirectAction action = RedirectAction.Stop;

			// Assert
			Assert.AreEqual(1, (int)action, "RedirectAction.Stop should be 1");
		}

		[TestMethod]
		public void RedirectAction_Cancel_HasCorrectValue()
		{
			// Arrange & Act
			RedirectAction action = RedirectAction.Cancel;

			// Assert
			Assert.AreEqual(3, (int)action, "RedirectAction.Cancel should be 3");
		}

		[TestMethod]
		public void RedirectAction_FollowWithGet_HasCorrectValue()
		{
			// Arrange & Act
			RedirectAction action = RedirectAction.FollowWithGet;

			// Assert
			Assert.AreEqual(2, (int)action, "RedirectAction.FollowWithGet should be 2");
		}

		[TestMethod]
		public void HttpRedirectInfo_Constructor_SetsPropertiesCorrectly()
		{
			// Arrange
			string clientName = "testClient";
			string originalUrl = "https://example.com/original";
			string redirectUrl = "https://example.com/redirect";
			int statusCode = 302;
			int redirectCount = 1;
			HttpMethod method = HttpMethod.Get;
			CancellationToken token = CancellationToken.None;

			// Act
			HttpRedirectInfo info = new HttpRedirectInfo(
					clientName,
					originalUrl,
					redirectUrl,
					statusCode,
					redirectCount,
					method,
					token);

			// Assert
			Assert.AreEqual(clientName, info.ClientName);
			Assert.AreEqual(originalUrl, info.OriginalUrl);
			Assert.AreEqual(redirectUrl, info.RedirectUrl);
			Assert.AreEqual(statusCode, info.StatusCode);
			Assert.AreEqual(redirectCount, info.RedirectCount);
			Assert.AreEqual(method, info.Method);
			Assert.AreEqual(token, info.CancellationToken);
		}

		[TestMethod]
		public void HttpRedirectInfo_StatusCode301_IsPermanentRedirect()
		{
			// Arrange
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					301,
					0,
					HttpMethod.Get,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(301, info.StatusCode, "301 is Moved Permanently");
		}

		[TestMethod]
		public void HttpRedirectInfo_StatusCode302_IsFound()
		{
			// Arrange
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					302,
					0,
					HttpMethod.Get,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(302, info.StatusCode, "302 is Found");
		}

		[TestMethod]
		public void HttpRedirectInfo_StatusCode303_IsSeeOther()
		{
			// Arrange
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					303,
					0,
					HttpMethod.Post,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(303, info.StatusCode, "303 is See Other");
		}

		[TestMethod]
		public void HttpRedirectInfo_StatusCode307_IsTemporaryRedirect()
		{
			// Arrange
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					307,
					0,
					HttpMethod.Post,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(307, info.StatusCode, "307 is Temporary Redirect");
		}

		[TestMethod]
		public void HttpRedirectInfo_StatusCode308_IsPermanentRedirect()
		{
			// Arrange
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					308,
					0,
					HttpMethod.Post,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(308, info.StatusCode, "308 is Permanent Redirect");
		}

		[TestMethod]
		public void HttpRedirectInfo_WithPostMethod_PreservesMethod()
		{
			// Arrange & Act
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/api/data",
					"https://example.com/api/v2/data",
					307,
					0,
					HttpMethod.Post,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(HttpMethod.Post, info.Method, "POST method should be preserved");
		}

		[TestMethod]
		public void HttpRedirectInfo_WithPutMethod_PreservesMethod()
		{
			// Arrange & Act
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/api/resource",
					"https://example.com/api/v2/resource",
					308,
					0,
					HttpMethod.Put,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(HttpMethod.Put, info.Method, "PUT method should be preserved");
		}

		[TestMethod]
		public void HttpRedirectInfo_WithDeleteMethod_PreservesMethod()
		{
			// Arrange & Act
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/api/resource/123",
					"https://example.com/api/v2/resource/123",
					307,
					0,
					HttpMethod.Delete,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(HttpMethod.Delete, info.Method, "DELETE method should be preserved");
		}

		[TestMethod]
		public void HttpRedirectInfo_RedirectCount_TracksChainDepth()
		{
			// Arrange & Act - Simulating third redirect in chain
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/step2",
					"https://example.com/step3",
					302,
					2,
					HttpMethod.Get,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(2, info.RedirectCount, "Should track position in redirect chain");
		}

		[TestMethod]
		public void HttpRedirectInfo_DifferentDomain_PreservesUrls()
		{
			// Arrange
			string originalUrl = "https://old-domain.com/page";
			string redirectUrl = "https://new-domain.com/page";

			// Act
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					originalUrl,
					redirectUrl,
					301,
					0,
					HttpMethod.Get,
					CancellationToken.None);

			// Assert
			Assert.AreEqual(originalUrl, info.OriginalUrl);
			Assert.AreEqual(redirectUrl, info.RedirectUrl);
		}

		[TestMethod]
		public void HttpRedirectInfo_WithCancellationToken_StoresToken()
		{
			// Arrange
			CancellationTokenSource cts = new CancellationTokenSource();
			CancellationToken token = cts.Token;

			// Act
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					302,
					0,
					HttpMethod.Get,
					token);

			// Assert
			Assert.AreEqual(token, info.CancellationToken);

			// Cleanup
			cts.Dispose();
		}

		[TestMethod]
		public void HttpRedirectInfo_WithCancelledToken_TokenIsCancelled()
		{
			// Arrange
			CancellationTokenSource cts = new CancellationTokenSource();
			cts.Cancel();
			CancellationToken token = cts.Token;

			// Act
			HttpRedirectInfo info = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					302,
					0,
					HttpMethod.Get,
					token);

			// Assert
			Assert.IsTrue(info.CancellationToken.IsCancellationRequested);

			// Cleanup
			cts.Dispose();
		}

		[TestMethod]
		public void HttpProgressInfo_Constructor_SetsPropertiesCorrectly()
		{
			// Arrange
			string clientName = "testClient";
			string url = "https://example.com/large-file.zip";

			// Act
			HttpProgressInfo progress = new HttpProgressInfo(clientName, url);

			// Assert
			Assert.AreEqual(clientName, progress.ClientName);
			Assert.AreEqual(url, progress.Url);
		}

		[TestMethod]
		public void MaxRedirections_DefaultValue_Is10()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(10, config.MaxRedirections, "Default max redirections should be 10");
		}

		[TestMethod]
		public void MaxRedirections_ConfigValue_MatchesDefaultsJson()
		{
			// This test verifies that the defaults.json value matches expected configuration
			// defaults.json specifies: "MaxRedirections": 10

			// Arrange
			int expectedMaxRedirections = 10;

			// Act
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(expectedMaxRedirections, config.MaxRedirections);
		}

		[TestMethod]
		public void AllowAutoRedirect_DefaultValue_IsFalse()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(false, config.AllowAutoRedirect, "AllowAutoRedirect should be disabled by default for manual control");
		}

		[TestMethod]
		public void AllowAutoRedirect_ConfigValue_MatchesDefaultsJson()
		{
			// This test verifies that the defaults.json value matches expected configuration
			// defaults.json specifies: "AllowAutoRedirect": false

			// Arrange
			bool expectedAllowAutoRedirect = false;

			// Act
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(expectedAllowAutoRedirect, config.AllowAutoRedirect);
		}

		[TestMethod]
		public void HttpClientConfig_MaxRedirections_DefaultIsZero()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(10, config.MaxRedirections, "Unified config has default value of 10");
		}

		[TestMethod]
		public void HttpClientConfig_AllowAutoRedirect_DefaultIsFalse()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.IsFalse(config.AllowAutoRedirect, "AllowAutoRedirect should default to false");
		}

		[TestMethod]
		public void HttpClientConfig_MaxRedirections_ValidatesRange()
		{
			// Arrange - MaxRedirections has max of 50 per option definitions
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "test",
				Uri = "https://example.com",
				MaxRedirections = 51
			};

			// Act
			bool result = config.Validate();

			// Assert
			Assert.IsFalse(result, "MaxRedirections above 50 should fail validation");
		}

		[TestMethod]
		public void HttpClientConfig_MaxRedirections_AcceptsValidRange()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig
			{
				Name = "test",
				Uri = "https://example.com",
				MaxRedirections = 25
			};

			// Act
			bool result = config.Validate();

			// Assert
			Assert.IsTrue(result, "MaxRedirections within range should pass validation");
		}

		[TestMethod]
		public void RedirectCallback_CanReturnFollow()
		{
			// Arrange
			Func<HttpRedirectInfo, RedirectAction> callback = (info) => RedirectAction.Follow;
			HttpRedirectInfo redirectInfo = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					302,
					0,
					HttpMethod.Get,
					CancellationToken.None);

			// Act
			RedirectAction result = callback(redirectInfo);

			// Assert
			Assert.AreEqual(RedirectAction.Follow, result);
		}

		[TestMethod]
		public void RedirectCallback_CanReturnStop()
		{
			// Arrange
			Func<HttpRedirectInfo, RedirectAction> callback = (info) => RedirectAction.Stop;
			HttpRedirectInfo redirectInfo = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					302,
					0,
					HttpMethod.Get,
					CancellationToken.None);

			// Act
			RedirectAction result = callback(redirectInfo);

			// Assert
			Assert.AreEqual(RedirectAction.Stop, result);
		}

		[TestMethod]
		public void RedirectCallback_CanReturnCancel()
		{
			// Arrange
			Func<HttpRedirectInfo, RedirectAction> callback = (info) => RedirectAction.Cancel;
			HttpRedirectInfo redirectInfo = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					302,
					0,
					HttpMethod.Get,
					CancellationToken.None);

			// Act
			RedirectAction result = callback(redirectInfo);

			// Assert
			Assert.AreEqual(RedirectAction.Cancel, result);
		}

		[TestMethod]
		public void RedirectCallback_CanReturnFollowWithGet()
		{
			// Arrange
			Func<HttpRedirectInfo, RedirectAction> callback = (info) => RedirectAction.FollowWithGet;
			HttpRedirectInfo redirectInfo = new HttpRedirectInfo(
					"test",
					"https://example.com/old",
					"https://example.com/new",
					302,
					0,
					HttpMethod.Post,
					CancellationToken.None);

			// Act
			RedirectAction result = callback(redirectInfo);

			// Assert
			Assert.AreEqual(RedirectAction.FollowWithGet, result);
		}

		[TestMethod]
		public void RedirectCallback_ConditionalLogic_WorksCorrectly()
		{
			// Arrange - Callback that stops after 3 redirects
			Func<HttpRedirectInfo, RedirectAction> callback = (info) =>
			{
				if(info.RedirectCount >= 3)
				{
					return RedirectAction.Stop;
				}
				return RedirectAction.Follow;
			};

			// Act & Assert - First redirect
			HttpRedirectInfo info1 = new HttpRedirectInfo("test", "url1", "url2", 302, 0, HttpMethod.Get, CancellationToken.None);
			Assert.AreEqual(RedirectAction.Follow, callback(info1));

			// Second redirect
			HttpRedirectInfo info2 = new HttpRedirectInfo("test", "url2", "url3", 302, 1, HttpMethod.Get, CancellationToken.None);
			Assert.AreEqual(RedirectAction.Follow, callback(info2));

			// Third redirect
			HttpRedirectInfo info3 = new HttpRedirectInfo("test", "url3", "url4", 302, 2, HttpMethod.Get, CancellationToken.None);
			Assert.AreEqual(RedirectAction.Follow, callback(info3));

			// Fourth redirect - should stop
			HttpRedirectInfo info4 = new HttpRedirectInfo("test", "url4", "url5", 302, 3, HttpMethod.Get, CancellationToken.None);
			Assert.AreEqual(RedirectAction.Stop, callback(info4));
		}

		[TestMethod]
		public void RedirectCallback_DomainCheck_WorksCorrectly()
		{
			// Arrange - Callback that stops on cross-domain redirects
			Func<HttpRedirectInfo, RedirectAction> callback = (info) =>
			{
				Uri originalUri = new Uri(info.OriginalUrl);
				Uri redirectUri = new Uri(info.RedirectUrl);

				if(originalUri.Host != redirectUri.Host)
				{
					return RedirectAction.Stop;
				}
				return RedirectAction.Follow;
			};

			// Act & Assert - Same domain
			HttpRedirectInfo sameDomain = new HttpRedirectInfo(
					"test",
					"https://example.com/page1",
					"https://example.com/page2",
					302,
					0,
					HttpMethod.Get,
					CancellationToken.None);
			Assert.AreEqual(RedirectAction.Follow, callback(sameDomain));

			// Different domain
			HttpRedirectInfo differentDomain = new HttpRedirectInfo(
					"test",
					"https://example.com/page",
					"https://other-domain.com/page",
					302,
					0,
					HttpMethod.Get,
					CancellationToken.None);
			Assert.AreEqual(RedirectAction.Stop, callback(differentDomain));
		}

		[TestMethod]
		public void RedirectCallback_StatusCodeCheck_WorksCorrectly()
		{
			// Arrange - Callback that changes POST to GET on 303
			Func<HttpRedirectInfo, RedirectAction> callback = (info) =>
			{
				if(info.StatusCode == 303)
				{
					return RedirectAction.FollowWithGet;
				}
				return RedirectAction.Follow;
			};

			// Act & Assert - 302 status
			HttpRedirectInfo status302 = new HttpRedirectInfo("test", "url1", "url2", 302, 0, HttpMethod.Post, CancellationToken.None);
			Assert.AreEqual(RedirectAction.Follow, callback(status302));

			// 303 status
			HttpRedirectInfo status303 = new HttpRedirectInfo("test", "url1", "url2", 303, 0, HttpMethod.Post, CancellationToken.None);
			Assert.AreEqual(RedirectAction.FollowWithGet, callback(status303));
		}

		[TestMethod]
		public void HttpLibraryConfig_DefaultMaxRedirections_Is10()
		{
			// Assert
			Assert.AreEqual(10, Constants.DefaultMaxRedirections);
		}

		[TestMethod]
		public void PooledHttpClientOptions_MaxRedirections_DefaultMatchesConfig()
		{
			// Arrange
			PooledHttpClientOptions options = new PooledHttpClientOptions();

			// Assert
			Assert.AreEqual(Constants.DefaultMaxRedirections, options.MaxRedirections);
		}

		[TestMethod]
		public void PooledHttpClientOptions_MaxRedirections_CanBeSet()
		{
			// Arrange
			PooledHttpClientOptions options = new PooledHttpClientOptions
			{
				MaxRedirections = 5
			};

			// Assert
			Assert.AreEqual(5, options.MaxRedirections);
		}
	}
}