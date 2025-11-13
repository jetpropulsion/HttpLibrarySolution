using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary
{
	/// <summary>
	/// Registry for socket callback handlers per client.
	/// Allows users to configure callbacks at runtime without modifying library code.
	/// </summary>
	public static class CallbackRegistry
	{
		private static readonly ConcurrentDictionary<string, SocketCallbackHandlers> handlers = new ConcurrentDictionary<string, SocketCallbackHandlers>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Merge incoming handlers into existing handlers. For each callback property, if the incoming value is not null it overrides the existing one; otherwise the existing value is preserved.
		/// Special-case PlaintextStreamFilter: compose multiple filters so they are invoked in sequence.
		/// </summary>
		private static SocketCallbackHandlers MergeHandlers(SocketCallbackHandlers existing, SocketCallbackHandlers incoming)
		{
			if(existing == null)
				return incoming ?? new SocketCallbackHandlers();
			if(incoming == null)
				return existing;

			// Compose ConnectCallback when both exist: call first, then second with same context and cancellation token.
			if(existing.ConnectCallback != null && incoming.ConnectCallback != null)
			{
				Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> firstConnect = existing.ConnectCallback;
				Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> secondConnect = incoming.ConnectCallback;

				existing.ConnectCallback = async (context, ct) =>
				{
					Stream firstResult = await firstConnect(context, ct).ConfigureAwait(false);
					Stream secondResult = await secondConnect(context, ct).ConfigureAwait(false);
					return secondResult ?? firstResult;
				};
			}
			else if(incoming.ConnectCallback != null)
			{
				existing.ConnectCallback = incoming.ConnectCallback;
			}

			// Compose plaintext stream filters when both exist: call first, then second with same context.
			if(existing.PlaintextStreamFilter != null && incoming.PlaintextStreamFilter != null)
			{
				Func<SocketsHttpPlaintextStreamFilterContext, CancellationToken, ValueTask<Stream>> first = existing.PlaintextStreamFilter;
				Func<SocketsHttpPlaintextStreamFilterContext, CancellationToken, ValueTask<Stream>> second = incoming.PlaintextStreamFilter;

				existing.PlaintextStreamFilter = async (context, ct) =>
				{
					Stream firstResult = await first(context, ct).ConfigureAwait(false);
					Stream secondResult = await second(context, ct).ConfigureAwait(false);
					return secondResult ?? firstResult;
				};

				// Mark composed for diagnostics and testing
				existing.PlaintextFilterIsComposed = true;
			}
			else if(incoming.PlaintextStreamFilter != null)
			{
				existing.PlaintextStreamFilter = incoming.PlaintextStreamFilter;
			}

			// Prefer incoming server certificate validation callback when provided (override)
			if(incoming.ServerCertificateCustomValidationCallback != null)
			{
				existing.ServerCertificateCustomValidationCallback = incoming.ServerCertificateCustomValidationCallback;
			}

			// Compose local certificate selection callbacks: call existing then incoming, prefer incoming's non-null result
			if(existing.LocalCertificateSelectionCallback != null && incoming.LocalCertificateSelectionCallback != null)
			{
				Func<HttpRequestMessage, System.Security.Cryptography.X509Certificates.X509Certificate2Collection?, string[], System.Security.Cryptography.X509Certificates.X509Certificate2?> firstSelector = existing.LocalCertificateSelectionCallback;
				Func<HttpRequestMessage, System.Security.Cryptography.X509Certificates.X509Certificate2Collection?, string[], System.Security.Cryptography.X509Certificates.X509Certificate2?> secondSelector = incoming.LocalCertificateSelectionCallback;

				existing.LocalCertificateSelectionCallback = (request, certificates, hostnames) =>
				{
					System.Security.Cryptography.X509Certificates.X509Certificate2? firstResult = firstSelector(request, certificates, hostnames);
					System.Security.Cryptography.X509Certificates.X509Certificate2? secondResult = secondSelector(request, certificates, hostnames);
					return secondResult ?? firstResult;
				};
			}
			else if(incoming.LocalCertificateSelectionCallback != null)
			{
				existing.LocalCertificateSelectionCallback = incoming.LocalCertificateSelectionCallback;
			}

			return existing;
		}

		/// <summary>
		/// Registers callback handlers for a specific client.
		/// Merges with any existing handlers so multiple registrations can augment behavior.
		/// </summary>
		/// <param name="clientName">Name of the client (must match a configured client)</param>
		/// <param name="callbackHandlers">Callback handlers to register</param>
		public static void RegisterHandlers(string clientName, SocketCallbackHandlers callbackHandlers)
		{
			if(string.IsNullOrWhiteSpace(clientName))
			{
				throw new ArgumentException("Client name cannot be null or whitespace", nameof(clientName));
			}

			if(callbackHandlers == null)
			{
				throw new ArgumentNullException(nameof(callbackHandlers));
			}

			handlers.AddOrUpdate(clientName, callbackHandlers, (key, existing) => MergeHandlers(existing, callbackHandlers));
		}

		/// <summary>
		/// Registers callback handlers for a specific client using a configuration action.
		/// Merges with any existing handlers so multiple registrations can augment behavior.
		/// </summary>
		/// <param name="clientName">Name of the client (must match a configured client)</param>
		/// <param name="configure">Action to configure the callback handlers</param>
		public static void RegisterHandlers(string clientName, Action<SocketCallbackHandlers> configure)
		{
			if(string.IsNullOrWhiteSpace(clientName))
			{
				throw new ArgumentException("Client name cannot be null or whitespace", nameof(clientName));
			}

			if(configure == null)
			{
				throw new ArgumentNullException(nameof(configure));
			}

			SocketCallbackHandlers newHandlers = new SocketCallbackHandlers();
			configure(newHandlers);

			handlers.AddOrUpdate(clientName, newHandlers, (key, existing) => MergeHandlers(existing, newHandlers));
		}

		/// <summary>
		/// Gets the registered callback handlers for a specific client.
		/// </summary>
		/// <param name="clientName">Name of the client</param>
		/// <returns>Callback handlers if registered, null otherwise</returns>
		internal static SocketCallbackHandlers? GetHandlers(string clientName)
		{
			if(string.IsNullOrWhiteSpace(clientName))
			{
				return null;
			}

			handlers.TryGetValue(clientName, out SocketCallbackHandlers? result);
			return result;
		}

		/// <summary>
		/// Removes callback handlers for a specific client.
		/// </summary>
		/// <param name="clientName">Name of the client</param>
		/// <returns>True if handlers were removed, false if client had no registered handlers</returns>
		public static bool UnregisterHandlers(string clientName)
		{
			if(string.IsNullOrWhiteSpace(clientName))
			{
				return false;
			}

			return handlers.TryRemove(clientName, out _);
		}

		/// <summary>
		/// Clears all registered callback handlers.
		/// </summary>
		public static void Clear()
		{
			handlers.Clear();
		}

		/// <summary>
		/// Checks if a client has registered callback handlers.
		/// </summary>
		/// <param name="clientName">Name of the client</param>
		/// <returns>True if the client has registered handlers, false otherwise</returns>
		public static bool HasHandlers(string clientName)
		{
			if(string.IsNullOrWhiteSpace(clientName))
			{
				return false;
			}

			return handlers.ContainsKey(clientName);
		}
	}
}