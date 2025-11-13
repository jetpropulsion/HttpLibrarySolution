using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace HttpLibrary
{
	/// <summary>
	/// Encapsulates HTTP request headers with case-insensitive key comparison.
	/// Provides a strongly-typed wrapper around header name-value pairs.
	/// Manages both per-request headers and custom runtime headers.
	/// </summary>
	public sealed class HttpRequestHeaders : IEnumerable<KeyValuePair<string, string>>
	{
		readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> headers;
		readonly bool isCustomHeadersContainer;

		/// <summary>
		/// Initializes a new instance of HttpRequestHeaders with case-insensitive header names.
		/// </summary>
		public HttpRequestHeaders()
		{
			headers = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			isCustomHeadersContainer = false;
		}

		/// <summary>
		/// Initializes a new instance of HttpRequestHeaders as a custom headers container.
		/// </summary>
		/// <param name="isCustomContainer">If true, this instance manages custom runtime headers</param>
		private HttpRequestHeaders(bool isCustomContainer)
		{
			headers = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			isCustomHeadersContainer = isCustomContainer;
		}

		/// <summary>
		/// Initializes a new instance of HttpRequestHeaders from an existing dictionary.
		/// </summary>
		/// <param name="headers">Dictionary of header name-value pairs</param>
		public HttpRequestHeaders(IDictionary<string, string>? headers)
		{
			this.headers = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			isCustomHeadersContainer = false;

			if(headers != null)
			{
				foreach(KeyValuePair<string, string> kvp in headers)
				{
					this.headers[ kvp.Key ] = kvp.Value;
				}
			}
		}

		/// <summary>
		/// Creates a custom headers container for runtime header management.
		/// </summary>
		/// <returns>HttpRequestHeaders instance configured for custom headers</returns>
		public static HttpRequestHeaders CreateCustomHeadersContainer()
		{
			return new HttpRequestHeaders(isCustomContainer: true);
		}

		/// <summary>
		/// Adds or updates a header.
		/// </summary>
		/// <param name="name">Header name (case-insensitive)</param>
		/// <param name="value">Header value</param>
		public void Add(string name, string value)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("Header name cannot be null or whitespace", nameof(name));
			}

			if(value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			headers[ name ] = value;
		}

		/// <summary>
		/// Adds or updates a header with RFC validation.
		/// </summary>
		/// <param name="name">Header name (validated per RFC 7230)</param>
		/// <param name="value">Header value (validated per RFC 7230)</param>
		public void AddWithValidation(string name, string value)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("Header name cannot be null or whitespace", nameof(name));
			}

			if(value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			// RFC 7230 Section 3.2: Validate header name and value
			if(!HttpHeaderValidator.IsValidHeaderName(name))
			{
				throw new ArgumentException($"Invalid header name '{name}' (RFC 7230 violation)", nameof(name));
			}

			if(!HttpHeaderValidator.IsValidHeaderValue(value))
			{
				throw new ArgumentException($"Invalid header value for '{name}' (RFC 7230 violation)", nameof(value));
			}

			// RFC7231 Section5.5.3: Special validation for User-Agent
			if(string.Equals(name, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
			{
				if(!HttpHeaderValidator.IsValidUserAgent(value))
				{
					throw new ArgumentException("Invalid User-Agent header (RFC 7231 violation)", nameof(value));
				}
			}

			headers[ name ] = value;
		}

		/// <summary>
		/// Removes a header.
		/// </summary>
		/// <param name="name">Header name to remove</param>
		/// <returns>True if header was removed, false otherwise</returns>
		public bool Remove(string name)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				return false;
			}

			return headers.TryRemove(name, out string? _);
		}

		/// <summary>
		/// Clears all headers.
		/// </summary>
		public void Clear()
		{
			headers.Clear();
		}

		/// <summary>
		/// Gets the number of headers.
		/// </summary>
		public int Count => headers.Count;

		/// <summary>
		/// Merges headers from another HttpRequestHeaders instance into this one.
		/// </summary>
		/// <param name="source">Source headers to merge from</param>
		/// <param name="overwrite">If true, source headers overwrite existing headers; if false, existing headers are preserved</param>
		/// <returns>Number of headers merged</returns>
		public int Merge(HttpRequestHeaders source, bool overwrite = false)
		{
			if(source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			int mergedCount = 0;
			foreach(KeyValuePair<string, string> kvp in source.headers)
			{
				if(overwrite)
				{
					headers[ kvp.Key ] = kvp.Value;
					mergedCount++;
				}
				else
				{
					if(headers.TryAdd(kvp.Key, kvp.Value))
					{
						mergedCount++;
					}
				}
			}

			return mergedCount;
		}

		/// <summary>
		/// Merges headers from a dictionary into this instance.
		/// </summary>
		/// <param name="source">Source dictionary to merge from</param>
		/// <param name="overwrite">If true, source headers overwrite existing headers; if false, existing headers are preserved</param>
		/// <returns>Number of headers merged</returns>
		public int Merge(IDictionary<string, string> source, bool overwrite = false)
		{
			if(source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			int mergedCount = 0;
			foreach(KeyValuePair<string, string> kvp in source)
			{
				if(overwrite)
				{
					headers[ kvp.Key ] = kvp.Value;
					mergedCount++;
				}
				else
				{
					if(headers.TryAdd(kvp.Key, kvp.Value))
					{
						mergedCount++;
					}
				}
			}

			return mergedCount;
		}

		/// <summary>
		/// Creates a copy of this HttpRequestHeaders instance.
		/// </summary>
		/// <returns>A new HttpRequestHeaders instance with the same headers</returns>
		public HttpRequestHeaders Clone()
		{
			HttpRequestHeaders cloned = new HttpRequestHeaders(isCustomContainer: isCustomHeadersContainer);
			foreach(KeyValuePair<string, string> kvp in headers)
			{
				cloned.headers[ kvp.Key ] = kvp.Value;
			}
			return cloned;
		}

		/// <summary>
		/// Applies headers to an HttpRequestMessage with RFC validation.
		/// Applies custom headers first, then per-request headers (which can override custom headers).
		/// </summary>
		/// <param name="request">The HttpRequestMessage to apply headers to</param>
		/// <param name="customHeadersSource">Optional custom headers to apply first</param>
		/// <param name="onInvalidHeader">Optional callback for invalid headers (name, reason)</param>
		public void ApplyTo(HttpRequestMessage request, HttpRequestHeaders? customHeadersSource = null, Action<string, string>? onInvalidHeader = null)
		{
			if(request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			// First, apply custom headers from the source (if provided)
			if(customHeadersSource != null)
			{
				foreach(KeyValuePair<string, string> kv in customHeadersSource.headers)
				{
					ApplySingleHeader(request, kv.Key, kv.Value, isCustomHeader: true, onInvalidHeader);
				}
			}

			// Then, apply per-request headers (these can override custom headers)
			foreach(KeyValuePair<string, string> kv in headers)
			{
				// If header exists from custom headers, remove it first
				if(request.Headers.Contains(kv.Key))
				{
					request.Headers.Remove(kv.Key);
				}

				ApplySingleHeader(request, kv.Key, kv.Value, isCustomHeader: false, onInvalidHeader);
			}
		}

		/// <summary>
		/// Applies a single header to the request with validation.
		/// </summary>
		private void ApplySingleHeader(HttpRequestMessage request, string name, string value, bool isCustomHeader, Action<string, string>? onInvalidHeader)
		{
			// RFC 7230 Section 3.2: Validate header name and value (only for per-request headers, custom headers are pre-validated)
			if(!isCustomHeader)
			{
				if(!HttpHeaderValidator.IsValidHeaderName(name))
				{
					onInvalidHeader?.Invoke(name, "Invalid header name (RFC 7230 violation)");
					return;
				}

				if(!HttpHeaderValidator.IsValidHeaderValue(value))
				{
					onInvalidHeader?.Invoke(name, "Invalid header value (RFC 7230 violation)");
					return;
				}
			}

			// RFC7231 Section5.5.3: Special validation for User-Agent
			if(string.Equals(name, Constants.HeaderUserAgent, StringComparison.OrdinalIgnoreCase))
			{
				if(!isCustomHeader && !HttpHeaderValidator.IsValidUserAgent(value))
				{
					onInvalidHeader?.Invoke(name, "Invalid User-Agent header (RFC7231 violation)");
					return;
				}

				ApplyUserAgent(request, value);
			}
			else
			{
				request.Headers.TryAddWithoutValidation(name, value);
			}
		}

		/// <summary>
		/// Applies User-Agent header with RFC 7231 compliant parsing.
		/// </summary>
		private static void ApplyUserAgent(HttpRequestMessage request, string raw)
		{
			if(request == null)
			{
				return;
			}

			request.Headers.Remove(Constants.HeaderUserAgent);
			request.Headers.UserAgent.Clear();

			System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
			foreach(ProductInfoHeaderValue token in ParseUserAgentTokens(raw))
			{
				parts.Add(token.ToString());
			}

			string ua = string.Join(" ", parts);
			request.Headers.TryAddWithoutValidation(Constants.HeaderUserAgent, ua);
		}

		/// <summary>
		/// Parses User-Agent tokens per RFC 7231.
		/// </summary>
		internal static IEnumerable<ProductInfoHeaderValue> ParseUserAgentTokens(string raw)
		{
			if(string.IsNullOrWhiteSpace(raw))
			{
				yield break;
			}

			// RFC7231 compliant User-Agent parsing
			System.Collections.Generic.List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(raw);
			foreach(UserAgentToken token in tokens)
			{
				if(token.IsComment)
				{
					yield return new ProductInfoHeaderValue(token.Comment!);
				}
				else if(token.Version != null)
				{
					yield return new ProductInfoHeaderValue(token.Product!, token.Version);
				}
				else if(token.Product != null)
				{
					// Product without version - wrap in comment to preserve
					string comment = $"({token.Product})";
					yield return new ProductInfoHeaderValue(comment);
				}
			}
		}

		/// <summary>
		/// Tries to get a header value.
		/// </summary>
		/// <param name="name">Header name (case-insensitive)</param>
		/// <param name="value">Header value if found</param>
		/// <returns>True if header exists, false otherwise</returns>
		public bool TryGetValue(string name, out string? value)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				value = null;
				return false;
			}

			return headers.TryGetValue(name, out value);
		}

		/// <summary>
		/// Checks if a header exists.
		/// </summary>
		/// <param name="name">Header name (case-insensitive)</param>
		/// <returns>True if header exists, false otherwise</returns>
		public bool Contains(string name)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				return false;
			}

			return headers.ContainsKey(name);
		}

		/// <summary>
		/// Gets all header names.
		/// </summary>
		public IEnumerable<string> Names => headers.Keys;

		/// <summary>
		/// Gets all header values.
		/// </summary>
		public IEnumerable<string> Values => headers.Values;

		/// <summary>
		/// Gets or sets a header value by name (case-insensitive).
		/// </summary>
		/// <param name="name">Header name</param>
		/// <returns>Header value</returns>
		public string this[ string name ]
		{
			get => headers[ name ];
			set => headers[ name ] = value;
		}

		/// <summary>
		/// Creates an HttpRequestHeaders instance from a dictionary.
		/// Returns null if the input is null.
		/// </summary>
		/// <param name="headers">Dictionary of headers</param>
		/// <returns>HttpRequestHeaders instance or null</returns>
		public static HttpRequestHeaders? FromDictionary(IDictionary<string, string>? headers)
		{
			if(headers == null)
			{
				return null;
			}

			return new HttpRequestHeaders(headers);
		}

		/// <summary>
		/// Converts to a standard dictionary.
		/// </summary>
		/// <returns>Dictionary of header name-value pairs</returns>
		public Dictionary<string, string> ToDictionary()
		{
			return new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			return headers.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		/// Creates an empty HttpRequestHeaders instance.
		/// </summary>
		public static HttpRequestHeaders Empty => new HttpRequestHeaders();
	}
}