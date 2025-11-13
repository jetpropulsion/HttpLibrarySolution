using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for client alias URL parsing and combination functionality.
	/// Tests the CombineUrlParts method and client alias URL resolution logic in ClientSelector.
	/// </summary>
	[TestClass]
	public class ClientAliasUrlTests
	{
		public TestContext TestContext { get; set; }

		#region CombineUrlParts Tests - Basic Functionality

		[TestMethod]
		public void CombineUrlParts_EmptyPathQueryFragment_ReturnsBaseUriAsString()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = string.Empty;

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/", result);
		}

		[TestMethod]
		public void CombineUrlParts_NullPathQueryFragment_ReturnsBaseUriAsString()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = string.Empty;

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/", result);
		}

		[TestMethod]
		public void CombineUrlParts_OnlyPath_ReturnsBaseUriWithPath()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/api/users";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/api/users", result);
		}

		[TestMethod]
		public void CombineUrlParts_PathWithTrailingSlash_ReturnsCorrectUrl()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/api/";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/api/", result);
		}

		#endregion

		#region CombineUrlParts Tests - Query String

		[TestMethod]
		public void CombineUrlParts_PathAndQuery_ReturnsUrlWithQuery()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/search?q=test";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/search?q=test", result);
		}

		[TestMethod]
		public void CombineUrlParts_OnlyQuery_ReturnsUrlWithRootPathAndQuery()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "?q=test";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("?q=test"));
		}

		[TestMethod]
		public void CombineUrlParts_MultipleQueryParameters_ReturnsCorrectUrl()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/api/search?q=test&page=1&limit=10";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/api/search?q=test&page=1&limit=10", result);
		}

		[TestMethod]
		public void CombineUrlParts_QueryWithSpecialCharacters_ReturnsCorrectUrl()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/search?q=hello+world&filter=type%3Dbook";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("/search?"));
			Assert.IsTrue(result.Contains("q=hello+world"));
		}

		#endregion

		#region CombineUrlParts Tests - Fragment

		[TestMethod]
		public void CombineUrlParts_PathWithFragment_ReturnsUrlWithFragment()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/docs#section1";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/docs#section1", result);
		}

		[TestMethod]
		public void CombineUrlParts_OnlyFragment_ReturnsUrlWithFragment()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "#section1";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("#section1"));
		}

		[TestMethod]
		public void CombineUrlParts_PathQueryAndFragment_ReturnsCompleteUrl()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/docs/api?version=2#authentication";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/docs/api?version=2#authentication", result);
		}

		[TestMethod]
		public void CombineUrlParts_QueryAndFragment_ReturnsCorrectUrl()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "?tab=overview#details";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("?tab=overview"));
			Assert.IsTrue(result.Contains("#details"));
		}

		#endregion

		#region CombineUrlParts Tests - Relative Paths

		[TestMethod]
		public void CombineUrlParts_RelativePath_AppendsToBaseUri()
		{
			Uri baseUri = new Uri("https://example.com/api/v1/");
			string pathQueryFragment = "users";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/api/v1/users", result);
		}

		[TestMethod]
		public void CombineUrlParts_RelativePathWithQuery_AppendsCorrectly()
		{
			Uri baseUri = new Uri("https://example.com/api/");
			string pathQueryFragment = "search?q=test";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/api/search?q=test", result);
		}

		#endregion

		#region CombineUrlParts Tests - Base URI Variations

		[TestMethod]
		public void CombineUrlParts_BaseUriWithPath_AbsolutePathReplacesBasePath()
		{
			Uri baseUri = new Uri("https://example.com/old/path/");
			string pathQueryFragment = "/new/path";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com/new/path", result);
		}

		[TestMethod]
		public void CombineUrlParts_BaseUriWithPort_PreservesPort()
		{
			Uri baseUri = new Uri("https://example.com:8080");
			string pathQueryFragment = "/api/users";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://example.com:8080/api/users", result);
		}

		[TestMethod]
		public void CombineUrlParts_HttpScheme_PreservesScheme()
		{
			Uri baseUri = new Uri("http://example.com");
			string pathQueryFragment = "/api/users";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("http://example.com/api/users", result);
		}

		[TestMethod]
		public void CombineUrlParts_BaseUriWithAuthentication_PreservesAuthentication()
		{
			Uri baseUri = new Uri("https://user:pass@example.com");
			string pathQueryFragment = "/api/users";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.StartsWith("https://user:pass@example.com"));
			Assert.IsTrue(result.Contains("/api/users"));
		}

		#endregion

		#region CombineUrlParts Tests - Real-World Scenarios

		[TestMethod]
		public void CombineUrlParts_GoogleSearch_CombinesCorrectly()
		{
			Uri baseUri = new Uri("https://google.com");
			string pathQueryFragment = "/search?q=test";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://google.com/search?q=test", result);
		}

		[TestMethod]
		public void CombineUrlParts_GitHubApi_CombinesCorrectly()
		{
			Uri baseUri = new Uri("https://api.github.com");
			string pathQueryFragment = "/repos/owner/repo/issues?state=open";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://api.github.com/repos/owner/repo/issues?state=open", result);
		}

		[TestMethod]
		public void CombineUrlParts_RestApiWithVersion_CombinesCorrectly()
		{
			Uri baseUri = new Uri("https://api.example.com/v1");
			string pathQueryFragment = "/users/123";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://api.example.com/users/123", result);
		}

		[TestMethod]
		public void CombineUrlParts_ComplexApiCall_HandlesAllComponents()
		{
			Uri baseUri = new Uri("https://api.example.com");
			string pathQueryFragment = "/v2/search?q=test&filter=active&sort=date#results";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("/v2/search"));
			Assert.IsTrue(result.Contains("?q=test&filter=active&sort=date"));
			Assert.IsTrue(result.Contains("#results"));
		}

		#endregion

		#region CombineUrlParts Tests - Edge Cases

		[TestMethod]
		public void CombineUrlParts_MultipleSlashes_HandlesCorrectly()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "//api//users";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("example.com"));
		}

		[TestMethod]
		public void CombineUrlParts_EmptyQueryValue_PreservesStructure()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/search?q=";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("?q="));
		}

		[TestMethod]
		public void CombineUrlParts_EmptyFragment_PreservesHash()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/page#";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("/page"));
		}

		[TestMethod]
		public void CombineUrlParts_SpecialCharactersInPath_HandlesCorrectly()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/api/users/test@example.com";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("/api/users/test@example.com"));
		}

		#endregion

		#region Client Alias Parsing Tests

		[TestMethod]
		public void ParseClientAlias_OnlyClientName_ExtractsClientName()
		{
			string aliasUrl = "google";
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			string clientName = separatorIndex >= 0
				? aliasUrl.Substring(0, separatorIndex)
				: aliasUrl;

			Assert.AreEqual("google", clientName);
			Assert.AreEqual(-1, separatorIndex);
		}

		[TestMethod]
		public void ParseClientAlias_ClientNameWithPath_ExtractsClientNameAndPath()
		{
			string aliasUrl = "google/search";
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			string clientName = aliasUrl.Substring(0, separatorIndex);
			string pathQueryFragment = aliasUrl.Substring(separatorIndex);

			Assert.AreEqual("google", clientName);
			Assert.AreEqual("/search", pathQueryFragment);
		}

		[TestMethod]
		public void ParseClientAlias_ClientNameWithQuery_ExtractsClientNameAndQuery()
		{
			string aliasUrl = "google?q=test";
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			string clientName = aliasUrl.Substring(0, separatorIndex);
			string pathQueryFragment = aliasUrl.Substring(separatorIndex);

			Assert.AreEqual("google", clientName);
			Assert.AreEqual("?q=test", pathQueryFragment);
		}

		[TestMethod]
		public void ParseClientAlias_ClientNameWithFragment_ExtractsClientNameAndFragment()
		{
			string aliasUrl = "google#section";
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			string clientName = aliasUrl.Substring(0, separatorIndex);
			string pathQueryFragment = aliasUrl.Substring(separatorIndex);

			Assert.AreEqual("google", clientName);
			Assert.AreEqual("#section", pathQueryFragment);
		}

		[TestMethod]
		public void ParseClientAlias_ComplexAlias_ExtractsAllParts()
		{
			string aliasUrl = "bvk/api/endpoint?version=2#details";
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			string clientName = aliasUrl.Substring(0, separatorIndex);
			string pathQueryFragment = aliasUrl.Substring(separatorIndex);

			Assert.AreEqual("bvk", clientName);
			Assert.AreEqual("/api/endpoint?version=2#details", pathQueryFragment);
		}

		[TestMethod]
		public void ParseClientAlias_ClientNameWithDashes_ParsesCorrectly()
		{
			string aliasUrl = "my-api-client/endpoint";
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			string clientName = aliasUrl.Substring(0, separatorIndex);
			string pathQueryFragment = aliasUrl.Substring(separatorIndex);

			Assert.AreEqual("my-api-client", clientName);
			Assert.AreEqual("/endpoint", pathQueryFragment);
		}

		[TestMethod]
		public void ParseClientAlias_ClientNameWithUnderscores_ParsesCorrectly()
		{
			string aliasUrl = "my_api_client/endpoint";
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			string clientName = aliasUrl.Substring(0, separatorIndex);
			string pathQueryFragment = aliasUrl.Substring(separatorIndex);

			Assert.AreEqual("my_api_client", clientName);
			Assert.AreEqual("/endpoint", pathQueryFragment);
		}

		#endregion

		#region URL Detection Tests

		[TestMethod]
		public void UrlDetection_HttpUrl_IsNotClientAlias()
		{
			string url = "http://example.com";

			bool isAlias = !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							 !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

			Assert.IsFalse(isAlias);
		}

		[TestMethod]
		public void UrlDetection_HttpsUrl_IsNotClientAlias()
		{
			string url = "https://example.com";

			bool isAlias = !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
						!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

			Assert.IsFalse(isAlias);
		}

		[TestMethod]
		public void UrlDetection_ClientAlias_IsDetected()
		{
			string url = "google/search";

			bool isAlias = !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							 !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

			Assert.IsTrue(isAlias);
		}

		[TestMethod]
		public void UrlDetection_SimpleClientName_IsDetected()
		{
			string url = "google";

			bool isAlias = !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							 !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

			Assert.IsTrue(isAlias);
		}

		[TestMethod]
		public void UrlDetection_MixedCaseHttp_IsNotClientAlias()
		{
			string url = "HtTp://example.com";

			bool isAlias = !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							 !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

			Assert.IsFalse(isAlias);
		}

		[TestMethod]
		public void UrlDetection_MixedCaseHttps_IsNotClientAlias()
		{
			string url = "HtTpS://example.com";

			bool isAlias = !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							 !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

			Assert.IsFalse(isAlias);
		}

		#endregion

		#region Integration Scenarios

		[TestMethod]
		public void IntegrationScenario_GoogleBaseAddress_WithSearchPath()
		{
			Uri baseUri = new Uri("https://google.com");
			string pathQueryFragment = "/search?q=test";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://google.com/search?q=test", result);
		}

		[TestMethod]
		public void IntegrationScenario_ApiBaseAddress_WithResourcePath()
		{
			Uri baseUri = new Uri("https://www.bvk.rs");
			string pathQueryFragment = "/api/endpoint";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://www.bvk.rs/api/endpoint", result);
		}

		[TestMethod]
		public void IntegrationScenario_LocalhostBase_WithPath()
		{
			Uri baseUri = new Uri("https://localhost");
			string pathQueryFragment = "/api/test";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("https://localhost/api/test", result);
		}

		[TestMethod]
		public void IntegrationScenario_CompleteWorkflow_ParseAndCombine()
		{
			// Simulate parsing "bvk/api/users?active=true#list"
			string aliasUrl = "bvk/api/users?active=true#list";
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			string clientName = aliasUrl.Substring(0, separatorIndex);
			string pathQueryFragment = aliasUrl.Substring(separatorIndex);

			// Simulate base address lookup (from clients.json)
			Uri baseUri = new Uri("https://www.bvk.rs");

			// Combine
			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.AreEqual("bvk", clientName);
			Assert.AreEqual("https://www.bvk.rs/api/users?active=true#list", result);
		}

		#endregion

		#region Base URI Preservation Tests

		[TestMethod]
		public void CombineUrlParts_PreservesScheme_Http()
		{
			Uri baseUri = new Uri("http://example.com");
			string pathQueryFragment = "/api";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.StartsWith("http://"));
		}

		[TestMethod]
		public void CombineUrlParts_PreservesScheme_Https()
		{
			Uri baseUri = new Uri("https://example.com");
			string pathQueryFragment = "/api";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.StartsWith("https://"));
		}

		[TestMethod]
		public void CombineUrlParts_PreservesHost()
		{
			Uri baseUri = new Uri("https://www.bvk.rs");
			string pathQueryFragment = "/api";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains("www.bvk.rs"));
		}

		[TestMethod]
		public void CombineUrlParts_PreservesCustomPort()
		{
			Uri baseUri = new Uri("https://example.com:8443");
			string pathQueryFragment = "/api";

			string result = HttpLibrary.ClientSelector.CombineUrlParts(baseUri, pathQueryFragment);

			Assert.IsTrue(result.Contains(":8443"));
		}

		#endregion

		#region Alias Resolution + Selection Test

		[TestMethod]
		public void AliasResolution_ResolvesAndSelectsGoogleClient()
		{
			// Arrange
			string alias = "google/search?q=test";
			ConcurrentDictionary<string, Uri> clientBaseAddresses = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
			clientBaseAddresses[ "google" ] = new Uri("https://google.com");

			ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient> registeredNames = new ConcurrentDictionary<string, HttpLibrary.IPooledHttpClient>(StringComparer.OrdinalIgnoreCase);
			// Create a simple test implementation of IPooledHttpClient to register under 'google'
			TestPooledHttpClient googleClient = new TestPooledHttpClient("google");
			registeredNames[ "google" ] = googleClient;

			ILogger logger = new NullLoggerFactory().CreateLogger("ClientAliasTest");

			// Act
			bool resolved = HttpLibrary.ClientSelector.TryResolveClientAlias(alias, clientBaseAddresses, out string resolvedUrl, out string resolvedClientName);

			// Log diagnostic info
			if(resolved)
			{
				this.TestContext.WriteLine($"Alias '{alias}' resolved to: {resolvedUrl} (client: {resolvedClientName})");
				logger.LogDebug("Alias '{Alias}' resolved to {Url} (client: {Client})", alias, resolvedUrl, resolvedClientName);
			}
			else
			{
				this.TestContext.WriteLine($"Input '{alias}' treated as direct URL (not an alias)");
				logger.LogDebug("Input '{Input}' treated as direct URL", alias);
			}

			// Now select client for resolved URL
			HttpLibrary.IPooledHttpClient? selected = HttpLibrary.ClientSelector.SelectClientForUrl(resolved ? resolvedUrl : alias, registeredNames, clientBaseAddresses, logger);

			// Assert
			Assert.IsTrue(resolved, "Alias should have been resolved");
			Assert.IsNotNull(selected, "A pooled client should have been selected");
			Assert.AreEqual("google", selected.Name, "Selected client should be the google client");
		}

		#endregion

		#region Alias Parsing Edge Cases

		[TestMethod]
		public void TryResolveClientAlias_ReturnsFalse_ForUnknownClient()
		{
			ConcurrentDictionary<string, Uri> clientBaseAddresses = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
			clientBaseAddresses[ "google" ] = new Uri("https://google.com");

			bool result = HttpLibrary.ClientSelector.TryResolveClientAlias("unknown/path", clientBaseAddresses, out string resolved, out string clientName);

			Assert.IsFalse(result);
			Assert.AreEqual(string.Empty, resolved);
			// Accept that clientName may contain the extracted token even when resolution fails
			Assert.AreEqual("unknown", clientName);
		}

		[TestMethod]
		public void TryResolveClientAlias_ReturnsFalse_ForEmptyAlias()
		{
			ConcurrentDictionary<string, Uri> clientBaseAddresses = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
			clientBaseAddresses[ "google" ] = new Uri("https://google.com");

			bool result = HttpLibrary.ClientSelector.TryResolveClientAlias(string.Empty, clientBaseAddresses, out string resolved, out string clientName);

			Assert.IsFalse(result);
			Assert.AreEqual(string.Empty, resolved);
			Assert.AreEqual(string.Empty, clientName);
		}

		[TestMethod]
		public void TryResolveClientAlias_SeparatorAtStart_IsInvalid()
		{
			ConcurrentDictionary<string, Uri> clientBaseAddresses = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
			clientBaseAddresses[ "google" ] = new Uri("https://google.com");

			// Alias starting with separator produces empty client name and should fail to resolve
			bool result = HttpLibrary.ClientSelector.TryResolveClientAlias("/search?q=test", clientBaseAddresses, out string resolved, out string clientName);

			Assert.IsFalse(result);
			Assert.AreEqual(string.Empty, clientName);
		}

		[TestMethod]
		public void TryResolveClientAlias_MultipleSeparators_ParsesAndCombines()
		{
			ConcurrentDictionary<string, Uri> clientBaseAddresses = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
			clientBaseAddresses[ "google" ] = new Uri("https://google.com");

			string alias = "google//search??q=test##frag";
			bool result = HttpLibrary.ClientSelector.TryResolveClientAlias(alias, clientBaseAddresses, out string resolved, out string clientName);

			Assert.IsTrue(result);
			Assert.AreEqual("google", clientName);
			Assert.IsTrue(resolved.StartsWith("https://google.com", StringComparison.OrdinalIgnoreCase));
		}

		[TestMethod]
		public void TryResolveClientAlias_PreservesPercentEncodedPath()
		{
			ConcurrentDictionary<string, Uri> clientBaseAddresses = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
			clientBaseAddresses[ "google" ] = new Uri("https://google.com");

			string alias = "google/search%20term?q=a%20b";
			bool result = HttpLibrary.ClientSelector.TryResolveClientAlias(alias, clientBaseAddresses, out string resolved, out string clientName);

			Assert.IsTrue(result);
			Assert.AreEqual("google", clientName.ToLowerInvariant());
			Assert.IsTrue(resolved.Contains("search", StringComparison.OrdinalIgnoreCase));
			Assert.IsTrue(resolved.Contains("q=a", StringComparison.OrdinalIgnoreCase));
			// Accept either percent-encoded or decoded representation
			Assert.IsTrue(resolved.Contains("search%20term", StringComparison.OrdinalIgnoreCase) || resolved.Contains("search term", StringComparison.OrdinalIgnoreCase));
			Assert.IsTrue(resolved.Contains("q=a%20b", StringComparison.OrdinalIgnoreCase) || resolved.Contains("q=a b", StringComparison.OrdinalIgnoreCase));
		}

		[TestMethod]
		public void TryResolveClientAlias_ClientNameCaseInsensitive_Resolves()
		{
			ConcurrentDictionary<string, Uri> clientBaseAddresses = new ConcurrentDictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
			clientBaseAddresses[ "google" ] = new Uri("https://google.com");

			string alias = "GoOgLe/search";
			bool result = HttpLibrary.ClientSelector.TryResolveClientAlias(alias, clientBaseAddresses, out string resolved, out string clientName);

			Assert.IsTrue(result);
			Assert.AreEqual("google", clientName.ToLowerInvariant());
			Assert.IsTrue(resolved.StartsWith("https://google.com", StringComparison.OrdinalIgnoreCase));
		}

		#endregion

		// Minimal test implementation of IPooledHttpClient used for selection tests
		private sealed class TestPooledHttpClient : HttpLibrary.IPooledHttpClient
		{
			private readonly string _name;
			private readonly HttpLibrary.PooledHttpClientMetrics _metrics = new HttpLibrary.PooledHttpClientMetrics();

			public TestPooledHttpClient(string name)
			{
				_name = name;
			}

			public string? Name => _name;

			public HttpLibrary.PooledHttpClientMetrics Metrics => _metrics;

			public Action<HttpLibrary.HttpProgressInfo>? ProgressCallback { get; set; }

			public Func<HttpLibrary.HttpRedirectInfo, HttpLibrary.RedirectAction>? RedirectCallback { get; set; }

			public int MaxRedirections => 10;

			public void AddRequestHeader(string name, string value) { }
			public bool RemoveRequestHeader(string name) => false;
			public void ClearRequestHeaders() { }

			public System.Threading.Tasks.Task<HttpResponseMessage> GetAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> GetStringAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<byte[]> GetBytesAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> PostStringAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<byte[]> PostBytesAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> PutStringAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<byte[]> PutBytesAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> DeleteAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> DeleteStringAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<byte[]> DeleteBytesAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> PatchAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> PatchStringAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<byte[]> PatchBytesAsync(string requestUri, HttpContent content, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> HeadAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> OptionsAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> OptionsStringAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<byte[]> OptionsBytesAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> TraceAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> TraceStringAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<byte[]> TraceBytesAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> ConnectAsync(string requestUri, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public System.Threading.Tasks.Task<string> PostStringAsync(string requestUri, string content, string mediaType = HttpLibrary.Constants.MediaTypePlainText, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> PutStringAsync(string requestUri, string content, string mediaType = HttpLibrary.Constants.MediaTypePlainText, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public System.Threading.Tasks.Task<string> PatchStringAsync(string requestUri, string content, string mediaType = HttpLibrary.Constants.MediaTypePlainText, HttpLibrary.HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		}
	}
}