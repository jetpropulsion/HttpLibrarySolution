using HttpLibrary;

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
	[TestClass]
	public class HttpRequestExecutorRedirectFlowsTests
	{
		class TestLogger : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => true;
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
		}

		// Capturing logger for asserting log messages in unit tests
		private class CapturingLogger : ILogger
		{
			public record LogEntry(LogLevel Level, string Message);
			public readonly System.Collections.Generic.List<LogEntry> Entries = new System.Collections.Generic.List<LogEntry>();
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => true;
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
			{
				try
				{
					string message = formatter(state, exception);
					Entries.Add(new LogEntry(logLevel, message));
				}
				catch
				{
					// Swallow formatting exceptions in tests
				}
			}
		}

		private class SequencePooledHttpClient : IPooledHttpClient
		{
			private readonly Queue<HttpResponseMessage> _responses;

			public SequencePooledHttpClient(IEnumerable<HttpResponseMessage> responses, string? name = null)
			{
				_responses = new Queue<HttpResponseMessage>(responses);
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

			// Only SendRawAsync is required by tests; other members throw if used
			public Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
			{
				if(_responses.Count == 0)
				{
					throw new InvalidOperationException("No more responses configured in SequencePooledHttpClient");
				}

				HttpResponseMessage next = _responses.Dequeue();
				next.RequestMessage = request;
				return Task.FromResult(next);
			}

			// Unused interface members - implement as not supported for these tests
			public Task<string> GetStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<byte[]> GetBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<HttpResponseMessage> GetAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

			public Task<string> PostStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<string> PostStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<byte[]> PostBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

			public Task<string> PutStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<string> PutStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<byte[]> PutBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

			public Task<string> DeleteStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<byte[]> DeleteBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<HttpResponseMessage> DeleteAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

			public Task<string> PatchStringAsync(string requestUri, string content, string mediaType = Constants.MediaTypePlainText, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<string> PatchStringAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<byte[]> PatchBytesAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<HttpResponseMessage> PatchAsync(string requestUri, HttpContent content, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

			public Task<HttpResponseMessage> HeadAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<string> OptionsStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<byte[]> OptionsBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<HttpResponseMessage> OptionsAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

			public Task<string> TraceStringAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<byte[]> TraceBytesAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<HttpResponseMessage> TraceAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

			public Task<HttpResponseMessage> ConnectAsync(string requestUri, HttpRequestHeaders? headers = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
			public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		}

		[TestMethod]
		public async Task HttpRequestExecutor_GetAsync_FollowsRedirect_ReturnsFinalBody()
		{
			TestLogger logger = new TestLogger();

			HttpResponseMessage redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
			redirectResponse.Headers.Location = new Uri("https://example.com/redirected");

			HttpResponseMessage finalResponse = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("Final content")
			};

			SequencePooledHttpClient client = new SequencePooledHttpClient(new[] { redirectResponse, finalResponse }, name: "seq");

			string result = await HttpRequestExecutor.GetAsync(client, logger, "https://example.com/start");

			Assert.AreEqual("Final content", result);
		}

		[TestMethod]
		public async Task HttpRequestExecutor_GetAsync_FollowsRelativeRedirect_ReturnsFinalBody()
		{
			TestLogger logger = new TestLogger();

			HttpResponseMessage redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
			redirectResponse.Headers.Location = new Uri("/relative-path", UriKind.Relative);

			HttpResponseMessage finalResponse = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("Relative final")
			};

			SequencePooledHttpClient client = new SequencePooledHttpClient(new[] { redirectResponse, finalResponse }, name: "seq");

			string result = await HttpRequestExecutor.GetAsync(client, logger, "https://example.com/base/path");

			Assert.AreEqual("Relative final", result);
		}

		[TestMethod]
		public async Task HttpRequestExecutor_PostAsync_303ChangesToGet_ReturnsFinalBody()
		{
			TestLogger logger = new TestLogger();

			HttpResponseMessage redirectResponse = new HttpResponseMessage(HttpStatusCode.SeeOther); //303
			redirectResponse.Headers.Location = new Uri("https://example.com/after-post");

			HttpResponseMessage finalResponse = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("Post->Get final")
			};

			SequencePooledHttpClient client = new SequencePooledHttpClient(new[] { redirectResponse, finalResponse }, name: "seq-post-303");

			string result = await HttpRequestExecutor.PostAsync(client, logger, "https://example.com/submit", new StringContent("payload"));

			Assert.AreEqual("Post->Get final", result);
		}

		[TestMethod]
		public async Task HttpRequestExecutor_RedirectCallback_Cancel_ThrowsOperationCanceled()
		{
			TestLogger logger = new TestLogger();

			HttpResponseMessage redirectResponse = new HttpResponseMessage(HttpStatusCode.Found);
			redirectResponse.Headers.Location = new Uri("https://example.com/redirected");

			SequencePooledHttpClient client = new SequencePooledHttpClient(new[] { redirectResponse }, name: "seq-cancel");

			client.RedirectCallback = (info) =>
			{
				// Cancel the redirect
				return RedirectAction.Cancel;
			};

			try
			{
				await HttpRequestExecutor.GetAsync(client, logger, "https://example.com/start");
				Assert.Fail("Expected OperationCanceledException was not thrown");
			}
			catch(OperationCanceledException)
			{
				// expected
			}
		}

		[TestMethod]
		public async Task HttpRequestExecutor_RedirectCallback_Stop_ReturnsEmpty()
		{
			TestLogger logger = new TestLogger();

			HttpResponseMessage redirectResponse = new HttpResponseMessage(HttpStatusCode.Redirect);
			redirectResponse.Headers.Location = new Uri("https://example.com/redirected");

			SequencePooledHttpClient client = new SequencePooledHttpClient(new[] { redirectResponse }, name: "seq-stop");

			client.RedirectCallback = (info) =>
			{
				// Stop following redirects
				return RedirectAction.Stop;
			};

			string result = await HttpRequestExecutor.GetAsync(client, logger, "https://example.com/start");

			Assert.AreEqual(string.Empty, result);
		}

		[TestMethod]
		public async Task HttpRequestExecutor_LongRedirectChain_ExceedsMaxRedirections_ReturnsEmpty()
		{
			CapturingLogger logger = new CapturingLogger();

			// Create a chain of redirects longer than the client's MaxRedirections (default10)
			List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
			for(int i = 0; i < 12; i++)
			{
				HttpResponseMessage redirect = new HttpResponseMessage(HttpStatusCode.Found);
				redirect.Headers.Location = new Uri($"https://example.com/redirect{i}");
				responses.Add(redirect);
			}

			SequencePooledHttpClient client = new SequencePooledHttpClient(responses, name: "seq-long");

			string result = await HttpRequestExecutor.GetAsync(client, logger, "https://example.com/start");

			// Expect empty result because MaxRedirections limit should stop following chain
			Assert.AreEqual(string.Empty, result);

			// Verify that a warning about maximum redirect limit was logged
			Assert.IsTrue(logger.Entries.Exists(e => e.Level == LogLevel.Warning && e.Message.Contains("Maximum redirect limit")), "Expected maximum redirect limit warning to be logged");
		}

		[TestMethod]
		public async Task HttpRequestExecutor_RedirectLoop_DetectedByMaxRedirections_ReturnsEmpty()
		{
			CapturingLogger logger = new CapturingLogger();

			// Simulate a loop: A -> B -> A -> B ... by enqueuing alternate Location headers
			List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
			for(int i = 0; i < 15; i++)
			{
				HttpResponseMessage redirect = new HttpResponseMessage(HttpStatusCode.Found);
				if(i % 2 == 0)
				{
					redirect.Headers.Location = new Uri("https://example.com/A");
				}
				else
				{
					redirect.Headers.Location = new Uri("https://example.com/B");
				}
				responses.Add(redirect);
			}

			SequencePooledHttpClient client = new SequencePooledHttpClient(responses, name: "seq-loop");

			string result = await HttpRequestExecutor.GetAsync(client, logger, "https://example.com/A");

			// Library detects loop only via MaxRedirections; result should be empty when limit reached
			Assert.AreEqual(string.Empty, result);

			// Verify that either a max redirect limit warning or a loop-detection warning was logged
			Assert.IsTrue(
				logger.Entries.Exists(e => e.Level == LogLevel.Warning && e.Message.Contains("Maximum redirect limit"))
				|| logger.Entries.Exists(e => e.Level == LogLevel.Warning && e.Message.Contains("Redirect loop detected")),
				"Expected either maximum redirect limit or redirect loop detection warning to be logged");
		}

		[TestMethod]
		public async Task HttpRequestExecutor_RedirectLoop_EarlyDetection_ReturnsEmpty()
		{
			CapturingLogger logger = new CapturingLogger();

			// Simulate A -> B -> A loop
			HttpResponseMessage first = new HttpResponseMessage(HttpStatusCode.Found);
			first.Headers.Location = new Uri("https://example.com/B");

			HttpResponseMessage second = new HttpResponseMessage(HttpStatusCode.Found);
			second.Headers.Location = new Uri("https://example.com/A");

			SequencePooledHttpClient client = new SequencePooledHttpClient(new[] { first, second }, name: "seq-loop-early");

			string result = await HttpRequestExecutor.GetAsync(client, logger, "https://example.com/A");

			// Loop A -> B -> A should be detected and return empty early
			Assert.AreEqual(string.Empty, result);

			// Verify that a loop-detection warning was logged
			Assert.IsTrue(logger.Entries.Exists(e => e.Level == LogLevel.Warning && e.Message.Contains("Redirect loop detected")), "Expected redirect loop detection warning to be logged");
		}
	}
}