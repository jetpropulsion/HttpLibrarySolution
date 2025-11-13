using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibrary.Handlers
{
	/// <summary>
	/// Ensures basic HTTP compliance for requests produced by the library:
	/// - Ensures a sensible User-Agent is present
	/// - Performs lightweight header validation (no CTLs)
	/// This handler is small, deterministic and trimming/AOT-friendly.
	/// </summary>
	internal sealed class HttpComplianceHandler : DelegatingHandler
	{
		private readonly string _userAgent;

		public HttpComplianceHandler()
		{
			_userAgent = "HttpLibrary/1.0";
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if(request == null)
				throw new ArgumentNullException(nameof(request));

			// Ensure User-Agent header
			try
			{
				if(request.Headers.UserAgent?.Any() != true)
				{
					request.Headers.UserAgent?.ParseAdd(_userAgent);
				}
			}
			catch
			{
				// best-effort
			}

			// Lightweight header validation: names and values must not contain control chars
			try
			{
				foreach(var header in request.Headers)
				{
					if(!IsValidHeaderName(header.Key))
						continue;
					foreach(string v in header.Value)
					{
						if(!IsValidHeaderValue(v))
							continue;
					}
				}
			}
			catch
			{
				// ignore validation failures; do not block request
			}

			return base.SendAsync(request, cancellationToken);
		}

		private static bool IsValidHeaderName(string name)
		{
			if(string.IsNullOrWhiteSpace(name))
				return false;
			foreach(char c in name)
			{
				if(char.IsControl(c) || c == '\r' || c == '\n')
					return false;
			}
			return true;
		}

		private static bool IsValidHeaderValue(string value)
		{
			if(value == null)
				return false;
			foreach(char c in value)
			{
				if(char.IsControl(c) && c != '\t')
					return false;
			}
			return true;
		}
	}
}