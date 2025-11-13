using System.Threading;

namespace HttpLibrary
{
	/// <summary>
	/// Lightweight metrics snapshot for the pooled typed client.
	/// </summary>
	public sealed class PooledHttpClientMetrics
	{
		long totalRequests;
		long successfulRequests;
		long failedRequests;
		long activeRequests;
		long totalBytesReceived;
		long totalRequestMilliseconds;

		public void OnRequestStarted() => Interlocked.Increment(ref activeRequests);
		public void OnRequestCompleted(bool success, long bytesReceived, long elapsedMs)
		{
			Interlocked.Decrement(ref activeRequests);
			Interlocked.Increment(ref totalRequests);
			if(success)
			{
				Interlocked.Increment(ref successfulRequests);
			}
			else
			{
				Interlocked.Increment(ref failedRequests);
			}
			if(bytesReceived > 0)
			{
				Interlocked.Add(ref totalBytesReceived, bytesReceived);
			}
			if(elapsedMs > 0)
			{
				Interlocked.Add(ref totalRequestMilliseconds, elapsedMs);
			}
		}

		public long TotalRequests => Interlocked.Read(ref totalRequests);
		public long SuccessfulRequests => Interlocked.Read(ref successfulRequests);
		public long FailedRequests => Interlocked.Read(ref failedRequests);
		public long ActiveRequests => Interlocked.Read(ref activeRequests);
		public long TotalBytesReceived => Interlocked.Read(ref totalBytesReceived);

		/// <summary>
		/// Average request duration in milliseconds (0 if no requests).
		/// </summary>
		public double AverageRequestMs
		{
			get
			{
				long total = Interlocked.Read(ref totalRequests);
				long ms = Interlocked.Read(ref totalRequestMilliseconds);
				return total == 0 ? 0 : (double)ms / total;
			}
		}
	}
}