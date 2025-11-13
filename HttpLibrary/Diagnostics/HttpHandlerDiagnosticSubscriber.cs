using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace HttpLibrary.Diagnostics
{
	/// <summary>
	/// Subscribes to framework HTTP handler DiagnosticListener events and logs request/response headers (full values) at Debug level.
	/// Minimal and AOT-friendly: no reflection, only type checks against HttpRequestMessage/HttpResponseMessage.
	/// </summary>
	public static class HttpHandlerDiagnosticSubscriber
	{
		public static IDisposable? Register(ILoggerFactory loggerFactory)
		{
			if(loggerFactory == null)
			{
				return null;
			}

			ILogger logger = loggerFactory.CreateLogger("HttpHandlerDiagnosticSubscriber");

			DiagnosticListenerObserver observer = new DiagnosticListenerObserver(logger);
			IDisposable allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(observer);

			return new CompositeDisposable(allListenersSubscription, observer);
		}

		private sealed class DiagnosticListenerObserver : IObserver<DiagnosticListener>, IDisposable
		{
			private readonly ILogger _logger;
			private readonly ConcurrentBag<IDisposable> _subscriptions = new ConcurrentBag<IDisposable>();
			private bool _disposed;

			public DiagnosticListenerObserver(ILogger logger)
			{
				_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			}

			public void OnNext(DiagnosticListener value)
			{
				if(value == null)
					return;

				// Only subscribe to the HTTP handler diagnostic source(s)
				// Common name: "HttpHandlerDiagnosticListener"; be tolerant and match prefix
				try
				{
					string name = value.Name ?? string.Empty;
					if(name.IndexOf("HttpHandlerDiagnosticListener", StringComparison.OrdinalIgnoreCase) >= 0 ||
						name.IndexOf("System.Net.Http", StringComparison.OrdinalIgnoreCase) >= 0 ||
						name.IndexOf("HttpHandler", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						IDisposable sub = value.Subscribe(new EventObserver(_logger));
						_subscriptions.Add(sub);
					}
				}
				catch
				{
					// best-effort
				}
			}

			public void OnError(Exception error) { }
			public void OnCompleted() { }

			public void Dispose()
			{
				if(_disposed)
					return;
				_disposed = true;
				while(_subscriptions.TryTake(out IDisposable? d))
				{
					try
					{ d.Dispose(); }
					catch { }
				}
			}
		}

		private sealed class EventObserver : IObserver<KeyValuePair<string, object?>>
		{
			private readonly ILogger _logger;

			public EventObserver(ILogger logger)
			{
				_logger = logger;
			}

			public void OnNext(KeyValuePair<string, object?> evt)
			{
				try
				{
					object? payload = evt.Value;

					// If payload is HttpRequestMessage
					if(payload is HttpRequestMessage req)
					{
						if(_logger.IsEnabled(LogLevel.Debug))
						{
							StringBuilder sb = new StringBuilder();
							sb.AppendLine($"[diagnostic] Request headers for {req.Method} {req.RequestUri}:");
							foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> h in req.Headers)
							{
								sb.AppendLine($" {h.Key}: {string.Join(", ", h.Value)}");
							}
							if(req.Content != null)
							{
								foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> h in req.Content.Headers)
								{
									sb.AppendLine($" {h.Key}: {string.Join(", ", h.Value)}");
								}
							}
							_logger.LogDebug(sb.ToString());
						}
						return;
					}

					// If payload is HttpResponseMessage
					if(payload is HttpResponseMessage resp)
					{
						if(_logger.IsEnabled(LogLevel.Debug))
						{
							StringBuilder sb = new StringBuilder();
							sb.AppendLine($"[diagnostic] Response headers for {( resp.RequestMessage?.Method?.ToString() ?? "?" )} {resp.RequestMessage?.RequestUri}");
							foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> h in resp.Headers)
							{
								sb.AppendLine($" {h.Key}: {string.Join(", ", h.Value)}");
							}
							if(resp.Content != null)
							{
								foreach(System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> h in resp.Content.Headers)
								{
									sb.AppendLine($" {h.Key}: {string.Join(", ", h.Value)}");
								}
							}
							_logger.LogDebug(sb.ToString());
						}
						return;
					}

					// Other payload shapes ignored - avoid reflection for AOT compatibility
				}
				catch
				{
					// best-effort
				}
			}

			public void OnError(Exception error) { }
			public void OnCompleted() { }
		}

		private sealed class CompositeDisposable : IDisposable
		{
			private readonly IDisposable _first;
			private readonly IDisposable? _second;
			private bool _disposed;

			public CompositeDisposable(IDisposable first, IDisposable? second)
			{
				_first = first ?? throw new ArgumentNullException(nameof(first));
				_second = second;
			}

			public void Dispose()
			{
				if(_disposed)
					return;
				_disposed = true;
				try
				{ _first.Dispose(); }
				catch { }
				if(_second != null)
				{
					try
					{ _second.Dispose(); }
					catch { }
				}
			}
		}
	}
}