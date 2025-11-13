using System;
using System.Collections.Generic;
using System.Linq;

namespace HttpLibrary
{
	/// <summary>
	/// Validates HTTP headers according to RFC7230 and RFC7231.
	/// </summary>
	public static class HttpHeaderValidator
	{
		/// <summary>
		/// Validates header name according to RFC7230 Section3.2.
		/// Header field name must be a token (visible ASCII excluding delimiters).
		/// </summary>
		public static bool IsValidHeaderName(string name)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				return false;
			}

			// RFC7230 Section3.2: token characters are visible ASCII except delimiters
			// tchar = "!" / "#" / "$" / "%" / "&" / "'" / "*" / "+" / "-" / "." / 
			// "^" / "_" / "`" / "|" / "~" / DIGIT / ALPHA
			foreach(char c in name)
			{
				if(!IsTokenChar(c))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Validates header value according to RFC7230 Section3.2.
		/// Field values are visible ASCII + whitespace, no control characters except HTAB.
		/// </summary>
		public static bool IsValidHeaderValue(string value)
		{
			if(value == null)
			{
				return false;
			}

			// Empty values are allowed
			if(value.Length == 0)
			{
				return true;
			}

			// RFC7230 Section3.2: field-value = *( field-content / obs-fold )
			// field-content = field-vchar [1*( SP / HTAB ) field-vchar ]
			// field-vchar = VCHAR / obs-text
			// obs-text = %x80-FF
			foreach(char c in value)
			{
				// Allow HTAB (0x09), SP (0x20), visible ASCII (0x21-0x7E), and obs-text (0x80-0xFF)
				if(c == '\t' || c == ' ')
				{
					continue;
				}
				if(c >= 0x21 && c <= 0x7E)
				{
					continue;
				}
				if(c >= 0x80 && c <= 0xFF)
				{
					continue;
				}
				// Reject control characters (except HTAB)
				return false;
			}
			return true;
		}

		/// <summary>
		/// Checks if a character is a valid token character per RFC7230.
		/// </summary>
		static bool IsTokenChar(char c)
		{
			// RFC7230 tchar: visible ASCII except delimiters
			// Delimiters: ( ) < > @ , ; : \ " / [ ] ? = { } SP HT
			if(c < 0x21 || c > 0x7E)
			{
				return false;
			}

			return !Constants.Rfc7230HeaderDelimiters.Contains(c);
		}

		/// <summary>
		/// Validates User-Agent header value according to RFC7231 Section5.5.3.
		/// User-Agent = product *( RWS ( product / comment ) )
		/// </summary>
		public static bool IsValidUserAgent(string userAgent)
		{
			if(string.IsNullOrWhiteSpace(userAgent))
			{
				return false;
			}

			// Basic validation: ensure it doesn't contain invalid control characters
			foreach(char c in userAgent)
			{
				// Reject control characters except HTAB and SP
				if(char.IsControl(c) && c != '\t')
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Parses User-Agent header according to RFC7231 Section5.5.3.
		/// Returns list of product/version tokens and comments.
		/// </summary>
		internal static List<UserAgentToken> ParseUserAgent(string userAgent)
		{
			List<UserAgentToken> tokens = new List<UserAgentToken>();
			if(string.IsNullOrWhiteSpace(userAgent))
			{
				return tokens;
			}

			int i = 0;
			int n = userAgent.Length;

			while(i < n)
			{
				// Skip whitespace
				while(i < n && ( userAgent[ i ] == ' ' || userAgent[ i ] == '\t' ))
				{
					i++;
				}
				if(i >= n)
				{
					break;
				}

				// Parse comment: (...)
				if(userAgent[ i ] == '(')
				{
					int start = i;
					i++;
					int depth = 1;
					while(i < n && depth > 0)
					{
						if(userAgent[ i ] == '(')
						{
							depth++;
						}
						else if(userAgent[ i ] == ')')
						{
							depth--;
						}
						i++;
					}
					if(depth == 0)
					{
						string comment = userAgent.Substring(start, i - start);
						tokens.Add(new UserAgentToken { Comment = comment });
					}
					continue;
				}

				// Parse product token: name/version or name
				int tokenStart = i;
				while(i < n && userAgent[ i ] != ' ' && userAgent[ i ] != '\t' && userAgent[ i ] != '(')
				{
					i++;
				}
				string token = userAgent.Substring(tokenStart, i - tokenStart);

				if(token.Length > 0)
				{
					int slashIndex = token.IndexOf('/');
					if(slashIndex > 0 && slashIndex < token.Length - 1)
					{
						string product = token.Substring(0, slashIndex);
						string version = token.Substring(slashIndex + 1);
						tokens.Add(new UserAgentToken { Product = product, Version = version });
					}
					else
					{
						// Product without version
						tokens.Add(new UserAgentToken { Product = token });
					}
				}
			}

			return tokens;
		}
	}
}