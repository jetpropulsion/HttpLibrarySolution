using Microsoft.Extensions.Logging;

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary.Handlers
{
	/// <summary>
	/// DelegatingHandler that logs full request and response headers at Debug level.
	/// Minimal/AOT-friendly: no reflection, no dynamic code.
	/// </summary>
	public sealed class LoggingHandler : DelegatingHandler
	{
		private readonly ILogger<LoggingHandler> _logger;

		public LoggingHandler(ILogger<LoggingHandler> logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);

			try
			{
				if(_logger.IsEnabled(LogLevel.Debug))
				{
					StringBuilder sb = new StringBuilder();
					sb.AppendLine($"[diagnostic] Request headers for {request.Method} {request.RequestUri}:");
					foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> h in request.Headers)
					{
						sb.AppendLine($" {h.Key}: {string.Join(", ", h.Value)}");
					}

					if(request.Content != null)
					{
						foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> h in request.Content.Headers)
						{
							sb.AppendLine($" {h.Key}: {string.Join(", ", h.Value)}");
						}
					}

					_logger.LogDebug(sb.ToString());
				}
			}
			catch
			{
				// best-effort
			}

			HttpResponseMessage? responseNullable = await base.SendAsync(request!, cancellationToken).ConfigureAwait(false);
			if(responseNullable == null)
			{
				throw new InvalidOperationException("DelegatingHandler returned null response");
			}

			HttpResponseMessage response = responseNullable;

			try
			{
				if(_logger.IsEnabled(LogLevel.Debug))
				{
					StringBuilder sb = new StringBuilder();
					sb.AppendLine($"[diagnostic] Response headers for {( response.RequestMessage?.Method?.ToString() ?? "?" )} {response.RequestMessage?.RequestUri} - Status {(int)response.StatusCode}:");
					foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> h in response.Headers)
					{
						sb.AppendLine($" {h.Key}: {string.Join(", ", h.Value)}");
					}

					if(response.Content != null)
					{
						foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> h in response.Content.Headers)
						{
							sb.AppendLine($" {h.Key}: {string.Join(", ", h.Value)}");
						}
					}

					_logger.LogDebug(sb.ToString());
				}
			}
			catch
			{
				// best-effort
			}

			return response;
		}
	}
}