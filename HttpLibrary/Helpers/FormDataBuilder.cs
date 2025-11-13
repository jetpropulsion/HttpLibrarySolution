using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace HttpLibrary
{
	/// <summary>
	/// Helper class for building RFC-compliant form data content.
	/// Supports application/x-www-form-urlencoded (RFC1866) and multipart/form-data (RFC7578).
	/// </summary>
	public sealed class FormDataBuilder
	{
		readonly Dictionary<string, string> fields = new Dictionary<string, string>();
		readonly List<FileField> files = new List<FileField>();

		/// <summary>
		/// Adds a text field to the form data.
		/// </summary>
		/// <param name="name">Field name</param>
		/// <param name="value">Field value</param>
		public void AddField(string name, string value)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("Field name cannot be null or whitespace", nameof(name));
			}

			fields[ name ] = value ?? string.Empty;
		}

		/// <summary>
		/// Adds a file to the form data (only valid for multipart/form-data).
		/// </summary>
		/// <param name="name">Field name</param>
		/// <param name="fileName">File name to send</param>
		/// <param name="fileContent">File content as byte array</param>
		/// <param name="contentType">MIME content type (defaults to application/octet-stream)</param>
		public void AddFile(string name, string fileName, byte[] fileContent, string? contentType = null)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("Field name cannot be null or whitespace", nameof(name));
			}
			if(string.IsNullOrWhiteSpace(fileName))
			{
				throw new ArgumentException("File name cannot be null or whitespace", nameof(fileName));
			}
			if(fileContent == null)
			{
				throw new ArgumentNullException(nameof(fileContent));
			}

			files.Add(new FileField
			{
				Name = name,
				FileName = fileName,
				Content = fileContent,
				ContentType = contentType ?? Constants.MediaTypeOctetStream
			});
		}

		/// <summary>
		/// Adds a file from disk to the form data (only valid for multipart/form-data).
		/// </summary>
		/// <param name="name">Field name</param>
		/// <param name="filePath">Path to the file</param>
		/// <param name="contentType">MIME content type (defaults to application/octet-stream)</param>
		public void AddFileFromDisk(string name, string filePath, string? contentType = null)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("Field name cannot be null or whitespace", nameof(name));
			}
			if(string.IsNullOrWhiteSpace(filePath))
			{
				throw new ArgumentException("File path cannot be null or whitespace", nameof(filePath));
			}
			if(!File.Exists(filePath))
			{
				throw new FileNotFoundException("File not found", filePath);
			}

			byte[] fileContent = File.ReadAllBytes(filePath);
			string fileName = Path.GetFileName(filePath);

			files.Add(new FileField
			{
				Name = name,
				FileName = fileName,
				Content = fileContent,
				ContentType = contentType ?? Constants.MediaTypeOctetStream
			});
		}

		/// <summary>
		/// Builds application/x-www-form-urlencoded content (RFC1866 Section8.2.1).
		/// Only text fields are supported. Files are ignored.
		/// </summary>
		/// <returns>FormUrlEncodedContent ready to send</returns>
		public FormUrlEncodedContent BuildUrlEncodedContent()
		{
			List<KeyValuePair<string, string>> formData = new List<KeyValuePair<string, string>>();

			foreach(KeyValuePair<string, string> field in fields)
			{
				formData.Add(new KeyValuePair<string, string>(field.Key, field.Value));
			}

			return new FormUrlEncodedContent(formData);
		}

		/// <summary>
		/// Builds multipart/form-data content (RFC7578).
		/// Supports both text fields and file uploads.
		/// </summary>
		/// <returns>MultipartFormDataContent ready to send</returns>
		public MultipartFormDataContent BuildMultipartContent()
		{
			// RFC7578: Generate a boundary that won't appear in the data
			string boundary = GenerateBoundary();
			MultipartFormDataContent content = new MultipartFormDataContent(boundary);

			// Add text fields
			foreach(KeyValuePair<string, string> field in fields)
			{
				StringContent stringContent = new StringContent(field.Value, Constants.DefaultTextEncoding);
				// RFC7578 Section4.2: Content-Disposition header for form fields
				stringContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
				{
					Name = QuoteString(field.Key)
				};
				// Remove Content-Type header for plain text fields (per RFC7578)
				stringContent.Headers.ContentType = null;
				content.Add(stringContent);
			}

			// Add files
			foreach(FileField file in files)
			{
				ByteArrayContent fileContent = new ByteArrayContent(file.Content);
				fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
				// RFC7578 Section4.2: Content-Disposition header for file fields
				fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
				{
					Name = QuoteString(file.Name),
					FileName = QuoteString(file.FileName)
				};
				content.Add(fileContent);
			}

			return content;
		}

		/// <summary>
		/// Generates a random boundary string for multipart/form-data (RFC7578 Section4.1).
		/// </summary>
		private static string GenerateBoundary()
		{
			// RFC7578: boundary is1*70 bcharsnospace
			// Use a GUID-based boundary that's safe and unique
			return "----WebKitFormBoundary" + Guid.NewGuid().ToString("N");
		}

		/// <summary>
		/// Quotes a string for use in HTTP headers (RFC7578 Section4.2).
		/// </summary>
		private static string QuoteString(string value)
		{
			// RFC7578: quoted-string format
			return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
		}

		sealed class FileField
		{
			public string Name { get; set; } = string.Empty;
			public string FileName { get; set; } = string.Empty;
			public byte[] Content { get; set; } = Array.Empty<byte>();
			public string ContentType { get; set; } = Constants.MediaTypeOctetStream;
		}
	}
}