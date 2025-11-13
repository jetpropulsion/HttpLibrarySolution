using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

using System;
using System.IO;

namespace HttpLibraryCLI.Sinks
{
	/// <summary>
	/// Custom Serilog sink that writes to stderr instead of stdout.
	/// This allows program output to go to stdout while logs go to stderr.
	/// Emits colorized output using ANSI escape sequences when supported.
	/// </summary>
	public sealed class StderrConsoleSink : ILogEventSink
	{
		private readonly ITextFormatter _formatter;
		private readonly object _syncRoot = new object();

		/// <summary>
		/// Creates a new stderr console sink with the specified output template.
		/// </summary>
		/// <param name="outputTemplate">The template for formatting log events</param>
		public StderrConsoleSink(string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
		{
			// Include SourceContext by default so log category is visible; helps identify where masked logs originate.
			_formatter = new MessageTemplateTextFormatter(outputTemplate, null);
		}

		private static string GetAnsiColorForLevel(LogEventLevel level)
		{
			switch(level)
			{
				case LogEventLevel.Verbose:
				case LogEventLevel.Debug:
				return "\u001b[90m"; // bright black / gray
				case LogEventLevel.Information:
				return "\u001b[32m"; // green
				case LogEventLevel.Warning:
				return "\u001b[33m"; // yellow
				case LogEventLevel.Error:
				case LogEventLevel.Fatal:
				return "\u001b[31m"; // red
				default:
				return "\u001b[0m"; // reset
			}
		}

		/// <summary>
		/// Emits a log event to stderr.
		/// </summary>
		/// <param name="logEvent">The log event to emit</param>
		public void Emit(LogEvent logEvent)
		{
			if(logEvent is null)
			{
				return;
			}

			lock(_syncRoot)
			{
				// Format into a string first so we can wrap it with ANSI codes if needed
				using System.IO.StringWriter sw = new System.IO.StringWriter(System.Globalization.CultureInfo.InvariantCulture);
				_formatter.Format(logEvent, sw);
				string formatted = sw.ToString();

				string color = GetAnsiColorForLevel(logEvent.Level);
				string reset = "\u001b[0m";

				// Write colorized output. Some terminals may ignore ANSI sequences; that's acceptable.
				Console.Error.Write(color);
				Console.Error.Write(formatted);
				Console.Error.Write(reset);
				Console.Error.Flush();
			}
		}
	}
}