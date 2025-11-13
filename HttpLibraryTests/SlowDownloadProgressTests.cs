using HttpLibrary;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class DownloadToFileAsync_ReportsProgress_MultipleInvocations
	{
		private string? _testDirectory;
		private string? _outputFile;

		[TestInitialize]
		public void Init()
		{
			_testDirectory = Path.Combine(Path.GetTempPath(), $"HttpLibraryTests_{Guid.NewGuid():N}");
			Directory.CreateDirectory(_testDirectory);
			_outputFile = Path.Combine(_testDirectory, "out.bin");
		}

		[TestCleanup]
		public void Cleanup()
		{
			try
			{
				if(!string.IsNullOrWhiteSpace(_testDirectory) && Directory.Exists(_testDirectory))
				{
					Directory.Delete(_testDirectory, recursive: true);
				}
			}
			catch
			{
				// ignore
			}
		}

		[TestMethod]
		public async Task DownloadToFileAsync_ReportsProgress_MultipleInvocations_Test()
		{
			// Arrange
			int totalSize = 200 * 1024; //200 KB
			int chunkSize = 16 * 1024; //16 KB
			int delayMs = 30; // small delay to ensure streaming

			SlowStream slowStream = new SlowStream(totalSize, chunkSize, delayMs);
			HttpResponseMessage response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
			response.Content = new StreamContent(slowStream, Constants.StreamBufferSize);
			response.Content.Headers.ContentLength = totalSize;

			TestHttpMessageHandler handler = new TestHttpMessageHandler((request, token) => Task.FromResult(response));
			TestPooledHttpClient client = new TestPooledHttpClient(handler, "fake");

			int progressCalls = 0;
			client.ProgressCallback = (info) =>
			{
				if(info.Stage == HttpProgressStage.DownloadingContent)
				{
					System.Threading.Interlocked.Increment(ref progressCalls);
				}
			};

			ILogger logger = new TestLogger();

			// Act
			long bytes = await HttpRequestExecutor.DownloadToFileAsync(client, logger, "https://example.test/file.bin", _outputFile!, TimeSpan.FromSeconds(30)).ConfigureAwait(false);

			// Assert
			Assert.IsTrue(File.Exists(_outputFile!), "Output file should exist after download");
			Assert.AreEqual(totalSize, bytes, "Downloaded byte count should match total size");
			Assert.IsTrue(progressCalls > 1, $"Expected multiple progress calls but got {progressCalls}");
		}

		private sealed class TestLogger : Microsoft.Extensions.Logging.ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
			public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
		}

		private sealed class SlowStream : Stream
		{
			private readonly byte[] _data;
			private readonly int _chunkSize;
			private readonly int _delayMs;
			private int _position;

			public SlowStream(int totalSize, int chunkSize, int delayMs)
			{
				_data = new byte[ totalSize ];
				Random rnd = new Random(42);
				rnd.NextBytes(_data);
				_chunkSize = chunkSize;
				_delayMs = delayMs;
				_position = 0;
			}

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => _data.Length;
			public override long Position { get => _position; set => throw new NotSupportedException(); }

			public override void Flush() { }
			public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

			public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
			{
				if(_position >= _data.Length)
				{
					return 0;
				}

				int toCopy = Math.Min(_chunkSize, Math.Min(buffer.Length, _data.Length - _position));
				await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
				// copy from internal buffer to target memory
				_data.AsSpan(_position, toCopy).CopyTo(buffer.Span);
				_position += toCopy;
				return toCopy;
			}

			public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}
	}
}