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
	public class HttpRequestExecutorStringFlowsTests
	{
		class TestLogger : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => true;
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
		}

		private class TestPooledHttpClient : IPooledHttpClient
		{
			private readonly HttpResponseMessage _response;

			public TestPooledHttpClient(HttpResponseMessage response, string? name = null)
			{
				_response = response;
				Name = name;
				Metrics = new PooledHttpClientMetrics();
			}

			public string? Name { get; }
			public PooledHttpClientMetrics Metrics { get; }
			public Action<HttpProgressInfo>? ProgressCallback { get; set; }
			public Func<HttpRedirectInfo, RedirectAction>? RedirectCallback { get; set; }
			public int MaxRedirections => 10;

			public void AddRequestHeader(string name, string value) { }
			public bool RemoveRequestHeader(string name) => false;
			public void ClearRequestHeaders() { }

			public Task<string> GetStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<byte[]> GetBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<HttpResponseMessage> GetAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<string> PostStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<byte[]> PostBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<string> PutStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<byte[]> PutBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<string> DeleteStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<byte[]> DeleteBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<HttpResponseMessage> DeleteAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<string> PatchStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<byte[]> PatchBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<HttpResponseMessage> PatchAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<HttpResponseMessage> HeadAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<string> OptionsStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<byte[]> OptionsBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<HttpResponseMessage> OptionsAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<string> TraceStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<byte[]> TraceBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<HttpResponseMessage> TraceAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<HttpResponseMessage> ConnectAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

			public Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
			{
				// Return a clone to avoid disposal issues
				HttpResponseMessage clone = new HttpResponseMessage(_response.StatusCode)
				{
					Content = _response.Content == null ? null : new StringContent(_response.Content.ReadAsStringAsync().GetAwaiter().GetResult()),
					RequestMessage = request
				};
				foreach(var h in _response.Headers)
				{
					clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
				}
				if(_response.Content?.Headers != null)
				{
					foreach(var h in _response.Content.Headers)
					{
						clone.Content?.Headers.TryAddWithoutValidation(h.Key, h.Value);
					}
				}

				return Task.FromResult(clone);
			}

			public Task<string> PostStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<string> PutStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<string> PatchStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		}

		[TestMethod]
		public async Task GetAsync_ReturnsResponseBody()
		{
			TestLogger logger = new TestLogger();
			HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("Hello from server")
			};

			TestPooledHttpClient client = new TestPooledHttpClient(response, "test");

			string result = await HttpRequestExecutor.GetAsync(client, logger, "https://example.com");

			Assert.AreEqual("Hello from server", result);
		}

		[TestMethod]
		public async Task PostAsync_ReturnsResponseBody()
		{
			TestLogger logger = new TestLogger();
			HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("Posted ok")
			};

			TestPooledHttpClient client = new TestPooledHttpClient(response, "test");

			HttpContent content = new StringContent("data");
			string result = await HttpRequestExecutor.PostAsync(client, logger, "https://example.com/api", content);

			Assert.AreEqual("Posted ok", result);
		}

		[TestMethod]
		public async Task NonSuccessResponse_ReturnsEmptyString()
		{
			TestLogger logger = new TestLogger();
			HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
			{
				Content = new StringContent("server error")
			};

			TestPooledHttpClient client = new TestPooledHttpClient(response, "test");

			string result = await HttpRequestExecutor.GetAsync(client, logger, "https://example.com");

			Assert.AreEqual(string.Empty, result);
		}
	}
}