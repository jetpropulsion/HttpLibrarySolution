using Microsoft.Extensions.Logging;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	/// <summary>
	/// Manages timeout, cancellation, and error handling for HTTP request execution.
	/// Provides consistent timeout and cancellation behavior across all request types.
	/// </summary>
	public sealed class RequestExecutionContext : IDisposable
	{
		readonly CancellationTokenSource timeoutCts;
		readonly CancellationTokenSource linkedCts;
		readonly CancellationToken parentToken;
		readonly ILogger logger;
		bool disposed = false;

		/// <summary>
		/// Gets the combined cancellation token that respects both timeout and parent cancellation.
		/// </summary>
		public CancellationToken Token => linkedCts.Token;

		/// <summary>
		/// Creates a new request execution context with timeout and cancellation management.
		/// </summary>
		/// <param name="timeout">Optional timeout for the operation (uses default if null)</param>
		/// <param name="cancellationToken">Parent cancellation token</param>
		/// <param name="logger">Logger for diagnostic information</param>
		/// <param name="defaultTimeout">Default timeout to use if timeout parameter is null</param>
		public RequestExecutionContext(
			TimeSpan? timeout,
			CancellationToken cancellationToken,
			ILogger logger,
			TimeSpan defaultTimeout)
		{
			TimeSpan actualTimeout = timeout ?? defaultTimeout;
			this.timeoutCts = new CancellationTokenSource(actualTimeout);
			this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
			this.parentToken = cancellationToken;
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Executes an async operation with standardized error handling and logging.
		/// </summary>
		/// <typeparam name="T">Return type of the operation</typeparam>
		/// <param name="operation">The async operation to execute</param>
		/// <param name="operationName">Name of the operation for logging</param>
		/// <returns>Result of the operation</returns>
		public async Task<T> ExecuteAsync<T>(
			Func<CancellationToken, Task<T>> operation,
			string operationName)
		{
			if(operation == null)
			{
				throw new ArgumentNullException(nameof(operation));
			}

			try
			{
				return await operation(linkedCts.Token).ConfigureAwait(false);
			}
			catch(HttpRequestException hre)
			{
				LogHttpException(hre, operationName);
				throw;
			}
			catch(OperationCanceledException oce) when(linkedCts.IsCancellationRequested)
			{
				LogCancellation(oce, operationName);
				throw;
			}
			catch(Exception ex)
			{
				logger.LogError(ex, "{Operation} failed", operationName);
				throw;
			}
		}

		/// <summary>
		/// Executes an async operation without return value with standardized error handling.
		/// </summary>
		/// <param name="operation">The async operation to execute</param>
		/// <param name="operationName">Name of the operation for logging</param>
		public async Task ExecuteAsync(
			Func<CancellationToken, Task> operation,
			string operationName)
		{
			if(operation == null)
			{
				throw new ArgumentNullException(nameof(operation));
			}

			try
			{
				await operation(linkedCts.Token).ConfigureAwait(false);
			}
			catch(HttpRequestException hre)
			{
				LogHttpException(hre, operationName);
				throw;
			}
			catch(OperationCanceledException oce) when(linkedCts.IsCancellationRequested)
			{
				LogCancellation(oce, operationName);
				throw;
			}
			catch(Exception ex)
			{
				logger.LogError(ex, "{Operation} failed", operationName);
				throw;
			}
		}

		void LogHttpException(HttpRequestException hre, string operationName)
		{
			if(hre.StatusCode.HasValue)
			{
				logger.LogError("{Operation}: HTTP error {StatusCode} ({StatusText}) - {Message}",
					operationName, (int)hre.StatusCode.Value, hre.StatusCode.Value, hre.Message);
			}
			else
			{
				logger.LogError("{Operation}: Request error - {Message}", operationName, hre.Message);
			}
		}

		void LogCancellation(OperationCanceledException oce, string operationName)
		{
			if(parentToken.IsCancellationRequested)
			{
				logger.LogError(oce, "{Operation} canceled by caller", operationName);
			}
			else if(timeoutCts.IsCancellationRequested)
			{
				logger.LogError(oce, "{Operation} timed out", operationName);
			}
			else
			{
				logger.LogError(oce, "{Operation} canceled", operationName);
			}
		}

		public void Dispose()
		{
			if(disposed)
			{
				return;
			}

			linkedCts?.Dispose();
			timeoutCts?.Dispose();
			disposed = true;
		}
	}
}