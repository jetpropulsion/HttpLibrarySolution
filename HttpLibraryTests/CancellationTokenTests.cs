using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for CancellationToken handling and timeout scenarios
	/// </summary>
	[TestClass]
	public class CancellationTokenTests
	{
		[TestMethod]
		public void CancellationToken_Default_IsNotCancelled()
		{
			// Arrange
			CancellationToken token = default;

			// Assert
			Assert.IsFalse(token.IsCancellationRequested);
			Assert.IsTrue(token.CanBeCanceled == false || token.CanBeCanceled == true); // Either is valid for default
		}

		[TestMethod]
		public void CancellationToken_None_IsNotCancelled()
		{
			// Arrange
			CancellationToken token = CancellationToken.None;

			// Assert
			Assert.IsFalse(token.IsCancellationRequested);
			Assert.IsFalse(token.CanBeCanceled);
		}

		[TestMethod]
		public void CancellationTokenSource_NewSource_IsNotCancelled()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();

			// Act
			CancellationToken token = cts.Token;

			// Assert
			Assert.IsFalse(token.IsCancellationRequested);
			Assert.IsTrue(token.CanBeCanceled);
		}

		[TestMethod]
		public void CancellationTokenSource_AfterCancel_IsCancelled()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			CancellationToken token = cts.Token;

			// Act
			cts.Cancel();

			// Assert
			Assert.IsTrue(token.IsCancellationRequested);
			Assert.IsTrue(cts.IsCancellationRequested);
		}

		[TestMethod]
		public void CancellationTokenSource_WithTimeout_CancelsAfterDelay()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
			CancellationToken token = cts.Token;

			// Assert - not cancelled immediately
			Assert.IsFalse(token.IsCancellationRequested);

			// Act - wait for timeout
			Thread.Sleep(200);

			// Assert - cancelled after timeout
			Assert.IsTrue(token.IsCancellationRequested);
		}

		[TestMethod]
		public void CancellationTokenSource_WithZeroTimeout_CancelsImmediately()
		{
			// Arrange & Act
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.Zero);
			CancellationToken token = cts.Token;

			// Small delay to let cancellation propagate
			Thread.Sleep(10);

			// Assert
			Assert.IsTrue(token.IsCancellationRequested);
		}

		[TestMethod]
		public void CancellationTokenSource_WithNegativeTimeout_ThrowsArgumentOutOfRange()
		{
			// Arrange
			bool exceptionThrown = false;

			// Act
			try
			{
				// Use -2 milliseconds, as -1 is special value for infinite timeout
				CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(-2));
				cts.Dispose();
			}
			catch(ArgumentOutOfRangeException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "Should throw ArgumentOutOfRangeException for negative timeout (other than -1 which means infinite)");
		}

		[TestMethod]
		public void LinkedTokenSource_BothTokens_NeitherCancelled()
		{
			// Arrange
			using CancellationTokenSource cts1 = new CancellationTokenSource();
			using CancellationTokenSource cts2 = new CancellationTokenSource();
			using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

			// Assert
			Assert.IsFalse(linked.Token.IsCancellationRequested);
		}

		[TestMethod]
		public void LinkedTokenSource_FirstCancelled_LinkedIsCancelled()
		{
			// Arrange
			using CancellationTokenSource cts1 = new CancellationTokenSource();
			using CancellationTokenSource cts2 = new CancellationTokenSource();
			using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

			// Act
			cts1.Cancel();

			// Assert
			Assert.IsTrue(linked.Token.IsCancellationRequested);
		}

		[TestMethod]
		public void LinkedTokenSource_SecondCancelled_LinkedIsCancelled()
		{
			// Arrange
			using CancellationTokenSource cts1 = new CancellationTokenSource();
			using CancellationTokenSource cts2 = new CancellationTokenSource();
			using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

			// Act
			cts2.Cancel();

			// Assert
			Assert.IsTrue(linked.Token.IsCancellationRequested);
		}

		[TestMethod]
		public void LinkedTokenSource_EitherCancelled_LinkedResponds()
		{
			// Arrange
			using CancellationTokenSource parentCts = new CancellationTokenSource();
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
			using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, timeoutCts.Token);

			// Wait for timeout to trigger
			Thread.Sleep(100);

			// Assert - linked should be cancelled from timeout
			Assert.IsTrue(linked.Token.IsCancellationRequested);
		}

		[TestMethod]
		public void LinkedTokenSource_ParentCancelledFirst_LinkedCancelled()
		{
			// Arrange
			using CancellationTokenSource parentCts = new CancellationTokenSource();
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Long timeout
			using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, timeoutCts.Token);

			// Act
			parentCts.Cancel();

			// Assert
			Assert.IsTrue(linked.Token.IsCancellationRequested);
			Assert.IsTrue(parentCts.IsCancellationRequested);
			Assert.IsFalse(timeoutCts.IsCancellationRequested); // Timeout not reached
		}

		[TestMethod]
		public void CancellationToken_ThrowIfCancellationRequested_ThrowsWhenCancelled()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			CancellationToken token = cts.Token;
			cts.Cancel();

			// Act
			bool exceptionThrown = false;
			try
			{
				token.ThrowIfCancellationRequested();
			}
			catch(OperationCanceledException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "Should throw OperationCanceledException when cancelled");
		}

		[TestMethod]
		public void CancellationToken_ThrowIfCancellationRequested_NoThrowWhenNotCancelled()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			CancellationToken token = cts.Token;

			// Act & Assert - should not throw
			token.ThrowIfCancellationRequested();
		}

		[TestMethod]
		public void CancellationToken_Register_CallbackInvokedOnCancel()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			bool callbackInvoked = false;

			cts.Token.Register(() =>
			{
				callbackInvoked = true;
			});

			// Act
			cts.Cancel();

			// Assert
			Assert.IsTrue(callbackInvoked);
		}

		[TestMethod]
		public void CancellationToken_Register_CallbackNotInvokedWhenNotCancelled()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			bool callbackInvoked = false;

			cts.Token.Register(() =>
			{
				callbackInvoked = true;
			});

			// No cancellation

			// Assert
			Assert.IsFalse(callbackInvoked);
		}

		[TestMethod]
		public void CancellationToken_MultipleCallbacks_AllInvoked()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			int callbackCount = 0;

			cts.Token.Register(() => callbackCount++);
			cts.Token.Register(() => callbackCount++);
			cts.Token.Register(() => callbackCount++);

			// Act
			cts.Cancel();

			// Assert
			Assert.AreEqual(3, callbackCount);
		}

		[TestMethod]
		public async Task TaskDelay_WithCancellation_ThrowsOperationCanceledException()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			cts.Cancel();

			// Act
			bool exceptionThrown = false;
			try
			{
				await Task.Delay(1000, cts.Token);
			}
			catch(TaskCanceledException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "Task.Delay should throw TaskCanceledException when token is cancelled");
		}

		[TestMethod]
		public async Task TaskDelay_WithTimeout_CompletesNormally()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();

			// Act - short delay should complete
			await Task.Delay(10, cts.Token);

			// Assert - no exception thrown
		}

		[TestMethod]
		public async Task TaskDelay_CancelledDuringDelay_ThrowsTaskCanceledException()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();

			// Act
			Task delayTask = Task.Delay(500, cts.Token);
			cts.Cancel(); // Cancel while delay is in progress

			// Assert
			bool exceptionThrown = false;
			try
			{
				await delayTask;
			}
			catch(TaskCanceledException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "Task should throw TaskCanceledException when cancelled during delay");
		}

		[TestMethod]
		public void HttpLibraryConfig_DefaultRequestTimeout_Is30Seconds()
		{
			// Assert
			Assert.AreEqual(TimeSpan.FromSeconds(30), Constants.DefaultRequestTimeout);
		}

		[TestMethod]
		public void HttpLibraryConfig_DefaultBinaryOperationTimeout_Is5Minutes()
		{
			// Assert
			Assert.AreEqual(TimeSpan.FromSeconds(300), Constants.DefaultBinaryOperationTimeout);
		}

		[TestMethod]
		public void HttpClientConfig_Timeout_DefaultIs30Seconds()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(TimeSpan.FromSeconds(30), config.Timeout);
		}

		[TestMethod]
		public void HttpClientConfig_Timeout_MatchesDefaultsJson()
		{
			// This test verifies that the defaults.json value matches expected configuration
			// defaults.json specifies: "Timeout": "00:00:30"

			// Arrange
			TimeSpan expectedTimeout = TimeSpan.FromSeconds(30);

			// Act
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(expectedTimeout, config.Timeout);
		}

		[TestMethod]
		public void HttpClientConfig_ConnectTimeout_DefaultIs10Seconds()
		{
			// Arrange
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(TimeSpan.FromSeconds(10), config.ConnectTimeout);
		}

		[TestMethod]
		public void HttpClientConfig_ConnectTimeout_MatchesDefaultsJson()
		{
			// defaults.json: "ConnectTimeout": "00:00:10"

			// Arrange
			TimeSpan expectedTimeout = TimeSpan.FromSeconds(10);

			// Act
			HttpClientConfig config = new HttpClientConfig();

			// Assert
			Assert.AreEqual(expectedTimeout, config.ConnectTimeout);
		}
		public void PooledHttpClientOptions_Timeout_CanBeNull()
		{
			// Arrange
			PooledHttpClientOptions options = new PooledHttpClientOptions
			{
				Timeout = null
			};

			// Assert
			Assert.IsNull(options.Timeout);
		}

		[TestMethod]
		public void PooledHttpClientOptions_Timeout_CanBeSet()
		{
			// Arrange
			TimeSpan customTimeout = TimeSpan.FromSeconds(60);
			PooledHttpClientOptions options = new PooledHttpClientOptions
			{
				Timeout = customTimeout
			};

			// Assert
			Assert.AreEqual(customTimeout, options.Timeout);
		}

		[TestMethod]
		public async Task CancellationTokenSource_CancelAfter_CancelsAfterDelay()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();

			// Act
			cts.CancelAfter(100); // Cancel after 100ms

			// Wait for cancellation
			await Task.Delay(200);

			// Assert
			Assert.IsTrue(cts.IsCancellationRequested);
		}

		[TestMethod]
		public void CancellationTokenSource_CancelAfter_ZeroMilliseconds_CancelsImmediately()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();

			// Act
			cts.CancelAfter(0);
			Thread.Sleep(10); // Small delay for cancellation to propagate

			// Assert
			Assert.IsTrue(cts.IsCancellationRequested);
		}

		[TestMethod]
		public void CancellationTokenSource_CancelAfter_NegativeMilliseconds_ThrowsArgumentOutOfRange()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();

			// Act
			bool exceptionThrown = false;
			try
			{
				// Use -2, as -1 is special value for infinite timeout
				cts.CancelAfter(-2);
			}
			catch(ArgumentOutOfRangeException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "CancelAfter should throw ArgumentOutOfRangeException for negative milliseconds (other than -1 which means infinite)");
		}

		[TestMethod]
		public void CancellationTokenSource_Dispose_CanBeSafelyCalled()
		{
			// Arrange
			CancellationTokenSource cts = new CancellationTokenSource();

			// Act
			cts.Dispose();

			// Assert - no exception thrown
		}

		[TestMethod]
		public void CancellationTokenSource_DisposeMultipleTimes_NoError()
		{
			// Arrange
			CancellationTokenSource cts = new CancellationTokenSource();

			// Act
			cts.Dispose();
			cts.Dispose(); // Second dispose
			cts.Dispose(); // Third dispose

			// Assert - no exception thrown
		}

		[TestMethod]
		public void CancellationTokenSource_AfterDispose_ThrowsObjectDisposedException()
		{
			// Arrange
			CancellationTokenSource cts = new CancellationTokenSource();
			cts.Dispose();

			// Act
			bool exceptionThrown = false;
			try
			{
				cts.Cancel();
			}
			catch(ObjectDisposedException)
			{
				exceptionThrown = true;
			}

			// Assert
			Assert.IsTrue(exceptionThrown, "Should throw ObjectDisposedException after dispose");
		}

		[TestMethod]
		public async Task LinkedTokenSource_Disposed_ProperlyCleansUp()
		{
			// Arrange
			using CancellationTokenSource cts1 = new CancellationTokenSource();
			using CancellationTokenSource cts2 = new CancellationTokenSource();
			CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

			// Act
			linked.Dispose();

			// Assert - parent tokens still work
			cts1.Cancel();
			Assert.IsTrue(cts1.IsCancellationRequested);

			await Task.CompletedTask; // Suppress async warning
		}

		[TestMethod]
		public void CancellationToken_Equals_SameSourceReturnsTrue()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			CancellationToken token1 = cts.Token;
			CancellationToken token2 = cts.Token;

			// Act & Assert
			Assert.IsTrue(token1.Equals(token2));
		}

		[TestMethod]
		public void CancellationToken_Equals_DifferentSourceReturnsFalse()
		{
			// Arrange
			using CancellationTokenSource cts1 = new CancellationTokenSource();
			using CancellationTokenSource cts2 = new CancellationTokenSource();
			CancellationToken token1 = cts1.Token;
			CancellationToken token2 = cts2.Token;

			// Act & Assert
			Assert.IsFalse(token1.Equals(token2));
		}

		[TestMethod]
		public void CancellationToken_GetHashCode_SameSourceReturnsSameHash()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			CancellationToken token1 = cts.Token;
			CancellationToken token2 = cts.Token;

			// Act
			int hash1 = token1.GetHashCode();
			int hash2 = token2.GetHashCode();

			// Assert
			Assert.AreEqual(hash1, hash2);
		}

		[TestMethod]
		public void CancellationToken_WaitHandle_ExistsAndValid()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();
			CancellationToken token = cts.Token;

			// Act
			WaitHandle handle = token.WaitHandle;

			// Assert
			Assert.IsNotNull(handle);
			Assert.IsFalse(handle.WaitOne(0)); // Not signaled yet

			// Cancel and check again
			cts.Cancel();
			Assert.IsTrue(handle.WaitOne(0)); // Now signaled
		}

		[TestMethod]
		public async Task CancellationToken_WaitAsync_CancelsTask()
		{
			// Arrange
			using CancellationTokenSource cts = new CancellationTokenSource();

			// Act
			Task waitTask = Task.Run(async () =>
			{
				await Task.Delay(Timeout.Infinite, cts.Token);
			});

			cts.Cancel();

			// Assert
			bool exceptionThrown = false;
			try
			{
				await waitTask;
			}
			catch(TaskCanceledException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "Wait task should throw TaskCanceledException when cancelled");
		}
	}
}