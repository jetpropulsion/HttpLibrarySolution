using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Net.Http;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for HttpLibrary Helpers utility methods.
	/// Tests file upload, content type detection, time formatting, and cookie flag building.
	/// </summary>
	[TestClass]
	public class LibraryHelpersTests
	{
		string testDirectory = string.Empty;
		string testFilePath = string.Empty;

		[TestInitialize]
		public void TestInitialize()
		{
			// Create temporary test directory
			testDirectory = Path.Combine(Path.GetTempPath(), "HttpLibraryCLI_Tests", Guid.NewGuid().ToString());
			Directory.CreateDirectory(testDirectory);

			// Create test file
			testFilePath = Path.Combine(testDirectory, "test.json");
			File.WriteAllText(testFilePath, "{\"test\":\"data\"}");
		}

		[TestCleanup]
		public void TestCleanup()
		{
			// Clean up test directory
			if(Directory.Exists(testDirectory))
			{
				try
				{
					Directory.Delete(testDirectory, recursive: true);
				}
				catch
				{
					// Ignore cleanup errors
				}
			}
		}

		#region Content Type Detection Tests

		[TestMethod]
		public void GetContentTypeFromExtension_JsonExtension_ReturnsApplicationJson()
		{
			// Arrange
			string extension = ".json";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/json", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_XmlExtension_ReturnsApplicationXml()
		{
			// Arrange
			string extension = ".xml";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/xml", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_HtmlExtension_ReturnsTextHtml()
		{
			// Arrange
			string extension = ".html";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("text/html", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_HtmExtension_ReturnsTextHtml()
		{
			// Arrange
			string extension = ".htm";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("text/html", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_TxtExtension_ReturnsTextPlain()
		{
			// Arrange
			string extension = ".txt";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("text/plain", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_JpegExtension_ReturnsImageJpeg()
		{
			// Arrange
			string extension = ".jpeg";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("image/jpeg", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_JpgExtension_ReturnsImageJpeg()
		{
			// Arrange
			string extension = ".jpg";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("image/jpeg", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_PngExtension_ReturnsImagePng()
		{
			// Arrange
			string extension = ".png";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("image/png", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_GifExtension_ReturnsImageGif()
		{
			// Arrange
			string extension = ".gif";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("image/gif", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_PdfExtension_ReturnsApplicationPdf()
		{
			// Arrange
			string extension = ".pdf";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/pdf", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_ZipExtension_ReturnsApplicationZip()
		{
			// Arrange
			string extension = ".zip";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/zip", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_CsvExtension_ReturnsTextCsv()
		{
			// Arrange
			string extension = ".csv";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("text/csv", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_UnknownExtension_ReturnsOctetStream()
		{
			// Arrange
			string extension = ".unknown";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/octet-stream", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_ExtensionWithoutDot_ReturnsCorrectType()
		{
			// Arrange
			string extension = "json";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/json", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_MixedCaseExtension_ReturnsCorrectType()
		{
			// Arrange
			string extension = ".JsOn";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/json", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_NullExtension_ReturnsOctetStream()
		{
			// Arrange
			string? extension = null;

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/octet-stream", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_EmptyExtension_ReturnsOctetStream()
		{
			// Arrange
			string extension = string.Empty;

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/octet-stream", contentType);
		}

		[TestMethod]
		public void GetContentTypeFromExtension_WhitespaceExtension_ReturnsOctetStream()
		{
			// Arrange
			string extension = " ";

			// Act
			string contentType = Helpers.GetContentTypeFromExtension(extension);

			// Assert
			Assert.AreEqual("application/octet-stream", contentType);
		}

		#endregion

		#region Time Formatting Tests

		[TestMethod]
		public void FormatTimeLeft_SessionCookie_ReturnsSessionText()
		{
			// Arrange
			DateTime? expires = null;
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeft(expires, now);

			// Assert
			Assert.AreEqual("Session", result);
		}

		[TestMethod]
		public void FormatTimeLeft_ExpiredCookie_ReturnsExpired()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddHours(-1);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeft(expires, now);

			// Assert
			Assert.AreEqual("Expired", result);
		}

		[TestMethod]
		public void FormatTimeLeft_DaysRemaining_ReturnsDaysAndHours()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddDays(7).AddHours(12);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeft(expires, now);

			// Assert
			Assert.IsTrue(result.Contains("d"));
			Assert.IsTrue(result.Contains("h"));
		}

		[TestMethod]
		public void FormatTimeLeft_HoursRemaining_ReturnsHoursAndMinutes()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddHours(5).AddMinutes(30);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeft(expires, now);

			// Assert
			Assert.IsTrue(result.Contains("h"));
			Assert.IsTrue(result.Contains("m"));
		}

		[TestMethod]
		public void FormatTimeLeft_MinutesRemaining_ReturnsMinutesAndSeconds()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddMinutes(45).AddSeconds(30);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeft(expires, now);

			// Assert
			Assert.IsTrue(result.Contains("m"));
			Assert.IsTrue(result.Contains("s"));
		}

		[TestMethod]
		public void FormatTimeLeft_SecondsRemaining_ReturnsSeconds()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddSeconds(30);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeft(expires, now);

			// Assert
			Assert.IsTrue(result.Contains("s"));
			Assert.IsFalse(result.Contains("m"));
			Assert.IsFalse(result.Contains("h"));
			Assert.IsFalse(result.Contains("d"));
		}

		[TestMethod]
		public void FormatTimeLeftLong_ExpiredCookie_ReturnsExpired()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddHours(-1);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeftLong(expires, now);

			// Assert
			Assert.AreEqual("Expired", result);
		}

		[TestMethod]
		public void FormatTimeLeftLong_YearsRemaining_IncludesYears()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddDays(400);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeftLong(expires, now);

			// Assert
			Assert.IsTrue(result.Contains("year"));
		}

		[TestMethod]
		public void FormatTimeLeftLong_PluralYears_UsesPluralForm()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddDays(800);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeftLong(expires, now);

			// Assert
			Assert.IsTrue(result.Contains("years"));
		}

		[TestMethod]
		public void FormatTimeLeftLong_SingularDay_UsesSingularForm()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddDays(1).AddHours(1);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeftLong(expires, now);

			// Assert
			Assert.IsTrue(result.Contains("1 day"));
			Assert.IsFalse(result.Contains("1 days"));
		}

		[TestMethod]
		public void FormatTimeLeftLong_ComplexTimeSpan_FormatsAllComponents()
		{
			// Arrange
			DateTime expires = DateTime.UtcNow.AddDays(400).AddHours(5).AddMinutes(30).AddSeconds(45);
			DateTime now = DateTime.UtcNow;

			// Act
			string result = Helpers.FormatTimeLeftLong(expires, now);

			// Assert
			Assert.IsTrue(result.Contains("year"));
			Assert.IsTrue(result.Contains("day"));
			Assert.IsTrue(result.Contains("hour"));
			Assert.IsTrue(result.Contains("minute"));
			Assert.IsTrue(result.Contains("second"));
			Assert.IsTrue(result.Contains(","));
		}

		#endregion

		#region Cookie Flag Building Tests

		[TestMethod]
		public void BuildCookieFlags_SecureCookie_ReturnsSecureFlag()
		{
			// Arrange
			HttpLibrary.PersistedCookie cookie = new HttpLibrary.PersistedCookie
			{
				Name = "test",
				Value = "value",
				Secure = true,
				HttpOnly = false,
				SameSite = null
			};

			// Act
			string result = Helpers.BuildCookieFlags(cookie);

			// Assert
			Assert.AreEqual("Secure", result);
		}

		[TestMethod]
		public void BuildCookieFlags_HttpOnlyCookie_ReturnsHttpOnlyFlag()
		{
			// Arrange
			HttpLibrary.PersistedCookie cookie = new HttpLibrary.PersistedCookie
			{
				Name = "test",
				Value = "value",
				Secure = false,
				HttpOnly = true,
				SameSite = null
			};

			// Act
			string result = Helpers.BuildCookieFlags(cookie);

			// Assert
			Assert.AreEqual("HttpOnly", result);
		}

		[TestMethod]
		public void BuildCookieFlags_SameSiteCookie_ReturnsSameSiteFlag()
		{
			// Arrange
			HttpLibrary.PersistedCookie cookie = new HttpLibrary.PersistedCookie
			{
				Name = "test",
				Value = "value",
				Secure = false,
				HttpOnly = false,
				SameSite = "Strict"
			};

			// Act
			string result = Helpers.BuildCookieFlags(cookie);

			// Assert
			Assert.AreEqual("SameSite=Strict", result);
		}

		[TestMethod]
		public void BuildCookieFlags_AllFlags_ReturnsAllFlags()
		{
			// Arrange
			HttpLibrary.PersistedCookie cookie = new HttpLibrary.PersistedCookie
			{
				Name = "test",
				Value = "value",
				Secure = true,
				HttpOnly = true,
				SameSite = "Lax"
			};

			// Act
			string result = Helpers.BuildCookieFlags(cookie);

			// Assert
			Assert.IsTrue(result.Contains("Secure"));
			Assert.IsTrue(result.Contains("HttpOnly"));
			Assert.IsTrue(result.Contains("SameSite=Lax"));
		}

		[TestMethod]
		public void BuildCookieFlags_NoFlags_ReturnsEmptyString()
		{
			// Arrange
			HttpLibrary.PersistedCookie cookie = new HttpLibrary.PersistedCookie
			{
				Name = "test",
				Value = "value",
				Secure = false,
				HttpOnly = false,
				SameSite = null
			};

			// Act
			string result = Helpers.BuildCookieFlags(cookie);

			// Assert
			Assert.AreEqual(string.Empty, result);
		}

		#endregion

		#region Format Bytes Tests

		[TestMethod]
		public void FormatBytes_ZeroBytes_ReturnsZeroB()
		{
			// Arrange
			long bytes = 0;

			// Act
			string result = Helpers.FormatBytes(bytes);

			// Assert
			Assert.AreEqual("0 B", result);
		}

		[TestMethod]
		public void FormatBytes_Bytes_ReturnsBytes()
		{
			// Arrange
			long bytes = 512;

			// Act
			string result = Helpers.FormatBytes(bytes);

			// Assert
			Assert.AreEqual("512 B", result);
		}

		[TestMethod]
		public void FormatBytes_Kilobytes_ReturnsKB()
		{
			// Arrange
			long bytes = 1024;

			// Act
			string result = Helpers.FormatBytes(bytes);

			// Assert
			Assert.AreEqual("1 KB", result);
		}

		[TestMethod]
		public void FormatBytes_Megabytes_ReturnsMB()
		{
			// Arrange
			long bytes = 1024 * 1024;

			// Act
			string result = Helpers.FormatBytes(bytes);

			// Assert
			Assert.AreEqual("1 MB", result);
		}

		[TestMethod]
		public void FormatBytes_Gigabytes_ReturnsGB()
		{
			// Arrange
			long bytes = 1024L * 1024L * 1024L;

			// Act
			string result = Helpers.FormatBytes(bytes);

			// Assert
			Assert.AreEqual("1 GB", result);
		}

		[TestMethod]
		public void FormatBytes_Terabytes_ReturnsTB()
		{
			// Arrange
			long bytes = 1024L * 1024L * 1024L * 1024L;

			// Act
			string result = Helpers.FormatBytes(bytes);

			// Assert
			Assert.AreEqual("1 TB", result);
		}

		[TestMethod]
		public void FormatBytes_FractionalKB_ReturnsFormattedValue()
		{
			// Arrange
			long bytes = 1536; // 1.5 KB

			// Act
			string result = Helpers.FormatBytes(bytes);

			// Assert
			Assert.AreEqual("1.5 KB", result);
		}

		[TestMethod]
		public void FormatBytes_LargeNumber_FormatsCorrectly()
		{
			// Arrange
			long bytes = 2500000000; // ~2.33 GB

			// Act
			string result = Helpers.FormatBytes(bytes);

			// Assert
			Assert.IsTrue(result.Contains("GB"));
			Assert.IsTrue(result.Contains("2."));
		}

		#endregion

		#region HTTP Verb Tests

		[TestMethod]
		public void IsHttpVerb_GET_ReturnsTrue()
		{
			// Arrange
			string verb = "GET";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_POST_ReturnsTrue()
		{
			// Arrange
			string verb = "POST";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_PUT_ReturnsTrue()
		{
			// Arrange
			string verb = "PUT";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_DELETE_ReturnsTrue()
		{
			// Arrange
			string verb = "DELETE";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_PATCH_ReturnsTrue()
		{
			// Arrange
			string verb = "PATCH";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_HEAD_ReturnsTrue()
		{
			// Arrange
			string verb = "HEAD";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_OPTIONS_ReturnsTrue()
		{
			// Arrange
			string verb = "OPTIONS";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_TRACE_ReturnsTrue()
		{
			// Arrange
			string verb = "TRACE";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_CONNECT_ReturnsTrue()
		{
			// Arrange
			string verb = "CONNECT";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_CaseInsensitive_ReturnsTrue()
		{
			// Arrange
			string verb = "get";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_MixedCase_ReturnsTrue()
		{
			// Arrange
			string verb = "PoSt";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsHttpVerb_InvalidVerb_ReturnsFalse()
		{
			// Arrange
			string verb = "INVALID";

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsHttpVerb_EmptyString_ReturnsFalse()
		{
			// Arrange
			string verb = string.Empty;

			// Act
			bool result = Helpers.IsHttpVerb(verb);

			// Assert
			Assert.IsFalse(result);
		}

		#endregion

		#region File Upload Tests

		[TestMethod]
		public void CreateHttpContent_InlineBody_ReturnsStringContent()
		{
			// Arrange
			string body = "{\"test\":\"data\"}";

			// Act
			HttpContent content = Helpers.CreateHttpContent(body, "POST");

			// Assert
			Assert.IsInstanceOfType(content, typeof(StringContent));
		}

		[TestMethod]
		public void CreateHttpContent_FileUpload_ReturnsByteArrayContent()
		{
			// Arrange
			string fileParam = $"@{testFilePath}";

			// Act
			HttpContent content = Helpers.CreateHttpContent(fileParam, "POST");

			// Assert
			Assert.IsInstanceOfType(content, typeof(ByteArrayContent));
		}

		[TestMethod]
		public void CreateHttpContent_FileUpload_SetsCorrectContentType()
		{
			// Arrange
			string fileParam = $"@{testFilePath}";

			// Act
			HttpContent content = Helpers.CreateHttpContent(fileParam, "POST");

			// Assert
			Assert.IsNotNull(content.Headers.ContentType);
			Assert.AreEqual("application/json", content.Headers.ContentType.MediaType);
		}

		[TestMethod]
		public void CreateHttpContent_NonExistentFile_ThrowsFileNotFoundException()
		{
			// Arrange
			string fileParam = "@nonexistent.txt";

			// Act & Assert
			try
			{
				HttpContent content = Helpers.CreateHttpContent(fileParam, "POST");
				Assert.Fail("Expected FileNotFoundException was not thrown");
			}
			catch(FileNotFoundException ex)
			{
				Assert.IsTrue(ex.Message.Contains("nonexistent.txt"));
			}
		}

		[TestMethod]
		public void CreateHttpContent_NullBody_ReturnsEmptyStringContent()
		{
			// Arrange
			string? body = null;

			// Act
			HttpContent content = Helpers.CreateHttpContent(body, "POST");

			// Assert
			Assert.IsInstanceOfType(content, typeof(StringContent));
		}

		[TestMethod]
		public void CreateHttpContent_EmptyBody_ReturnsEmptyStringContent()
		{
			// Arrange
			string body = string.Empty;

			// Act
			HttpContent content = Helpers.CreateHttpContent(body, "POST");

			// Assert
			Assert.IsInstanceOfType(content, typeof(StringContent));
		}

		#endregion
	}
}