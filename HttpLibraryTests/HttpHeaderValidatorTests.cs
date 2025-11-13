using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;
using System.Linq;

namespace HttpLibraryTests
{
	[TestClass]
	public class HttpHeaderValidatorTests
	{
		#region Header Name Validation Tests

		[TestMethod]
		public void IsValidHeaderName_WithValidName_ReturnsTrue()
		{
			// Arrange
			string validName = "Content-Type";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(validName);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithAllValidTokenChars_ReturnsTrue()
		{
			// RFC 7230: Valid token characters
			string validName = "Valid-Header_Name.123!#$%&'*+^`|~";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(validName);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithEmptyString_ReturnsFalse()
		{
			// Arrange
			string emptyName = "";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(emptyName);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithNull_ReturnsFalse()
		{
			// Arrange
			string? nullName = null;

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nullName!);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithWhitespace_ReturnsFalse()
		{
			// Arrange
			string nameWithSpace = "Content Type";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nameWithSpace);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithControlCharacters_ReturnsFalse()
		{
			// Arrange
			string nameWithControl = "Content\x01Type";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nameWithControl);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithDelimiterParenthesis_ReturnsFalse()
		{
			// RFC 7230: Delimiters not allowed
			string nameWithDelimiter = "Content(Type)";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nameWithDelimiter);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithDelimiterColon_ReturnsFalse()
		{
			// Arrange
			string nameWithColon = "Content:Type";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nameWithColon);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithDelimiterSemicolon_ReturnsFalse()
		{
			// Arrange
			string nameWithSemicolon = "Content;Type";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nameWithSemicolon);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithDelimiterComma_ReturnsFalse()
		{
			// Arrange
			string nameWithComma = "Content,Type";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nameWithComma);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithDelimiterQuote_ReturnsFalse()
		{
			// Arrange
			string nameWithQuote = "Content\"Type";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nameWithQuote);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderName_WithUnicodeCharacters_ReturnsFalse()
		{
			// RFC 7230: Only ASCII allowed
			string nameWithUnicode = "Content-?ype";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderName(nameWithUnicode);

			// Assert
			Assert.IsFalse(result);
		}

		#endregion

		#region Header Value Validation Tests

		[TestMethod]
		public void IsValidHeaderValue_WithValidValue_ReturnsTrue()
		{
			// Arrange
			string validValue = "application/json";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(validValue);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithEmptyString_ReturnsTrue()
		{
			// RFC 7230: Empty values are allowed
			string emptyValue = "";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(emptyValue);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithNull_ReturnsFalse()
		{
			// Arrange
			string? nullValue = null;

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(nullValue!);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithSpace_ReturnsTrue()
		{
			// RFC 7230: SP allowed
			string valueWithSpace = "text/html; charset=utf-8";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(valueWithSpace);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithTab_ReturnsTrue()
		{
			// RFC 7230: HTAB allowed
			string valueWithTab = "text/html;\tcharset=utf-8";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(valueWithTab);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithLeadingTrailingWhitespace_ReturnsTrue()
		{
			// Arrange
			string valueWithWhitespace = "  application/json  ";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(valueWithWhitespace);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithObsoleteText_ReturnsTrue()
		{
			// RFC 7230: obs-text (0x80-0xFF) allowed
			string valueWithObsText = "value\x80\xFF";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(valueWithObsText);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithControlCharNullByte_ReturnsFalse()
		{
			// Arrange
			string valueWithNull = "value\x00";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(valueWithNull);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithControlCharNewline_ReturnsFalse()
		{
			// Arrange
			string valueWithNewline = "value\n";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(valueWithNewline);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithControlCharCarriageReturn_ReturnsFalse()
		{
			// Arrange
			string valueWithCR = "value\r";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(valueWithCR);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidHeaderValue_WithVisibleAscii_ReturnsTrue()
		{
			// RFC 7230: VCHAR (0x21-0x7E)
			string visibleAscii = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

			// Act
			bool result = HttpHeaderValidator.IsValidHeaderValue(visibleAscii);

			// Assert
			Assert.IsTrue(result);
		}

		#endregion

		#region User-Agent Validation Tests

		[TestMethod]
		public void IsValidUserAgent_WithValidAgent_ReturnsTrue()
		{
			// Arrange
			string validUA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

			// Act
			bool result = HttpHeaderValidator.IsValidUserAgent(validUA);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidUserAgent_WithEmptyString_ReturnsFalse()
		{
			// Arrange
			string emptyUA = "";

			// Act
			bool result = HttpHeaderValidator.IsValidUserAgent(emptyUA);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidUserAgent_WithNull_ReturnsFalse()
		{
			// Arrange
			string? nullUA = null;

			// Act
			bool result = HttpHeaderValidator.IsValidUserAgent(nullUA!);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidUserAgent_WithTab_ReturnsTrue()
		{
			// RFC 7231: HTAB allowed
			string uaWithTab = "Mozilla/5.0\t(Windows)";

			// Act
			bool result = HttpHeaderValidator.IsValidUserAgent(uaWithTab);

			// Assert
			Assert.IsTrue(result);
		}

		[TestMethod]
		public void IsValidUserAgent_WithNewline_ReturnsFalse()
		{
			// Control characters (except TAB) not allowed
			string uaWithNewline = "Mozilla/5.0\n(Windows)";

			// Act
			bool result = HttpHeaderValidator.IsValidUserAgent(uaWithNewline);

			// Assert
			Assert.IsFalse(result);
		}

		[TestMethod]
		public void IsValidUserAgent_WithCarriageReturn_ReturnsFalse()
		{
			// Arrange
			string uaWithCR = "Mozilla/5.0\r(Windows)";

			// Act
			bool result = HttpHeaderValidator.IsValidUserAgent(uaWithCR);

			// Assert
			Assert.IsFalse(result);
		}

		#endregion

		#region User-Agent Parsing Tests

		[TestMethod]
		public void ParseUserAgent_WithSimpleProduct_ReturnsSingleToken()
		{
			// Arrange
			string ua = "Mozilla/5.0";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("Mozilla", tokens[ 0 ].Product);
			Assert.AreEqual("5.0", tokens[ 0 ].Version);
		}

		[TestMethod]
		public void ParseUserAgent_WithProductNoVersion_ReturnsTokenWithoutVersion()
		{
			// Arrange
			string ua = "curl";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("curl", tokens[ 0 ].Product);
			Assert.IsNull(tokens[ 0 ].Version);
		}

		[TestMethod]
		public void ParseUserAgent_WithMultipleProducts_ReturnsAllTokens()
		{
			// Arrange
			string ua = "Mozilla/5.0 Chrome/96.0 Safari/537.36";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.AreEqual(3, tokens.Count);
			Assert.AreEqual("Mozilla", tokens[ 0 ].Product);
			Assert.AreEqual("5.0", tokens[ 0 ].Version);
			Assert.AreEqual("Chrome", tokens[ 1 ].Product);
			Assert.AreEqual("96.0", tokens[ 1 ].Version);
			Assert.AreEqual("Safari", tokens[ 2 ].Product);
			Assert.AreEqual("537.36", tokens[ 2 ].Version);
		}

		[TestMethod]
		public void ParseUserAgent_WithComment_ReturnsCommentToken()
		{
			// Arrange
			string ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.AreEqual(2, tokens.Count);
			Assert.AreEqual("Mozilla", tokens[ 0 ].Product);
			Assert.IsNotNull(tokens[ 1 ].Comment);
			Assert.AreEqual("(Windows NT 10.0; Win64; x64)", tokens[ 1 ].Comment);
		}

		[TestMethod]
		public void ParseUserAgent_WithNestedComments_HandlesDepthCorrectly()
		{
			// Arrange
			string ua = "Product/1.0 (Comment (nested) here)";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.AreEqual(2, tokens.Count);
			Assert.AreEqual("Product", tokens[ 0 ].Product);
			Assert.IsNotNull(tokens[ 1 ].Comment);
			Assert.AreEqual("(Comment (nested) here)", tokens[ 1 ].Comment);
		}

		[TestMethod]
		public void ParseUserAgent_WithMixedProductsAndComments_PreservesOrder()
		{
			// Arrange
			string ua = "Mozilla/5.0 (Windows) Chrome/96.0 (KHTML)";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.AreEqual(4, tokens.Count);
			Assert.AreEqual("Mozilla", tokens[ 0 ].Product);
			Assert.IsNotNull(tokens[ 1 ].Comment);
			Assert.AreEqual("Chrome", tokens[ 2 ].Product);
			Assert.IsNotNull(tokens[ 3 ].Comment);
		}

		[TestMethod]
		public void ParseUserAgent_WithUnbalancedOpenParenthesis_HandlesGracefully()
		{
			// Arrange - unbalanced (
			string ua = "Mozilla/5.0 (Windows";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert - should return product token only, comment incomplete
			Assert.AreEqual(1, tokens.Count);
			Assert.AreEqual("Mozilla", tokens[ 0 ].Product);
		}

		[TestMethod]
		public void ParseUserAgent_WithEmptyString_ReturnsEmptyList()
		{
			// Arrange
			string ua = "";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.AreEqual(0, tokens.Count);
		}

		[TestMethod]
		public void ParseUserAgent_WithNull_ReturnsEmptyList()
		{
			// Arrange
			string? ua = null;

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua!);

			// Assert
			Assert.AreEqual(0, tokens.Count);
		}

		[TestMethod]
		public void ParseUserAgent_WithOnlyWhitespace_ReturnsEmptyList()
		{
			// Arrange
			string ua = "   \t   ";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.AreEqual(0, tokens.Count);
		}

		[TestMethod]
		public void ParseUserAgent_RealWorldExample_ParsesCorrectly()
		{
			// Arrange - Real Firefox UA
			string ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/115.0";

			// Act
			List<UserAgentToken> tokens = HttpHeaderValidator.ParseUserAgent(ua);

			// Assert
			Assert.IsTrue(tokens.Count >= 3); // At least Mozilla, comment, and Gecko/Firefox
			Assert.AreEqual("Mozilla", tokens[ 0 ].Product);
			Assert.AreEqual("5.0", tokens[ 0 ].Version);

			// Should contain comment
			UserAgentToken? commentToken = tokens.FirstOrDefault(t => t.Comment != null);
			Assert.IsNotNull(commentToken);

			// Should contain Gecko
			UserAgentToken? geckoToken = tokens.FirstOrDefault(t => t.Product == "Gecko");
			Assert.IsNotNull(geckoToken);
		}

		#endregion
	}
}