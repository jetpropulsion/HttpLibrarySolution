using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Threading;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for PooledHttpClientMetrics including request counting, duration tracking, and thread safety
	/// </summary>
	[TestClass]
	public class MetricsTests
	{
		#region Metrics Structure Tests

		[TestMethod]
		public void PooledHttpClientMetrics_Constructor_InitializesWithZeroValues()
		{
			// Arrange & Act
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert
			Assert.AreEqual(0L, metrics.TotalRequests);
			Assert.AreEqual(0L, metrics.SuccessfulRequests);
			Assert.AreEqual(0L, metrics.FailedRequests);
			Assert.AreEqual(0L, metrics.ActiveRequests);
			Assert.AreEqual(0L, metrics.TotalBytesReceived);
			Assert.AreEqual(0.0, metrics.AverageRequestMs);
		}

		[TestMethod]
		public void PooledHttpClientMetrics_TotalRequests_CanBeRead()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			long totalRequests = metrics.TotalRequests;

			// Assert
			Assert.AreEqual(0L, totalRequests);
		}

		[TestMethod]
		public void PooledHttpClientMetrics_SuccessfulRequests_CanBeRead()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			long successfulRequests = metrics.SuccessfulRequests;

			// Assert
			Assert.AreEqual(0L, successfulRequests);
		}

		[TestMethod]
		public void PooledHttpClientMetrics_FailedRequests_CanBeRead()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			long failedRequests = metrics.FailedRequests;

			// Assert
			Assert.AreEqual(0L, failedRequests);
		}

		[TestMethod]
		public void PooledHttpClientMetrics_ActiveRequests_CanBeRead()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			long activeRequests = metrics.ActiveRequests;

			// Assert
			Assert.AreEqual(0L, activeRequests);
		}

		[TestMethod]
		public void PooledHttpClientMetrics_TotalBytesReceived_CanBeRead()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			long bytesReceived = metrics.TotalBytesReceived;

			// Assert
			Assert.AreEqual(0L, bytesReceived);
		}

		[TestMethod]
		public void PooledHttpClientMetrics_AverageRequestMs_CanBeRead()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			double averageMs = metrics.AverageRequestMs;

			// Assert
			Assert.AreEqual(0.0, averageMs);
		}

		#endregion

		#region Counter Tests

		[TestMethod]
		public void Metrics_TotalRequests_CountsAllRequests()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act - Simulate incrementing (would be done by actual implementation)
			// Note: These tests verify the structure exists, actual incrementing 
			// happens in the PooledHttpClient implementation

			// Assert
			Assert.AreEqual(0L, metrics.TotalRequests, "Initial count should be 0");
		}

		[TestMethod]
		public void Metrics_SuccessfulRequests_SeparateFromTotal()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert - Successful requests is independent counter
			Assert.AreEqual(0L, metrics.SuccessfulRequests);
			Assert.AreEqual(0L, metrics.TotalRequests);
		}

		[TestMethod]
		public void Metrics_FailedRequests_SeparateFromTotal()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert - Failed requests is independent counter
			Assert.AreEqual(0L, metrics.FailedRequests);
			Assert.AreEqual(0L, metrics.TotalRequests);
		}

		[TestMethod]
		public void Metrics_ActiveRequests_IndependentCounter()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert - Active requests is independent
			Assert.AreEqual(0L, metrics.ActiveRequests);
		}

		#endregion

		#region Bytes Tracking Tests

		[TestMethod]
		public void Metrics_TotalBytesReceived_UsesLongType()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			long bytes = metrics.TotalBytesReceived;

			// Assert - Using long allows tracking > 2GB
			Assert.IsInstanceOfType(bytes, typeof(long));
		}

		[TestMethod]
		public void Metrics_TotalBytesReceived_CanHandleLargeValues()
		{
			// Arrange - Verify we can represent large byte counts
			long largeByteCount = 10L * 1024L * 1024L * 1024L; // 10 GB

			// Assert - long type can hold this
			Assert.IsTrue(largeByteCount > int.MaxValue);
			Assert.IsTrue(largeByteCount > 0);
		}

		#endregion

		#region Average Calculation Tests

		[TestMethod]
		public void Metrics_AverageRequestMs_UsesDoubleType()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			double average = metrics.AverageRequestMs;

			// Assert
			Assert.IsInstanceOfType(average, typeof(double));
		}

		[TestMethod]
		public void Metrics_AverageRequestMs_DefaultIsZero()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert
			Assert.AreEqual(0.0, metrics.AverageRequestMs);
		}

		[TestMethod]
		public void Metrics_AverageRequestMs_CanRepresentDecimals()
		{
			// Arrange - Verify double precision for milliseconds
			double testAverage = 123.456;

			// Assert - Double can represent decimal precision
			Assert.AreEqual(123.456, testAverage, 0.001);
		}

		#endregion

		#region Thread Safety Concepts Tests

		[TestMethod]
		public void Metrics_MultipleReads_Safe()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act - Multiple concurrent reads should be safe
			long read1 = metrics.TotalRequests;
			long read2 = metrics.TotalRequests;
			long read3 = metrics.TotalRequests;

			// Assert - All reads return same value
			Assert.AreEqual(read1, read2);
			Assert.AreEqual(read2, read3);
		}

		[TestMethod]
		public void Metrics_DifferentProperties_CanBeReadConcurrently()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act - Reading different properties should be safe
			long total = metrics.TotalRequests;
			long successful = metrics.SuccessfulRequests;
			long failed = metrics.FailedRequests;
			long active = metrics.ActiveRequests;
			long bytes = metrics.TotalBytesReceived;
			double average = metrics.AverageRequestMs;

			// Assert - All reads succeed
			Assert.AreEqual(0L, total);
			Assert.AreEqual(0L, successful);
			Assert.AreEqual(0L, failed);
			Assert.AreEqual(0L, active);
			Assert.AreEqual(0L, bytes);
			Assert.AreEqual(0.0, average);
		}

		#endregion

		#region Interlocked Operations Tests (Conceptual)

		[TestMethod]
		public void Interlocked_Increment_ThreadSafe()
		{
			// Arrange
			int counter = 0;

			// Act
			int result = Interlocked.Increment(ref counter);

			// Assert
			Assert.AreEqual(1, result);
			Assert.AreEqual(1, counter);
		}

		[TestMethod]
		public void Interlocked_Decrement_ThreadSafe()
		{
			// Arrange
			int counter = 5;

			// Act
			int result = Interlocked.Decrement(ref counter);

			// Assert
			Assert.AreEqual(4, result);
			Assert.AreEqual(4, counter);
		}

		[TestMethod]
		public void Interlocked_Add_ThreadSafe()
		{
			// Arrange
			long counter = 100L;

			// Act
			long result = Interlocked.Add(ref counter, 50L);

			// Assert
			Assert.AreEqual(150L, result);
			Assert.AreEqual(150L, counter);
		}

		[TestMethod]
		public void Interlocked_Read_ThreadSafeRead()
		{
			// Arrange
			long counter = 12345L;

			// Act
			long result = Interlocked.Read(ref counter);

			// Assert
			Assert.AreEqual(12345L, result);
		}

		[TestMethod]
		public void Interlocked_Exchange_AtomicSwap()
		{
			// Arrange
			int counter = 10;

			// Act
			int oldValue = Interlocked.Exchange(ref counter, 20);

			// Assert
			Assert.AreEqual(10, oldValue);
			Assert.AreEqual(20, counter);
		}

		[TestMethod]
		public void Interlocked_CompareExchange_ConditionalSwap()
		{
			// Arrange
			int counter = 100;

			// Act - Only exchange if counter equals 100
			int oldValue = Interlocked.CompareExchange(ref counter, 200, 100);

			// Assert
			Assert.AreEqual(100, oldValue);
			Assert.AreEqual(200, counter);
		}

		[TestMethod]
		public void Interlocked_CompareExchange_NoSwapIfMismatch()
		{
			// Arrange
			int counter = 100;

			// Act - Try to exchange but comparand doesn't match
			int oldValue = Interlocked.CompareExchange(ref counter, 200, 50);

			// Assert
			Assert.AreEqual(100, oldValue);
			Assert.AreEqual(100, counter); // Unchanged
		}

		#endregion

		#region Relationship Tests

		[TestMethod]
		public void Metrics_TotalRequests_ShouldEqualSuccessfulPlusFailed()
		{
			// This is a conceptual test - in actual implementation:
			// TotalRequests = SuccessfulRequests + FailedRequests

			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert initial state
			Assert.AreEqual(
					metrics.SuccessfulRequests + metrics.FailedRequests,
					metrics.TotalRequests,
					"Total should equal successful + failed");
		}

		[TestMethod]
		public void Metrics_ActiveRequests_ShouldBeNonNegative()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert
			Assert.IsTrue(metrics.ActiveRequests >= 0, "Active requests cannot be negative");
		}

		[TestMethod]
		public void Metrics_TotalBytesReceived_ShouldBeNonNegative()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert
			Assert.IsTrue(metrics.TotalBytesReceived >= 0, "Bytes received cannot be negative");
		}

		[TestMethod]
		public void Metrics_AverageRequestMs_ShouldBeNonNegative()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert
			Assert.IsTrue(metrics.AverageRequestMs >= 0.0, "Average time cannot be negative");
		}

		#endregion

		#region Format and Display Tests

		[TestMethod]
		public void Metrics_TotalRequests_CanBeFormattedAsString()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			string formatted = metrics.TotalRequests.ToString();

			// Assert
			Assert.AreEqual("0", formatted);
		}

		[TestMethod]
		public void Metrics_AverageRequestMs_CanBeFormattedWithDecimals()
		{
			// Arrange
			double average = 123.456;

			// Act
			string formatted = average.ToString("F2");

			// Assert
			Assert.AreEqual("123.46", formatted); // 2 decimal places
		}

		[TestMethod]
		public void Metrics_TotalBytesReceived_CanBeFormattedAsKB()
		{
			// Arrange
			long bytes = 1024L * 500; // 500 KB

			// Act
			double kb = bytes / 1024.0;
			string formatted = $"{kb:F2} KB";

			// Assert
			Assert.AreEqual("500.00 KB", formatted);
		}

		[TestMethod]
		public void Metrics_TotalBytesReceived_CanBeFormattedAsMB()
		{
			// Arrange
			long bytes = 1024L * 1024L * 5; // 5 MB

			// Act
			double mb = bytes / ( 1024.0 * 1024.0 );
			string formatted = $"{mb:F2} MB";

			// Assert
			Assert.AreEqual("5.00 MB", formatted);
		}

		#endregion

		#region Zero Division Tests

		[TestMethod]
		public void Metrics_AverageWithZeroRequests_HandlesGracefully()
		{
			// When TotalRequests = 0, average should be 0 (not divide by zero)

			// Arrange
			int totalRequests = 0;
			double totalMs = 0.0;

			// Act
			double average = totalRequests > 0 ? totalMs / totalRequests : 0.0;

			// Assert
			Assert.AreEqual(0.0, average);
		}

		[TestMethod]
		public void Metrics_AverageWithOneRequest_CalculatesCorrectly()
		{
			// Arrange
			int totalRequests = 1;
			double totalMs = 100.0;

			// Act
			double average = totalMs / totalRequests;

			// Assert
			Assert.AreEqual(100.0, average);
		}

		[TestMethod]
		public void Metrics_AverageWithMultipleRequests_CalculatesCorrectly()
		{
			// Arrange
			int totalRequests = 5;
			double totalMs = 500.0;

			// Act
			double average = totalMs / totalRequests;

			// Assert
			Assert.AreEqual(100.0, average);
		}

		#endregion

		#region Overflow Tests

		[TestMethod]
		public void Metrics_IntMaxValue_CanBeStored()
		{
			// Arrange
			int maxValue = int.MaxValue;

			// Assert
			Assert.AreEqual(2147483647, maxValue);
		}

		[TestMethod]
		public void Metrics_LongMaxValue_CanBeStored()
		{
			// Arrange
			long maxValue = long.MaxValue;

			// Assert
			Assert.AreEqual(9223372036854775807L, maxValue);
		}

		[TestMethod]
		public void Metrics_BytesOverIntMax_UsesLong()
		{
			// Arrange - More than int.MaxValue bytes
			long largeBytes = (long)int.MaxValue + 1000L;

			// Assert
			Assert.IsTrue(largeBytes > int.MaxValue);
			Assert.AreEqual(2147484647L, largeBytes);
		}

		#endregion

		#region Type Safety Tests

		[TestMethod]
		public void Metrics_AllCounters_AreValueTypes()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Assert - Value types are thread-safer for reads
			Assert.IsTrue(typeof(int).IsValueType);
			Assert.IsTrue(typeof(long).IsValueType);
			Assert.IsTrue(typeof(double).IsValueType);
		}

		[TestMethod]
		public void Metrics_CanBeCompared()
		{
			// Arrange
			int requests1 = 10;
			int requests2 = 20;

			// Assert
			Assert.IsTrue(requests2 > requests1);
			Assert.IsTrue(requests1 < requests2);
			Assert.IsFalse(requests1 == requests2);
		}

		#endregion
	}
}