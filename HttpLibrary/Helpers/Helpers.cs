using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace HttpLibrary
{
	/// <summary>
	/// Convenience helpers intended for reuse by applications built on top of HttpLibrary.
	/// This consolidates commonly useful functions previously in the CLI helpers and
	/// acts as a single import for small utility routines (content type detection,
	/// human-readable formatting, small payload creation helpers, etc.).
	/// </summary>
	public static class Helpers
	{
		private static readonly FrozenDictionary<string, string> _mimeTypes =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				[ ".txt" ] = Constants.MediaTypePlainText,
				[ ".html" ] = Constants.MediaTypeHtml,
				[ ".htm" ] = Constants.MediaTypeHtml,
				[ ".css" ] = "text/css",
				[ ".csv" ] = "text/csv",
				[ ".xml" ] = Constants.MediaTypeXml,
				[ ".rtf" ] = "application/rtf",
				[ ".md" ] = "text/markdown",
				[ ".json" ] = Constants.MediaTypeJson,
				[ ".js" ] = "application/javascript",
				[ ".mjs" ] = "application/javascript",
				[ ".yaml" ] = "application/x-yaml",
				[ ".yml" ] = "application/x-yaml",
				[ ".pdf" ] = "application/pdf",
				[ ".zip" ] = "application/zip",
				[ ".gz" ] = "application/gzip",
				[ ".jpg" ] = "image/jpeg",
				[ ".jpeg" ] = "image/jpeg",
				[ ".png" ] = "image/png",
				[ ".gif" ] = "image/gif",
				[ ".bmp" ] = "image/bmp",
				[ ".webp" ] = "image/webp",
				[ ".svg" ] = "image/svg+xml",
				[ ".ico" ] = "image/x-icon",
				[ ".mp3" ] = "audio/mpeg",
				[ ".wav" ] = "audio/wav",
				[ ".mp4" ] = "video/mp4",
				[ ".webm" ] = "video/webm",
				[ ".woff" ] = "font/woff",
				[ ".woff2" ] = "font/woff2",
				[ ".ttf" ] = "font/ttf",
				[ ".otf" ] = "font/otf",
				[ ".bin" ] = Constants.MediaTypeOctetStream,
			}.ToFrozenDictionary();

		/// <summary>
		/// Formats bytes to human-readable format (B, KB, MB, GB)
		/// </summary>
		public static string FormatBytes(long bytes)
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			double len = bytes;
			int order = 0;
			while(len >= 1024 && order < sizes.Length - 1)
			{
				order++;
				len = len / 1024;
			}
			return $"{len:0.##} {sizes[ order ]}";
		}

		/// <summary>
		/// Short human readable time-left formatting used by CLI output.
		/// </summary>
		public static string FormatTimeLeft(DateTime? expires, DateTime now)
		{
			if(!expires.HasValue)
			{
				return Constants.SessionCookieText;
			}

			TimeSpan timeLeft = expires.Value - now;
			if(timeLeft.TotalSeconds < 0)
			{
				return "Expired";
			}

			if(timeLeft.TotalDays >= 1)
			{
				return $"{(int)timeLeft.TotalDays}d {timeLeft.Hours}h";
			}

			if(timeLeft.TotalHours >= 1)
			{
				return $"{(int)timeLeft.TotalHours}h {timeLeft.Minutes}m";
			}

			if(timeLeft.TotalMinutes >= 1)
			{
				return $"{(int)timeLeft.TotalMinutes}m {timeLeft.Seconds}s";
			}

			return $"{(int)timeLeft.TotalSeconds}s";
		}

		/// <summary>
		/// Long human readable time-left format.
		/// </summary>
		public static string FormatTimeLeftLong(DateTime expires, DateTime now)
		{
			TimeSpan timeLeft = expires - now;
			if(timeLeft.TotalSeconds < 0)
			{
				return "Expired";
			}

			int years = (int)( timeLeft.TotalDays / 365.25 );
			int days = (int)( timeLeft.TotalDays % 365.25 );
			int hours = timeLeft.Hours;
			int minutes = timeLeft.Minutes;
			int seconds = timeLeft.Seconds;

			if(years > 0)
			{
				string yearText = years == 1 ? "year" : "years";
				string dayText = days == 1 ? "day" : "days";
				string hourText = hours == 1 ? "hour" : "hours";
				string minuteText = minutes == 1 ? "minute" : "minutes";
				string secondText = seconds == 1 ? "second" : "seconds";
				return $"{years} {yearText}, {days} {dayText}, {hours} {hourText}, {minutes} {minuteText}, {seconds} {secondText}";
			}

			if(timeLeft.TotalDays >= 1)
			{
				string dayText = timeLeft.Days == 1 ? "day" : "days";
				return $"{timeLeft.Days} {dayText}, {hours} hours, {minutes} minutes";
			}

			if(timeLeft.TotalHours >= 1)
			{
				return $"{(int)timeLeft.TotalHours} hours, {minutes} minutes";
			}

			if(timeLeft.TotalMinutes >= 1)
			{
				return $"{(int)timeLeft.TotalMinutes} minutes, {timeLeft.Seconds} seconds";
			}

			return $"{(int)timeLeft.TotalSeconds} seconds";
		}

		/// <summary>
		/// Build cookie flags string for display
		/// </summary>
		public static string BuildCookieFlags(PersistedCookie cookie)
		{
			List<string> flags = new List<string>();

			if(cookie.Secure)
			{
				flags.Add("Secure");
			}

			if(cookie.HttpOnly)
			{
				flags.Add("HttpOnly");
			}

			if(!string.IsNullOrEmpty(cookie.SameSite))
			{
				flags.Add($"SameSite={cookie.SameSite}");
			}

			return flags.Count > 0 ? string.Join("; ", flags) : string.Empty;
		}

		/// <summary>
		/// Returns true when the provided verb is an HTTP verb
		/// </summary>
		public static bool IsHttpVerb(string verb)
		{
			string[] httpVerbs = { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE", "CONNECT" };
			foreach(string v in httpVerbs)
			{
				if(string.Equals(verb, v, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Create HttpContent from a literal string or a file path prefixed with '@'.
		/// If appConfig is provided, file size limits are validated against configuration.
		/// </summary>
		public static HttpContent CreateHttpContent(string? bodyOrFile, string verb, ApplicationConfiguration? appConfig = null)
		{
			if(string.IsNullOrWhiteSpace(bodyOrFile))
			{
				return new StringContent(string.Empty);
			}

			if(bodyOrFile.StartsWith("@", StringComparison.OrdinalIgnoreCase))
			{
				string filePath = bodyOrFile.Substring(1);
				if(!File.Exists(filePath))
				{
					throw new FileNotFoundException($"File not found: {filePath}", filePath);
				}

				if(appConfig != null && appConfig.FileUpload != null)
				{
					FileInfo fi = new FileInfo(filePath);
					if(fi.Length > appConfig.FileUpload.MaxFileSizeBytes)
					{
						throw new ArgumentException($"File too large: {fi.Length:N0} bytes (maximum: {appConfig.FileUpload.MaxFileSizeBytes:N0} bytes)", nameof(bodyOrFile));
					}
				}

				string fileContent = File.ReadAllText(filePath);

				string extension = Path.GetExtension(filePath);
				string contentType = GetContentTypeFromExtension(extension);

				StringContent content = new StringContent(fileContent);
				content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
				return content;
			}

			return new StringContent(bodyOrFile);
		}

		/// <summary>
		/// Map file extension to MIME type; returns application/octet-stream by default.
		/// </summary>
		public static string GetContentTypeFromExtension(string? extension)
		{
			if(string.IsNullOrWhiteSpace(extension))
			{
				return Constants.MediaTypeOctetStream;
			}

			string ext = extension.ToLowerInvariant();
			if(!ext.StartsWith("."))
			{
				ext = "." + ext;
			}

			return _mimeTypes.TryGetValue(ext, out string? contentType) ? contentType : Constants.MediaTypeOctetStream;
		}
	}
}