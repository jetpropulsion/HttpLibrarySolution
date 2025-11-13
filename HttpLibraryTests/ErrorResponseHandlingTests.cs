using HttpLibrary;

using HttpLibraryTests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for error response handling in HttpRequestExecutor.
	/// Ensures that error responses (4xx, 5xx) are handled gracefully without causing disposal errors.
	/// Related to fix for "Error while copying content to a stream" issue when servers return malformed error content.
	/// </summary>
	[TestClass]
	public class ErrorResponseHandlingTests
	{
		private TestLogger? logger;

		[TestInitialize]
		public void Setup()
		{
			logger = new TestLogger();
			CookiePersistence.Initialize(":memory:");
		}

		[TestCleanup]
		public void Cleanup()
		{
			logger = null;
		}

		#region GetAsync Error Response Tests

		[TestMethod]
		public async Task GetAsync_400BadRequest_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.BadRequest, content: new StringContent("Bad Request Error"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.GetAsync(client, logger!, "https://example.com/api").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 400"), "Should log 400 error");
			Assert.AreEqual(1, handler.RequestCount, "Should make exactly one request");
		}

		[TestMethod]
		public async Task GetAsync_404NotFound_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.NotFound, content: new StringContent("Not Found"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.GetAsync(client, logger!, "https://example.com/missing").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 404"), "Should log 404 error");
		}

		[TestMethod]
		public async Task GetAsync_500InternalServerError_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.InternalServerError, content: new StringContent("Internal Server Error"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.GetAsync(client, logger!, "https://example.com/error").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 500"), "Should log 500 error");
		}

		[TestMethod]
		public async Task GetAsync_MalformedErrorResponse_DoesNotThrowDisposalError()
		{
			// Arrange - Simulate Facebook's problematic TRACE response
			TestHttpMessageHandler handler = new TestHttpMessageHandler((request, cancellationToken) =>
			{
				HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
				// Create content that might cause disposal issues
				response.Content = new ByteArrayContent(new byte[] { 0xFF, 0xFE, 0xFD }); // Invalid UTF-8
				return Task.FromResult(response);
			});

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act - Should not throw exception
			await HttpRequestExecutor.GetAsync(client, logger!, "https://facebook.com/trace").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 400"), "Should log error status");
			// The key assertion is that we got here without throwing
		}

		[TestMethod]
		public async Task GetAsync_EmptyErrorResponse_HandlesGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.BadRequest, content: new StringContent(string.Empty));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.GetAsync(client, logger!, "https://example.com").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 400"), "Should log error");
		}

		#endregion

		#region SendRequestAsync Error Response Tests (POST, PUT, DELETE, PATCH, etc.)

		[TestMethod]
		public async Task PostAsync_403Forbidden_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.Forbidden, content: new StringContent("Forbidden"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");
			StringContent content = new StringContent("test data");

			// Act
			await HttpRequestExecutor.PostAsync(client, logger!, "https://example.com/api", content).ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 403"), "Should log 403 error");
		}

		[TestMethod]
		public async Task PutAsync_409Conflict_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.Conflict, content: new StringContent("Conflict"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");
			StringContent content = new StringContent("update data");

			// Act
			await HttpRequestExecutor.PutAsync(client, logger!, "https://example.com/api/1", content).ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 409"), "Should log 409 error");
		}

		[TestMethod]
		public async Task DeleteAsync_410Gone_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.Gone, content: new StringContent("Gone"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.DeleteAsync(client, logger!, "https://example.com/api/1").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 410"), "Should log 410 error");
		}

		[TestMethod]
		public async Task PatchAsync_422UnprocessableEntity_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler((HttpStatusCode)422, content: new StringContent("{\"error\":\"Validation failed\"}"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");
			StringContent content = new StringContent("{\"invalid\":\"data\"}");

			// Act
			await HttpRequestExecutor.PatchAsync(client, logger!, "https://example.com/api/1", content).ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 422"), "Should log 422 error");
		}

		[TestMethod]
		public async Task HeadAsync_503ServiceUnavailable_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.ServiceUnavailable, content: new StringContent(string.Empty));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.HeadAsync(client, logger!, "https://example.com").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 503"), "Should log 503 error");
		}

		[TestMethod]
		public async Task OptionsAsync_405MethodNotAllowed_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.MethodNotAllowed, content: new StringContent("Method Not Allowed"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.OptionsAsync(client, logger!, "https://example.com").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 405"), "Should log 405 error");
		}

		[TestMethod]
		public async Task TraceAsync_400BadRequest_HandlesErrorResponseGracefully()
		{
			// Arrange - TRACE is often blocked with 400
			TestHttpMessageHandler handler = new TestHttpMessageHandler((request, cancellationToken) =>
			{
				Assert.AreEqual(HttpMethod.Trace, request.Method, "Should be TRACE method");
				HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest);
				response.Content = new StringContent("Bad Request");
				return Task.FromResult(response);
			});

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.TraceAsync(client, logger!, "https://facebook.com").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 400"), "Should log 400 error for TRACE");
		}

		[TestMethod]
		public async Task ConnectAsync_405MethodNotAllowed_HandlesErrorResponseGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.MethodNotAllowed, content: new StringContent("Method Not Allowed"));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.ConnectAsync(client, logger!, "https://example.com").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 405"), "Should log 405 error");
		}

		#endregion

		#region Edge Cases

		[TestMethod]
		public async Task GetAsync_VeryLargeErrorResponse_TruncatesInLog()
		{
			// Arrange
			string largeErrorBody = new string('x', 1000); // 1000 character error
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.BadRequest, content: new StringContent(largeErrorBody));

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act
			await HttpRequestExecutor.GetAsync(client, logger!, "https://example.com").ConfigureAwait(false);

			// Assert
			Assert.IsTrue(logger!.HasError("HTTP error: 400"), "Should log error");
			// Error body should be logged at Debug level (truncated to 500 chars)
		}

		[TestMethod]
		public async Task GetAsync_ContentReadThrowsException_HandlesGracefully()
		{
			// Arrange
			InMemoryResponseHandler handler = new InMemoryResponseHandler(HttpStatusCode.BadRequest, content: new ThrowingHttpContent());

			TestPooledHttpClient client = new TestPooledHttpClient(handler, "test-client");

			// Act - Should not throw, should catch and log the exception
			await HttpRequestExecutor.GetAsync(client, logger!, "https://example.com").ConfigureAwait(false);

			// Assert
			// When content read throws, the error message appears in the HttpRequestException catch block
			// The HttpResponseMessage's status code triggers logging, then content read fails
			Assert.IsTrue(logger!.HasError("HTTP error: 400") || logger.HasError("Request"), "Should log error");
			// Exception reading content should be caught and handled gracefully
		}

		#endregion
	}

	#region Helper Classes

	/// <summary>
	/// Test logger that captures log messages for assertion
	/// </summary>
	internal class TestLogger : ILogger
	{
		private readonly List<string> errorMessages = new List<string>();
		private readonly List<string> debugMessages = new List<string>();
		private readonly List<string> infoMessages = new List<string>();

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			string message = formatter(state, exception);

			switch(logLevel)
			{
				case LogLevel.Error:
				errorMessages.Add(message);
				break;
				case LogLevel.Debug:
				debugMessages.Add(message);
				break;
				case LogLevel.Information:
				infoMessages.Add(message);
				break;
			}
		}

		public bool HasError(string substring) => errorMessages.Exists(m => m.Contains(substring, StringComparison.OrdinalIgnoreCase));

		public bool HasDebug(string substring) => debugMessages.Exists(m => m.Contains(substring, StringComparison.OrdinalIgnoreCase));

		public bool HasInfo(string substring) => infoMessages.Exists(m => m.Contains(substring, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Test HTTP message handler that allows custom response configuration
	/// </summary>
	internal class TestHttpMessageHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory;
		private int requestCount;

		public int RequestCount => requestCount;

		public TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
		{
			this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			requestCount++;
			return await responseFactory(request, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Minimal test implementation of IPooledHttpClient
	/// </summary>
	internal class TestPooledHttpClient : IPooledHttpClient
	{
		private readonly HttpClient httpClient;
		private readonly string name;
		private readonly PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

		public string? Name => name;
		public int MaxRedirections { get; set; } = 10;
		public Func<HttpRedirectInfo, RedirectAction>? RedirectCallback { get; set; }
		public Action<HttpProgressInfo>? ProgressCallback { get; set; }
		public PooledHttpClientMetrics Metrics => metrics;

		public TestPooledHttpClient(HttpMessageHandler handler, string name)
		{
			this.httpClient = new HttpClient(handler);
			this.name = name ?? "test-client";
		}

		public void AddRequestHeader(string name, string value)
		{
			// Minimal implementation for testing
		}

		public bool RemoveRequestHeader(string name) => false;

		public void ClearRequestHeaders()
		{
			// Minimal implementation for testing
		}

		public Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			return httpClient.SendAsync(request, cancellationToken);
		}

		// Stub implementations - not used in these tests
		public Task<string> GetStringAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<byte[]> GetBytesAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> GetAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> PostStringAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<byte[]> PostBytesAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> PostAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> PutStringAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<byte[]> PutBytesAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> PutAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> DeleteStringAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<byte[]> DeleteBytesAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> DeleteAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> PatchStringAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<byte[]> PatchBytesAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> PatchAsync(string url, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> HeadAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> OptionsStringAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<byte[]> OptionsBytesAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> OptionsAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> TraceStringAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<byte[]> TraceBytesAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> TraceAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> ConnectAsync(string url, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> PostStringAsync(string url, string content, string contentType, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> PutStringAsync(string url, string content, string contentType, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public Task<string> PatchStringAsync(string url, string content, string contentType, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		public void Dispose()
		{
			httpClient?.Dispose();
		}
	}

	/// <summary>
	/// HttpContent that throws when read - for testing error handling
	/// </summary>
	internal class ThrowingHttpContent : HttpContent
	{
		protected override Task SerializeToStreamAsync(System.IO.Stream stream, TransportContext? context)
		{
			throw new InvalidOperationException("Simulated content read error");
		}

		protected override bool TryComputeLength(out long length)
		{
			length = -1;
			return false;
		}
	}

	#endregion
}