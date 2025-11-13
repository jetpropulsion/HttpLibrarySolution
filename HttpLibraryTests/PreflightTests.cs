using HttpLibrary;
using HttpLibrary.Models;

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
	public class PreflightTests
	{
		class TestLogger : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => true;
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
		}

		class FakePooledHttpClient : IPooledHttpClient
		{
			public string? Name => "fake";
			public PooledHttpClientMetrics Metrics => new PooledHttpClientMetrics();
			public Action<HttpProgressInfo>? ProgressCallback { get; set; }
			public Func<HttpRedirectInfo, RedirectAction>? RedirectCallback { get; set; }
			public int MaxRedirections => 10;

			public void AddRequestHeader(string name, string value) { }
			public bool RemoveRequestHeader(string name) => false;
			public void ClearRequestHeaders() { }

			// Only SendRawAsync is used by the test; other methods implemented to satisfy the interface.
			public Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
			{
				HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);
				resp.Headers.Add("Access-Control-Allow-Origin", "https://example.com");
				resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
				resp.Headers.Add("Access-Control-Allow-Headers", "X-Custom-Header, Content-Type");
				resp.Headers.Add("Access-Control-Allow-Credentials", "true");
				return Task.FromResult(resp);
			}

			// The rest of the interface methods are not used in this test and throw if called.
			public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
			public Task<string> PostStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<string> PutStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
			public Task<string> PatchStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
		}

		[TestMethod]
		public async Task SendPreflightAsync_ParsesAccessControlHeaders()
		{
			TestLogger logger = new TestLogger();
			FakePooledHttpClient client = new FakePooledHttpClient();

			PreflightResult res = await HttpRequestExecutor.SendPreflightAsync(client, logger, "https://example.com/resource", "https://origin.example", "POST", "X-Custom-Header");

			Assert.IsNotNull(res);
			Assert.AreEqual("https://example.com", res.AllowOrigin);
			Assert.AreEqual("GET, POST, OPTIONS", res.AllowMethods);
			Assert.AreEqual("X-Custom-Header, Content-Type", res.AllowHeaders);
			Assert.IsTrue(res.AllowCredentials.HasValue && res.AllowCredentials.Value);
		}
	}
}