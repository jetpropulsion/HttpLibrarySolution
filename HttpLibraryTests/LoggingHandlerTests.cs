using HttpLibrary;

using HttpLibraryTests.TestUtilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class LoggingHandlerTests
	{
		[TestMethod]
		public async Task LoggingHandler_LogsRequestAndResponseHeaders()
		{
			ListLoggerProvider provider = new ListLoggerProvider();
			ILoggerFactory testLoggerFactory = new LoggerFactory(new[] { provider });
			LoggerBridge.SetFactory(testLoggerFactory);

			HttpLibrary.Testing.TestHooks.SetPrimaryHandlerFactory(() => new InMemoryResponseHandler());

			ConcurrentDictionary<string, IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			ServiceProvider sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);
			IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
			HttpClient client = factory.CreateClient("default");

			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://example.com/test");
			req.Headers.Add("X-Test-Req", "reqval");

			HttpResponseMessage resp = await client.SendAsync(req);

			string joined = string.Join("\n", provider.Entries);
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(joined.Contains("X-Test-Req: reqval"));
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(joined.Contains("X-Test-Resp: respval"));

			// Reset test hook and logger bridge
			HttpLibrary.Testing.TestHooks.SetPrimaryHandlerFactory(null);
			LoggerBridge.SetFactory(new NullLoggerFactory());
		}

		[TestMethod]
		public async Task LoggingHandler_DoesNotLogWhenDebugDisabled()
		{
			ThresholdLoggerProvider provider = new ThresholdLoggerProvider(Microsoft.Extensions.Logging.LogLevel.Information);
			ILoggerFactory testLoggerFactory = new LoggerFactory(new[] { provider });
			LoggerBridge.SetFactory(testLoggerFactory);

			HttpLibrary.Testing.TestHooks.SetPrimaryHandlerFactory(() => new InMemoryResponseHandler());

			ConcurrentDictionary<string, IPooledHttpClient> registered;
			ConcurrentDictionary<string, Uri> baseAddrs;
			ServiceProvider sp = HttpLibrary.ServiceConfiguration.InitializeServices(out registered, out baseAddrs);
			IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
			HttpClient client = factory.CreateClient("default");

			HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://example.com/test");
			req.Headers.Add("X-Test-Req", "reqval");

			HttpResponseMessage resp = await client.SendAsync(req);

			string joined = string.Join("\n", provider.Entries);
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsFalse(joined.Contains("X-Test-Req: reqval"));
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsFalse(joined.Contains("X-Test-Resp: respval"));

			// Reset test hook and logger bridge
			HttpLibrary.Testing.TestHooks.SetPrimaryHandlerFactory(null);
			LoggerBridge.SetFactory(new NullLoggerFactory());
		}
	}

	// Simple in-memory logger provider to capture log entries
	internal sealed class ListLoggerProvider : ILoggerProvider
	{
		public readonly System.Collections.Generic.List<string> Entries = new System.Collections.Generic.List<string>();

		public ILogger CreateLogger(string categoryName)
		{
			return new ListLogger(this, categoryName);
		}

		public void Dispose() { }
	}

	internal sealed class ListLogger : ILogger
	{
		private readonly ListLoggerProvider _provider;
		private readonly string _category;

		public ListLogger(ListLoggerProvider provider, string category)
		{
			_provider = provider;
			_category = category;
		}

		// Explicit interface implementation to avoid nullable mismatch warnings
		System.IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
		{
			try
			{
				string msg = formatter(state, exception);
				_provider.Entries.Add(msg);
			}
			catch { }
		}
	}

	internal sealed class NullScope : System.IDisposable
	{
		public static readonly NullScope Instance = new NullScope();
		public void Dispose() { }
	}

	// Logger provider that filters out Debug logs by threshold
	internal sealed class ThresholdLoggerProvider : ILoggerProvider
	{
		private readonly LogLevel _minLevel;
		public readonly System.Collections.Generic.List<string> Entries = new System.Collections.Generic.List<string>();

		public ThresholdLoggerProvider(LogLevel minLevel)
		{
			_minLevel = minLevel;
		}

		public ILogger CreateLogger(string categoryName)
		{
			return new ThresholdLogger(this, categoryName, _minLevel);
		}

		public void Dispose() { }

		private sealed class ThresholdLogger : ILogger
		{
			private readonly ThresholdLoggerProvider _provider;
			private readonly LogLevel _minLevel;

			public ThresholdLogger(ThresholdLoggerProvider provider, string category, LogLevel minLevel)
			{
				_provider = provider;
				_minLevel = minLevel;
			}

			// Explicit interface implementation to avoid nullable mismatch warnings
			System.IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

			public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
			{
				if(!IsEnabled(logLevel))
					return;
				try
				{
					string msg = formatter(state, exception);
					_provider.Entries.Add(msg);
				}
				catch { }
			}
		}
	}
}