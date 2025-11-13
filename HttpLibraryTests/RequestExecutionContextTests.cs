using HttpLibrary;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class RequestExecutionContextTests
	{
		class TestLogger : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => true;
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
		}

		[TestMethod]
		public async Task ExecuteAsync_WithTimeout_ThrowsOperationCanceledException()
		{
			TestLogger logger = new TestLogger();
			using RequestExecutionContext context = new RequestExecutionContext(
				TimeSpan.FromMilliseconds(100),
				CancellationToken.None,
				logger,
				TimeSpan.FromSeconds(30));

			try
			{
				await context.ExecuteAsync(async (ct) =>
				{
					await Task.Delay(1000, ct);
					return 42;
				}, "Test operation");
				Assert.Fail("Expected OperationCanceledException was not thrown");
			}
			catch(OperationCanceledException)
			{
				// expected
			}
		}

		[TestMethod]
		public async Task ExecuteAsync_WithParentCancellation_ThrowsOperationCanceledException()
		{
			TestLogger logger = new TestLogger();
			CancellationTokenSource cts = new CancellationTokenSource();
			using RequestExecutionContext context = new RequestExecutionContext(
				TimeSpan.FromSeconds(30),
				cts.Token,
				logger,
				TimeSpan.FromSeconds(30));

			try
			{
				await context.ExecuteAsync(async (ct) =>
				{
					cts.Cancel();
					await Task.Delay(100, ct);
					return 42;
				}, "Test operation");
				Assert.Fail("Expected OperationCanceledException was not thrown");
			}
			catch(OperationCanceledException)
			{
				// expected
			}
		}

		[TestMethod]
		public async Task ExecuteAsync_SuccessfulOperation_ReturnsResult()
		{
			TestLogger logger = new TestLogger();
			using RequestExecutionContext context = new RequestExecutionContext(
				TimeSpan.FromSeconds(30),
				CancellationToken.None,
				logger,
				TimeSpan.FromSeconds(30));

			int result = await context.ExecuteAsync(async (ct) =>
			{
				await Task.Delay(10, ct);
				return 42;
			}, "Test operation");

			Assert.AreEqual(42, result);
		}

		[TestMethod]
		public void Dispose_MultipleTimesDoesNotThrow()
		{
			TestLogger logger = new TestLogger();
			RequestExecutionContext context = new RequestExecutionContext(
				TimeSpan.FromSeconds(30),
				CancellationToken.None,
				logger,
				TimeSpan.FromSeconds(30));

			context.Dispose();
			context.Dispose();
		}
	}
}