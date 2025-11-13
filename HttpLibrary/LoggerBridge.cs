using Microsoft.Extensions.Logging;

using System;
using System.Runtime.CompilerServices;

namespace HttpLibrary
{
	public static class LoggerBridge
	{
		private static ILoggerFactory? _factory;

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public static void SetFactory(ILoggerFactory factory)
		{
			_factory = factory ?? throw new ArgumentNullException(nameof(factory));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public static ILoggerFactory? GetFactory()
		{
			return _factory;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static ILogger CreateLogger()
		{
			return _factory?.CreateLogger("HttpLibrary") ?? new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory().CreateLogger("HttpLibrary");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public static void LogTrace(string messageTemplate, params object[] args)
		{
			CreateLogger().LogTrace(messageTemplate, args);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public static void LogDebug(string messageTemplate, params object[] args)
		{
			CreateLogger().LogDebug(messageTemplate, args);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public static void LogInformation(string messageTemplate, params object[] args)
		{
			CreateLogger().LogInformation(messageTemplate, args);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public static void LogWarning(string messageTemplate, params object[] args)
		{
			CreateLogger().LogWarning(messageTemplate, args);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public static void LogError(string messageTemplate, params object[] args)
		{
			CreateLogger().LogError(messageTemplate, args);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public static void LogError(Exception ex, string messageTemplate, params object[] args)
		{
			CreateLogger().LogError(ex, messageTemplate, args);
		}
	}
}