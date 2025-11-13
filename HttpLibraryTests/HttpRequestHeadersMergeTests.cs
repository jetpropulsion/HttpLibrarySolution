using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for HttpRequestHeaders merge and clone operations
	/// </summary>
	[TestClass]
	public class HttpRequestHeadersMergeTests
	{
		[TestMethod]
		public void Merge_WithOverwriteFalse_PreservesExistingHeaders()
		{
			// Arrange
			HttpRequestHeaders headers1 = new HttpRequestHeaders();
			headers1.Add("X-Header-1", "value1");
			headers1.Add("X-Header-2", "value2");

			HttpRequestHeaders headers2 = new HttpRequestHeaders();
			headers2.Add("X-Header-2", "value2-updated");
			headers2.Add("X-Header-3", "value3");

			// Act
			int mergedCount = headers1.Merge(headers2, overwrite: false);

			// Assert
			Assert.AreEqual(1, mergedCount); // Only X-Header-3 was added
			Assert.AreEqual("value1", headers1[ "X-Header-1" ]);
			Assert.AreEqual("value2", headers1[ "X-Header-2" ]); // Preserved
			Assert.AreEqual("value3", headers1[ "X-Header-3" ]);
		}

		[TestMethod]
		public void Merge_WithOverwriteTrue_ReplacesExistingHeaders()
		{
			// Arrange
			HttpRequestHeaders headers1 = new HttpRequestHeaders();
			headers1.Add("X-Header-1", "value1");
			headers1.Add("X-Header-2", "value2");

			HttpRequestHeaders headers2 = new HttpRequestHeaders();
			headers2.Add("X-Header-2", "value2-updated");
			headers2.Add("X-Header-3", "value3");

			// Act
			int mergedCount = headers1.Merge(headers2, overwrite: true);

			// Assert
			Assert.AreEqual(2, mergedCount); // Both headers merged
			Assert.AreEqual("value1", headers1[ "X-Header-1" ]);
			Assert.AreEqual("value2-updated", headers1[ "X-Header-2" ]); // Updated
			Assert.AreEqual("value3", headers1[ "X-Header-3" ]);
		}

		[TestMethod]
		public void Merge_WithEmptySource_ReturnsZero()
		{
			// Arrange
			HttpRequestHeaders headers1 = new HttpRequestHeaders();
			headers1.Add("X-Header-1", "value1");

			HttpRequestHeaders headers2 = new HttpRequestHeaders();

			// Act
			int mergedCount = headers1.Merge(headers2);

			// Assert
			Assert.AreEqual(0, mergedCount);
			Assert.AreEqual(1, headers1.Count);
		}

		[TestMethod]
		public void Merge_IntoEmptyHeaders_AddsAllHeaders()
		{
			// Arrange
			HttpRequestHeaders headers1 = new HttpRequestHeaders();

			HttpRequestHeaders headers2 = new HttpRequestHeaders();
			headers2.Add("X-Header-1", "value1");
			headers2.Add("X-Header-2", "value2");
			headers2.Add("X-Header-3", "value3");

			// Act
			int mergedCount = headers1.Merge(headers2);

			// Assert
			Assert.AreEqual(3, mergedCount);
			Assert.AreEqual(3, headers1.Count);
			Assert.AreEqual("value1", headers1[ "X-Header-1" ]);
			Assert.AreEqual("value2", headers1[ "X-Header-2" ]);
			Assert.AreEqual("value3", headers1[ "X-Header-3" ]);
		}

		[TestMethod]
		public void Merge_NullSource_ThrowsArgumentNullException()
		{
			// Arrange
			HttpRequestHeaders headers = new HttpRequestHeaders();

			// Act & Assert
			try
			{
				headers.Merge((HttpRequestHeaders)null!);
				Assert.Fail("Expected ArgumentNullException was not thrown");
			}
			catch(ArgumentNullException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void Merge_WithDictionary_MergesHeaders()
		{
			// Arrange
			HttpRequestHeaders headers = new HttpRequestHeaders();
			headers.Add("X-Header-1", "value1");

			Dictionary<string, string> dict = new Dictionary<string, string>
						{
								{ "X-Header-2", "value2" },
								{ "X-Header-3", "value3" }
						};

			// Act
			int mergedCount = headers.Merge(dict);

			// Assert
			Assert.AreEqual(2, mergedCount);
			Assert.AreEqual(3, headers.Count);
			Assert.AreEqual("value1", headers[ "X-Header-1" ]);
			Assert.AreEqual("value2", headers[ "X-Header-2" ]);
			Assert.AreEqual("value3", headers[ "X-Header-3" ]);
		}

		[TestMethod]
		public void Merge_WithDictionary_OverwriteTrue_ReplacesHeaders()
		{
			// Arrange
			HttpRequestHeaders headers = new HttpRequestHeaders();
			headers.Add("X-Header-1", "value1");

			Dictionary<string, string> dict = new Dictionary<string, string>
						{
								{ "X-Header-1", "value1-updated" },
								{ "X-Header-2", "value2" }
						};

			// Act
			int mergedCount = headers.Merge(dict, overwrite: true);

			// Assert
			Assert.AreEqual(2, mergedCount);
			Assert.AreEqual("value1-updated", headers[ "X-Header-1" ]);
			Assert.AreEqual("value2", headers[ "X-Header-2" ]);
		}

		[TestMethod]
		public void Merge_WithDictionary_NullSource_ThrowsArgumentNullException()
		{
			// Arrange
			HttpRequestHeaders headers = new HttpRequestHeaders();

			// Act & Assert
			try
			{
				headers.Merge((IDictionary<string, string>)null!);
				Assert.Fail("Expected ArgumentNullException was not thrown");
			}
			catch(ArgumentNullException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void Merge_CaseInsensitive_MergesCorrectly()
		{
			// Arrange
			HttpRequestHeaders headers1 = new HttpRequestHeaders();
			headers1.Add("X-Header", "value1");

			HttpRequestHeaders headers2 = new HttpRequestHeaders();
			headers2.Add("x-header", "value2");

			// Act
			int mergedCount = headers1.Merge(headers2, overwrite: false);

			// Assert
			Assert.AreEqual(0, mergedCount); // No merge because same header (case-insensitive)
			Assert.AreEqual("value1", headers1[ "X-Header" ]); // Preserved
		}

		[TestMethod]
		public void Clone_CreatesIdenticalCopy()
		{
			// Arrange
			HttpRequestHeaders original = new HttpRequestHeaders();
			original.Add("X-Header-1", "value1");
			original.Add("X-Header-2", "value2");
			original.Add("Authorization", "Bearer token");

			// Act
			HttpRequestHeaders cloned = original.Clone();

			// Assert
			Assert.AreEqual(original.Count, cloned.Count);
			Assert.AreEqual("value1", cloned[ "X-Header-1" ]);
			Assert.AreEqual("value2", cloned[ "X-Header-2" ]);
			Assert.AreEqual("Bearer token", cloned[ "Authorization" ]);
		}

		[TestMethod]
		public void Clone_CreatesIndependentCopy()
		{
			// Arrange
			HttpRequestHeaders original = new HttpRequestHeaders();
			original.Add("X-Header-1", "value1");

			// Act
			HttpRequestHeaders cloned = original.Clone();
			cloned.Add("X-Header-2", "value2");

			// Assert
			Assert.AreEqual(1, original.Count); // Original unchanged
			Assert.AreEqual(2, cloned.Count);
			Assert.IsFalse(original.Contains("X-Header-2"));
			Assert.IsTrue(cloned.Contains("X-Header-2"));
		}

		[TestMethod]
		public void Clone_EmptyHeaders_CreatesEmptyClone()
		{
			// Arrange
			HttpRequestHeaders original = new HttpRequestHeaders();

			// Act
			HttpRequestHeaders cloned = original.Clone();

			// Assert
			Assert.AreEqual(0, original.Count);
			Assert.AreEqual(0, cloned.Count);
		}

		[TestMethod]
		public void Clone_CustomHeadersContainer_PreservesContainerType()
		{
			// Arrange
			HttpRequestHeaders original = HttpRequestHeaders.CreateCustomHeadersContainer();
			original.AddWithValidation("X-API-Key", "secret");

			// Act
			HttpRequestHeaders cloned = original.Clone();

			// Assert
			Assert.AreEqual(1, cloned.Count);
			Assert.AreEqual("secret", cloned[ "X-API-Key" ]);
		}

		[TestMethod]
		public void Names_ReturnsAllHeaderNames()
		{
			// Arrange
			HttpRequestHeaders headers = new HttpRequestHeaders();
			headers.Add("X-Header-1", "value1");
			headers.Add("X-Header-2", "value2");
			headers.Add("Authorization", "Bearer token");

			// Act
			List<string> names = new List<string>(headers.Names);

			// Assert
			Assert.AreEqual(3, names.Count);
			Assert.IsTrue(names.Contains("X-Header-1"));
			Assert.IsTrue(names.Contains("X-Header-2"));
			Assert.IsTrue(names.Contains("Authorization"));
		}

		[TestMethod]
		public void Values_ReturnsAllHeaderValues()
		{
			// Arrange
			HttpRequestHeaders headers = new HttpRequestHeaders();
			headers.Add("X-Header-1", "value1");
			headers.Add("X-Header-2", "value2");
			headers.Add("Authorization", "Bearer token");

			// Act
			List<string> values = new List<string>(headers.Values);

			// Assert
			Assert.AreEqual(3, values.Count);
			Assert.IsTrue(values.Contains("value1"));
			Assert.IsTrue(values.Contains("value2"));
			Assert.IsTrue(values.Contains("Bearer token"));
		}
	}
}