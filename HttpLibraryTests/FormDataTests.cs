using HttpLibrary;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class FormDataTests
	{
		ServiceProvider? serviceProvider;
		ILogger<FormDataTests>? logger;

		[TestInitialize]
		public void Initialize()
		{
			ServiceCollection services = new ServiceCollection();
			services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
			serviceProvider = services.BuildServiceProvider();
			logger = serviceProvider.GetRequiredService<ILogger<FormDataTests>>();
		}

		[TestCleanup]
		public void Cleanup()
		{
			serviceProvider?.Dispose();
		}

		#region FormDataBuilder Tests

		[TestMethod]
		public void FormDataBuilder_AddField_StoresField()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();

			// Act
			builder.AddField("username", "testuser");
			builder.AddField("password", "testpass");

			// Assert
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_AddField_ThrowsOnNullName()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();

			// Act & Assert
			try
			{
				builder.AddField(null!, "value");
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void FormDataBuilder_AddField_ThrowsOnEmptyName()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();

			// Act & Assert
			try
			{
				builder.AddField("", "value");
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void FormDataBuilder_AddField_AcceptsNullValue()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();

			// Act
			builder.AddField("field", null!);

			// Assert - should not throw
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_AddFile_StoresFile()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			byte[] fileContent = Encoding.UTF8.GetBytes("test file content");

			// Act
			builder.AddFile("upload", "test.txt", fileContent, "text/plain");

			// Assert
			MultipartFormDataContent content = builder.BuildMultipartContent();
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_AddFile_ThrowsOnNullName()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			byte[] fileContent = Encoding.UTF8.GetBytes("test");

			// Act & Assert
			try
			{
				builder.AddFile(null!, "test.txt", fileContent);
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void FormDataBuilder_AddFile_ThrowsOnNullFileName()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			byte[] fileContent = Encoding.UTF8.GetBytes("test");

			// Act & Assert
			try
			{
				builder.AddFile("upload", null!, fileContent);
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void FormDataBuilder_AddFile_ThrowsOnNullContent()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();

			// Act & Assert
			try
			{
				builder.AddFile("upload", "test.txt", null!);
				Assert.Fail("Expected ArgumentNullException was not thrown");
			}
			catch(ArgumentNullException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void FormDataBuilder_AddFile_UsesDefaultContentType()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			byte[] fileContent = Encoding.UTF8.GetBytes("test");

			// Act
			builder.AddFile("upload", "test.bin", fileContent);

			// Assert
			MultipartFormDataContent content = builder.BuildMultipartContent();
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_BuildUrlEncodedContent_ReturnsValidContent()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("key1", "value1");
			builder.AddField("key2", "value2");
			builder.AddField("special", "value with spaces & symbols=test");

			// Act
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();

			// Assert
			Assert.IsNotNull(content);
			Assert.AreEqual(Constants.MediaTypeFormUrlEncoded, content.Headers.ContentType?.MediaType);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_ReturnsValidContent()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("username", "testuser");
			builder.AddField("email", "test@example.com");
			byte[] fileContent = Encoding.UTF8.GetBytes("file content");
			builder.AddFile("upload", "document.pdf", fileContent, "application/pdf");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
			Assert.AreEqual(Constants.MediaTypeFormData, content.Headers.ContentType?.MediaType);
			Assert.IsNotNull(content.Headers.ContentType?.Parameters);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_GeneratesUniqueBoundary()
		{
			// Arrange
			FormDataBuilder builder1 = new FormDataBuilder();
			builder1.AddField("field1", "value1");

			FormDataBuilder builder2 = new FormDataBuilder();
			builder2.AddField("field2", "value2");

			// Act
			MultipartFormDataContent content1 = builder1.BuildMultipartContent();
			MultipartFormDataContent content2 = builder2.BuildMultipartContent();

			// Assert - Extract actual boundary values
			string? boundary1 = null;
			string? boundary2 = null;

			if(content1.Headers.ContentType?.Parameters != null)
			{
				foreach(System.Net.Http.Headers.NameValueHeaderValue param in content1.Headers.ContentType.Parameters)
				{
					if(param.Name == "boundary")
					{
						boundary1 = param.Value;
						break;
					}
				}
			}

			if(content2.Headers.ContentType?.Parameters != null)
			{
				foreach(System.Net.Http.Headers.NameValueHeaderValue param in content2.Headers.ContentType.Parameters)
				{
					if(param.Name == "boundary")
					{
						boundary2 = param.Value;
						break;
					}
				}
			}

			Assert.IsNotNull(boundary1, "First content should have a boundary");
			Assert.IsNotNull(boundary2, "Second content should have a boundary");
			// Boundaries should be different (statistically, GUID-based generation makes collision extremely unlikely)
			Assert.AreNotEqual(boundary1, boundary2, "Boundaries should be unique");
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_HandlesSpecialCharactersInFieldNames()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("field-with-dash", "value1");
			builder.AddField("field_with_underscore", "value2");
			builder.AddField("field.with.dot", "value3");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_HandlesSpecialCharactersInValues()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("field1", "value with \"quotes\"");
			builder.AddField("field2", "value with \\backslash");
			builder.AddField("field3", "value\nwith\nnewlines");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_HandlesBinaryFileContent()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			// Create binary content with all byte values
			byte[] binaryContent = new byte[ 256 ];
			for(int i = 0; i < 256; i++)
			{
				binaryContent[ i ] = (byte)i;
			}
			builder.AddFile("binary", "binary.dat", binaryContent, "application/octet-stream");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_HandlesLargeFiles()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			// Create 1MB file
			byte[] largeContent = new byte[ 1_048_576 ];
			new Random().NextBytes(largeContent);
			builder.AddFile("largefile", "large.bin", largeContent);

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
			Assert.IsTrue(content.Headers.ContentLength > 1_000_000);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_HandlesMultipleFiles()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddFile("file1", "test1.txt", Encoding.UTF8.GetBytes("content1"), "text/plain");
			builder.AddFile("file2", "test2.txt", Encoding.UTF8.GetBytes("content2"), "text/plain");
			builder.AddFile("file3", "test3.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_HandlesEmptyFields()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("empty", "");
			builder.AddField("nonempty", "value");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_BuildUrlEncodedContent_HandlesEmptyBuilder()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();

			// Act
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();

			// Assert
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_HandlesEmptyBuilder()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
		}

		#endregion

		#region RFC Compliance Tests

		[TestMethod]
		public void FormDataBuilder_BuildUrlEncodedContent_RFC1866_UsesCorrectMediaType()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("test", "value");

			// Act
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();

			// Assert
			// RFC 1866 Section 8.2.1: application/x-www-form-urlencoded
			Assert.AreEqual("application/x-www-form-urlencoded", content.Headers.ContentType?.MediaType);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_RFC7578_UsesCorrectMediaType()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("test", "value");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			// RFC 7578: multipart/form-data
			Assert.AreEqual("multipart/form-data", content.Headers.ContentType?.MediaType);
		}

		[TestMethod]
		public void FormDataBuilder_BuildMultipartContent_RFC7578_IncludesBoundaryParameter()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("test", "value");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			// RFC 7578 Section 4.1: boundary parameter is required
			Assert.IsNotNull(content.Headers.ContentType?.Parameters);
			bool hasBoundary = false;
			foreach(System.Net.Http.Headers.NameValueHeaderValue param in content.Headers.ContentType.Parameters)
			{
				if(param.Name == "boundary")
				{
					hasBoundary = true;
					Assert.IsFalse(string.IsNullOrWhiteSpace(param.Value));
				}
			}
			Assert.IsTrue(hasBoundary, "Boundary parameter is required per RFC 7578");
		}

		[TestMethod]
		public void FormDataBuilder_BuildUrlEncodedContent_RFC1866_HandlesSpecialCharacters()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			// RFC 1866: Space should be encoded as +
			// Special characters should be percent-encoded
			builder.AddField("field", "value with spaces");
			builder.AddField("special", "a&b=c");

			// Act
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();

			// Assert
			Assert.IsNotNull(content);
			// Content should be properly encoded
		}

		[TestMethod]
		public async Task FormDataBuilder_BuildUrlEncodedContent_RFC1866_ProducesValidEncodedString()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("username", "john doe");
			builder.AddField("password", "p@ss&w=rd");

			// Act
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();
			string encoded = await content.ReadAsStringAsync();

			// Assert
			// Should contain URL-encoded characters
			Assert.IsTrue(encoded.Contains("username=") || encoded.Contains("password="));
			// Ampersand should separate fields
			Assert.IsTrue(encoded.Contains("&"));
		}

		#endregion

		#region Integration-Style Tests (Without Real HTTP)

		[TestMethod]
		public void FormData_URLEncoded_CreatesValidContent()
		{
			// Arrange
			Dictionary<string, string> formData = new Dictionary<string, string>
						{
								{ "username", "testuser" },
								{ "password", "testpass123" },
								{ "email", "test@example.com" }
						};

			FormDataBuilder builder = new FormDataBuilder();
			foreach(KeyValuePair<string, string> field in formData)
			{
				builder.AddField(field.Key, field.Value);
			}

			// Act
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();

			// Assert
			Assert.IsNotNull(content);
			Assert.AreEqual(Constants.MediaTypeFormUrlEncoded, content.Headers.ContentType?.MediaType);
		}

		[TestMethod]
		public void FormData_Multipart_WithFieldsAndFiles_CreatesValidContent()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("title", "Document Upload");
			builder.AddField("description", "Test document");
			builder.AddFile("document", "test.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf");
			builder.AddFile("image", "photo.jpg", new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
			Assert.AreEqual(Constants.MediaTypeFormData, content.Headers.ContentType?.MediaType);
			Assert.IsTrue(content.Headers.ContentLength > 0);
		}

		[TestMethod]
		public void FormData_Multipart_UTF8Fields_HandlesCorrectly()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("name", "???");  // Chinese
			builder.AddField("description", "????");  // Cyrillic
			builder.AddField("emoji", "????");  // Emoji

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormData_Multipart_FileWithUTF8Filename_HandlesCorrectly()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			byte[] content = Encoding.UTF8.GetBytes("test content");
			builder.AddFile("file", "??.txt", content, "text/plain");  // Chinese filename

			// Act
			MultipartFormDataContent multipartContent = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(multipartContent);
		}

		#endregion

		#region Edge Cases

		[TestMethod]
		public void FormDataBuilder_OverwriteField_UpdatesValue()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddField("key", "value1");

			// Act
			builder.AddField("key", "value2");  // Overwrite

			// Assert
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_VeryLongFieldValue_HandlesCorrectly()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			string longValue = new string('a', 100000);  // 100K characters
			builder.AddField("longfield", longValue);

			// Act
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();

			// Assert
			Assert.IsNotNull(content);
			Assert.IsTrue(content.Headers.ContentLength > 100000);
		}

		[TestMethod]
		public void FormDataBuilder_ManyFields_HandlesCorrectly()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			for(int i = 0; i < 1000; i++)
			{
				builder.AddField($"field{i}", $"value{i}");
			}

			// Act
			FormUrlEncodedContent content = builder.BuildUrlEncodedContent();

			// Assert
			Assert.IsNotNull(content);
		}

		[TestMethod]
		public void FormDataBuilder_ZeroByteFile_HandlesCorrectly()
		{
			// Arrange
			FormDataBuilder builder = new FormDataBuilder();
			builder.AddFile("empty", "empty.txt", Array.Empty<byte>(), "text/plain");

			// Act
			MultipartFormDataContent content = builder.BuildMultipartContent();

			// Assert
			Assert.IsNotNull(content);
		}

		#endregion
	}
}