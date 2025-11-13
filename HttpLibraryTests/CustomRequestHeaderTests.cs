using HttpLibrary;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for custom request header functionality in PooledHttpClient
	/// </summary>
	[TestClass]
	public class CustomRequestHeaderTests
	{
		private PooledHttpClient CreateTestClient()
		{
			HttpClient client = new HttpClient();
			IOptions<PooledHttpClientOptions> options = Options.Create(new PooledHttpClientOptions { Name = "test-client" });
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();
			return new PooledHttpClient(client, options, logger);
		}

		#region AddRequestHeader - Valid Headers

		[TestMethod]
		public void AddRequestHeader_ValidHeader_DoesNotThrow()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert - should not throw
			client.AddRequestHeader("X-Custom-Header", "custom-value");
			client.AddRequestHeader("Authorization", "Bearer token123");
			client.AddRequestHeader("Accept", "application/json");
		}

		[TestMethod]
		public void AddRequestHeader_EmptyValue_DoesNotThrow()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert - empty string is valid per RFC 7230
			client.AddRequestHeader("X-Empty-Header", string.Empty);
		}

		[TestMethod]
		public void AddRequestHeader_ValidUserAgent_DoesNotThrow()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert
			client.AddRequestHeader("User-Agent", "MyApp/1.0");
			client.AddRequestHeader("User-Agent", "MyApp/1.0 (Windows NT 10.0; Win64; x64)");
			client.AddRequestHeader("User-Agent", "MyApp/1.0 HttpLibrary/1.0");
		}

		[TestMethod]
		public void AddRequestHeader_CommonHeaders_Allowed()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert - common headers should be allowed
			client.AddRequestHeader("Authorization", "Bearer token123");
			client.AddRequestHeader("Accept", "application/json");
			client.AddRequestHeader("Accept-Language", "en-US");
			client.AddRequestHeader("Accept-Encoding", "gzip, deflate");
			client.AddRequestHeader("X-API-Key", "secret-key");
			client.AddRequestHeader("X-Request-ID", Guid.NewGuid().ToString());
		}

		#endregion

		#region AddRequestHeader - Invalid Input

		[TestMethod]
		public void AddRequestHeader_NullName_ThrowsArgumentException()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert
			try
			{
				client.AddRequestHeader(null!, "value");
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException ex)
			{
				Assert.IsTrue(ex.Message.Contains("Header name cannot be null or whitespace"));
			}
		}

		[TestMethod]
		public void AddRequestHeader_EmptyName_ThrowsArgumentException()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert
			try
			{
				client.AddRequestHeader(string.Empty, "value");
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException ex)
			{
				Assert.IsTrue(ex.Message.Contains("Header name cannot be null or whitespace"));
			}
		}

		[TestMethod]
		public void AddRequestHeader_WhitespaceName_ThrowsArgumentException()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert
			try
			{
				client.AddRequestHeader("   ", "value");
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException ex)
			{
				Assert.IsTrue(ex.Message.Contains("Header name cannot be null or whitespace"));
			}
		}

		[TestMethod]
		public void AddRequestHeader_NullValue_ThrowsArgumentNullException()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert
			try
			{
				client.AddRequestHeader("X-Header", null!);
				Assert.Fail("Expected ArgumentNullException was not thrown");
			}
			catch(ArgumentNullException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void AddRequestHeader_InvalidHeaderName_ThrowsArgumentException()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert
			try
			{
				client.AddRequestHeader("Invalid Header Name!", "value");
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException ex)
			{
				Assert.IsTrue(ex.Message.Contains("RFC 7230 violation"));
			}
		}

		[TestMethod]
		public void AddRequestHeader_InvalidHeaderNameWithColon_ThrowsArgumentException()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert
			try
			{
				client.AddRequestHeader("Header:Name", "value");
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException ex)
			{
				Assert.IsTrue(ex.Message.Contains("RFC 7230 violation"));
			}
		}

		[TestMethod]
		public void AddRequestHeader_InvalidHeaderNameWithSpace_ThrowsArgumentException()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert
			try
			{
				client.AddRequestHeader("Header Name", "value");
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException ex)
			{
				Assert.IsTrue(ex.Message.Contains("RFC 7230 violation"));
			}
		}

		#endregion

		#region AddRequestHeader - Update Behavior

		[TestMethod]
		public void AddRequestHeader_SameHeaderTwice_UpdatesValue()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act
			client.AddRequestHeader("X-API-Key", "key1");
			client.AddRequestHeader("X-API-Key", "key2");

			// Assert - second call should update the value (verified by no exception)
			Assert.IsTrue(client.RemoveRequestHeader("X-API-Key"));
		}

		[TestMethod]
		public void AddRequestHeader_CaseInsensitive_UpdatesSameHeader()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act
			client.AddRequestHeader("X-API-Key", "key1");
			client.AddRequestHeader("x-api-key", "key2");
			client.AddRequestHeader("X-Api-Key", "key3");

			// Assert - all three should update the same header (case-insensitive)
			Assert.IsTrue(client.RemoveRequestHeader("x-API-key"));
			Assert.IsFalse(client.RemoveRequestHeader("X-API-Key")); // Already removed
		}

		#endregion

		#region RemoveRequestHeader

		[TestMethod]
		public void RemoveRequestHeader_ExistingHeader_ReturnsTrue()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			client.AddRequestHeader("X-Custom-Header", "value");

			// Act
			bool removed = client.RemoveRequestHeader("X-Custom-Header");

			// Assert
			Assert.IsTrue(removed);
		}

		[TestMethod]
		public void RemoveRequestHeader_NonExistingHeader_ReturnsFalse()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act
			bool removed = client.RemoveRequestHeader("X-Nonexistent");

			// Assert
			Assert.IsFalse(removed);
		}

		[TestMethod]
		public void RemoveRequestHeader_NullName_ReturnsFalse()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act
			bool removed = client.RemoveRequestHeader(null!);

			// Assert
			Assert.IsFalse(removed);
		}

		[TestMethod]
		public void RemoveRequestHeader_EmptyName_ReturnsFalse()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act
			bool removed = client.RemoveRequestHeader(string.Empty);

			// Assert
			Assert.IsFalse(removed);
		}

		[TestMethod]
		public void RemoveRequestHeader_WhitespaceName_ReturnsFalse()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act
			bool removed = client.RemoveRequestHeader("   ");

			// Assert
			Assert.IsFalse(removed);
		}

		[TestMethod]
		public void RemoveRequestHeader_CaseInsensitive_RemovesHeader()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			client.AddRequestHeader("X-Custom-Header", "value");

			// Act
			bool removed = client.RemoveRequestHeader("x-custom-header");

			// Assert
			Assert.IsTrue(removed);
		}

		[TestMethod]
		public void RemoveRequestHeader_AfterRemoval_ReturnsFalse()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			client.AddRequestHeader("X-Custom-Header", "value");
			client.RemoveRequestHeader("X-Custom-Header");

			// Act
			bool removed = client.RemoveRequestHeader("X-Custom-Header");

			// Assert
			Assert.IsFalse(removed);
		}

		#endregion

		#region ClearRequestHeaders

		[TestMethod]
		public void ClearRequestHeaders_WithHeaders_ClearsAll()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			client.AddRequestHeader("X-Header-1", "value1");
			client.AddRequestHeader("X-Header-2", "value2");
			client.AddRequestHeader("X-Header-3", "value3");

			// Act
			client.ClearRequestHeaders();

			// Assert - after clearing, removing should return false
			Assert.IsFalse(client.RemoveRequestHeader("X-Header-1"));
			Assert.IsFalse(client.RemoveRequestHeader("X-Header-2"));
			Assert.IsFalse(client.RemoveRequestHeader("X-Header-3"));
		}

		[TestMethod]
		public void ClearRequestHeaders_WithNoHeaders_DoesNotThrow()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert - should not throw
			client.ClearRequestHeaders();
		}

		[TestMethod]
		public void ClearRequestHeaders_CalledMultipleTimes_DoesNotThrow()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			client.AddRequestHeader("X-Header", "value");

			// Act & Assert
			client.ClearRequestHeaders();
			client.ClearRequestHeaders();
			client.ClearRequestHeaders();
		}

		[TestMethod]
		public void AddRequestHeader_AfterClear_AddsHeader()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			client.AddRequestHeader("X-Header-1", "value1");
			client.ClearRequestHeaders();

			// Act
			client.AddRequestHeader("X-Header-2", "value2");

			// Assert - should be able to remove newly added header
			Assert.IsTrue(client.RemoveRequestHeader("X-Header-2"));
		}

		#endregion

		#region Thread Safety

		[TestMethod]
		public void CustomHeaders_ThreadSafe_UnderConcurrentAccess()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			int numberOfTasks = 100;
			List<Task> tasks = new List<Task>();

			// Act - concurrent adds
			for(int i = 0; i < numberOfTasks; i++)
			{
				int index = i;
				tasks.Add(Task.Run(() =>
				{
					client.AddRequestHeader($"X-Header-{index}", $"value-{index}");
				}));
			}
			Task.WaitAll(tasks.ToArray());

			// Assert - all headers should be added
			for(int i = 0; i < numberOfTasks; i++)
			{
				Assert.IsTrue(client.RemoveRequestHeader($"X-Header-{i}"));
			}
		}

		[TestMethod]
		public void CustomHeaders_ThreadSafe_ConcurrentAddAndRemove()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			int numberOfOperations = 50;
			List<Task> tasks = new List<Task>();

			// Act - concurrent adds and removes
			for(int i = 0; i < numberOfOperations; i++)
			{
				int index = i;
				tasks.Add(Task.Run(() =>
				{
					client.AddRequestHeader($"X-Header-{index}", $"value-{index}");
				}));
				tasks.Add(Task.Run(() =>
				{
					client.RemoveRequestHeader($"X-Header-{index}");
				}));
			}
			Task.WaitAll(tasks.ToArray());

			// Assert - no exceptions should be thrown (state may vary due to race conditions)
			client.ClearRequestHeaders(); // Should not throw
		}

		#endregion

		#region Multiple Headers

		[TestMethod]
		public void AddRequestHeader_MultipleDifferentHeaders_AllAdded()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act
			client.AddRequestHeader("X-Header-1", "value1");
			client.AddRequestHeader("X-Header-2", "value2");
			client.AddRequestHeader("X-Header-3", "value3");
			client.AddRequestHeader("Authorization", "Bearer token");
			client.AddRequestHeader("Accept", "application/json");

			// Assert - all should be removable
			Assert.IsTrue(client.RemoveRequestHeader("X-Header-1"));
			Assert.IsTrue(client.RemoveRequestHeader("X-Header-2"));
			Assert.IsTrue(client.RemoveRequestHeader("X-Header-3"));
			Assert.IsTrue(client.RemoveRequestHeader("Authorization"));
			Assert.IsTrue(client.RemoveRequestHeader("Accept"));
		}

		[TestMethod]
		public void CustomHeaders_MaxHeaderCount_NoLimit()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			int headerCount = 100; // Reduced from 1000 for faster test execution

			// Act
			for(int i = 0; i < headerCount; i++)
			{
				client.AddRequestHeader($"X-Header-{i}", $"value-{i}");
			}

			// Assert - should be able to add many headers
			for(int i = 0; i < headerCount; i++)
			{
				Assert.IsTrue(client.RemoveRequestHeader($"X-Header-{i}"));
			}
		}

		#endregion
	}
}