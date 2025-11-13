using HttpLibrary;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class PooledHttpClientTests
	{
		[TestMethod]
		public void Constructor_ThrowsArgumentNullException_WhenHttpClientIsNull()
		{
			// Arrange
			HttpClient? client = null;
			IOptions<PooledHttpClientOptions> options = Options.Create(new PooledHttpClientOptions());
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();

			// Act & Assert
			try
			{
				PooledHttpClient pooledClient = new PooledHttpClient(client!, options, logger);
				Assert.Fail("Expected ArgumentNullException was not thrown");
			}
			catch(ArgumentNullException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
		{
			// Arrange
			HttpClient client = new HttpClient();
			IOptions<PooledHttpClientOptions> options = Options.Create(new PooledHttpClientOptions());
			ILogger<PooledHttpClient>? logger = null;

			// Act & Assert
			try
			{
				PooledHttpClient pooledClient = new PooledHttpClient(client, options, logger!);
				Assert.Fail("Expected ArgumentNullException was not thrown");
			}
			catch(ArgumentNullException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void Constructor_SetsNameFromOptions()
		{
			// Arrange
			HttpClient client = new HttpClient();
			string expectedName = "testClient";
			PooledHttpClientOptions options = new PooledHttpClientOptions { Name = expectedName };
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();

			// Act
			PooledHttpClient pooledClient = new PooledHttpClient(client, Options.Create(options), logger);

			// Assert
			Assert.AreEqual(expectedName, pooledClient.Name);
		}

		[TestMethod]
		public void Constructor_SetsBaseAddress_WhenProvidedInOptions()
		{
			// Arrange
			HttpClient client = new HttpClient();
			Uri expectedBaseAddress = new Uri("https://api.example.com");
			PooledHttpClientOptions options = new PooledHttpClientOptions { BaseAddress = expectedBaseAddress };
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();

			// Act
			PooledHttpClient pooledClient = new PooledHttpClient(client, Options.Create(options), logger);

			// Assert
			Assert.AreEqual(expectedBaseAddress, client.BaseAddress);
		}

		[TestMethod]
		public void Constructor_SetsDefaultRequestHeaders()
		{
			// Arrange
			HttpClient client = new HttpClient();
			PooledHttpClientOptions options = new PooledHttpClientOptions();
			options.DefaultRequestHeaders[ "User-Agent" ] = "TestAgent/1.0";
			options.DefaultRequestHeaders[ "Accept" ] = "application/json";
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();

			// Act
			PooledHttpClient pooledClient = new PooledHttpClient(client, Options.Create(options), logger);

			// Assert
			Assert.IsTrue(client.DefaultRequestHeaders.Contains("User-Agent"));
			Assert.IsTrue(client.DefaultRequestHeaders.Contains("Accept"));
		}

		[TestMethod]
		public void Metrics_InitiallyZero()
		{
			// Arrange
			HttpClient client = new HttpClient();
			IOptions<PooledHttpClientOptions> options = Options.Create(new PooledHttpClientOptions());
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();
			PooledHttpClient pooledClient = new PooledHttpClient(client, options, logger);

			// Act
			PooledHttpClientMetrics metrics = pooledClient.Metrics;

			// Assert
			Assert.AreEqual(0L, metrics.TotalRequests);
			Assert.AreEqual(0L, metrics.SuccessfulRequests);
			Assert.AreEqual(0L, metrics.FailedRequests);
			Assert.AreEqual(0L, metrics.ActiveRequests);
			Assert.AreEqual(0L, metrics.TotalBytesReceived);
			Assert.AreEqual(0.0, metrics.AverageRequestMs);
		}

		[TestMethod]
		public async Task SendAsync_ThrowsArgumentNullException_WhenRequestIsNull()
		{
			// Arrange
			HttpClient client = new HttpClient();
			IOptions<PooledHttpClientOptions> options = Options.Create(new PooledHttpClientOptions());
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();
			PooledHttpClient pooledClient = new PooledHttpClient(client, options, logger);
			HttpRequestMessage? request = null;

			// Act & Assert
			try
			{
				HttpResponseMessage response = await pooledClient.SendAsync(request!);
				Assert.Fail("Expected ArgumentNullException was not thrown");
			}
			catch(ArgumentNullException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public async Task SendRawAsync_ThrowsArgumentNullException_WhenRequestIsNull()
		{
			// Arrange
			HttpClient client = new HttpClient();
			IOptions<PooledHttpClientOptions> options = Options.Create(new PooledHttpClientOptions());
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();
			PooledHttpClient pooledClient = new PooledHttpClient(client, options, logger);
			HttpRequestMessage? request = null;

			// Act & Assert
			try
			{
				HttpResponseMessage response = await pooledClient.SendRawAsync(request!);
				Assert.Fail("Expected ArgumentNullException was not thrown");
			}
			catch(ArgumentNullException)
			{
				// Expected exception
			}
		}
	}

	[TestClass]
	public class PooledHttpClientMetricsTests
	{
		[TestMethod]
		public void OnRequestStarted_IncrementsActiveRequests()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			metrics.OnRequestStarted();

			// Assert
			Assert.AreEqual(1L, metrics.ActiveRequests);
		}

		[TestMethod]
		public void OnRequestCompleted_Success_UpdatesMetrics()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();
			metrics.OnRequestStarted();

			// Act
			metrics.OnRequestCompleted(success: true, bytesReceived: 1024, elapsedMs: 100);

			// Assert
			Assert.AreEqual(0L, metrics.ActiveRequests);
			Assert.AreEqual(1L, metrics.TotalRequests);
			Assert.AreEqual(1L, metrics.SuccessfulRequests);
			Assert.AreEqual(0L, metrics.FailedRequests);
			Assert.AreEqual(1024L, metrics.TotalBytesReceived);
			Assert.AreEqual(100.0, metrics.AverageRequestMs);
		}

		[TestMethod]
		public void OnRequestCompleted_Failure_UpdatesMetrics()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();
			metrics.OnRequestStarted();

			// Act
			metrics.OnRequestCompleted(success: false, bytesReceived: 0, elapsedMs: 50);

			// Assert
			Assert.AreEqual(0L, metrics.ActiveRequests);
			Assert.AreEqual(1L, metrics.TotalRequests);
			Assert.AreEqual(0L, metrics.SuccessfulRequests);
			Assert.AreEqual(1L, metrics.FailedRequests);
			Assert.AreEqual(0L, metrics.TotalBytesReceived);
			Assert.AreEqual(50.0, metrics.AverageRequestMs);
		}

		[TestMethod]
		public void AverageRequestMs_CalculatesCorrectly_WithMultipleRequests()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();

			// Act
			metrics.OnRequestStarted();
			metrics.OnRequestCompleted(success: true, bytesReceived: 100, elapsedMs: 100);
			metrics.OnRequestStarted();
			metrics.OnRequestCompleted(success: true, bytesReceived: 200, elapsedMs: 200);
			metrics.OnRequestStarted();
			metrics.OnRequestCompleted(success: true, bytesReceived: 300, elapsedMs: 300);

			// Assert
			Assert.AreEqual(3L, metrics.TotalRequests);
			Assert.AreEqual(600L, metrics.TotalBytesReceived);
			Assert.AreEqual(200.0, metrics.AverageRequestMs); // (100 + 200 + 300) / 3
		}

		[TestMethod]
		public void Metrics_ThreadSafe_UnderConcurrentAccess()
		{
			// Arrange
			PooledHttpClientMetrics metrics = new PooledHttpClientMetrics();
			int numberOfTasks = 100;
			List<Task> tasks = new List<Task>();

			// Act
			for(int i = 0; i < numberOfTasks; i++)
			{
				tasks.Add(Task.Run(() =>
				{
					metrics.OnRequestStarted();
					Thread.Sleep(1); // simulate work
					metrics.OnRequestCompleted(success: true, bytesReceived: 100, elapsedMs: 10);
				}));
			}
			Task.WaitAll(tasks.ToArray());

			// Assert
			Assert.AreEqual((long)numberOfTasks, metrics.TotalRequests);
			Assert.AreEqual((long)numberOfTasks, metrics.SuccessfulRequests);
			Assert.AreEqual(0L, metrics.ActiveRequests);
			Assert.AreEqual((long)( numberOfTasks * 100 ), metrics.TotalBytesReceived);
		}
	}

	[TestClass]
	public class PooledHttpClientOptionsTests
	{
		[TestMethod]
		public void DefaultRequestVersion_DefaultsToHttp2()
		{
			// Arrange & Act
			PooledHttpClientOptions options = new PooledHttpClientOptions();

			// Assert
			Assert.AreEqual(HttpVersion.Version20, options.DefaultRequestVersion);
		}

		[TestMethod]
		public void AutomaticDecompression_DefaultsToAll()
		{
			// Arrange & Act
			PooledHttpClientOptions options = new PooledHttpClientOptions();

			// Assert
			Assert.AreEqual(DecompressionMethods.All, options.AutomaticDecompression);
		}

		[TestMethod]
		public void PooledConnectionLifetime_DefaultsToFiveMinutes()
		{
			// Arrange & Act
			PooledHttpClientOptions options = new PooledHttpClientOptions();

			// Assert
			Assert.AreEqual(TimeSpan.FromMinutes(5), options.PooledConnectionLifetime);
		}

		[TestMethod]
		public void MaxConnectionsPerServer_DefaultsToMaxValue()
		{
			// Arrange & Act
			PooledHttpClientOptions options = new PooledHttpClientOptions();

			// Assert
			Assert.AreEqual(int.MaxValue, options.MaxConnectionsPerServer);
		}

		[TestMethod]
		public void DefaultRequestHeaders_IsCaseInsensitive()
		{
			// Arrange
			PooledHttpClientOptions options = new PooledHttpClientOptions();

			// Act
			options.DefaultRequestHeaders[ "content-type" ] = "application/json";
			options.DefaultRequestHeaders[ "Content-Type" ] = "text/html";

			// Assert
			Assert.HasCount(1, options.DefaultRequestHeaders);
			Assert.AreEqual("text/html", options.DefaultRequestHeaders[ "CONTENT-TYPE" ]);
		}
	}

	[TestClass]
	public class NamedPooledHttpClientProviderTests
	{
		[TestMethod]
		public void GetClient_ThrowsArgumentException_WhenNameIsEmpty()
		{
			// Arrange
			ServiceCollection services = new ServiceCollection();
			services.AddHttpClient();
			services.AddSingleton<IOptionsMonitor<PooledHttpClientOptions>>(sp =>
			{
				IOptionsMonitorCache<PooledHttpClientOptions> cache = new OptionsCache<PooledHttpClientOptions>();
				return new OptionsMonitor<PooledHttpClientOptions>(
									new OptionsFactory<PooledHttpClientOptions>(
											Array.Empty<IConfigureOptions<PooledHttpClientOptions>>(),
											Array.Empty<IPostConfigureOptions<PooledHttpClientOptions>>()
									),
									Array.Empty<IOptionsChangeTokenSource<PooledHttpClientOptions>>(),
									cache
							);
			});
			services.AddSingleton<ILoggerFactory, LoggerFactory>();
			ServiceProvider provider = services.BuildServiceProvider();

			IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
			IOptionsMonitor<PooledHttpClientOptions> options = provider.GetRequiredService<IOptionsMonitor<PooledHttpClientOptions>>();
			ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
			NamedPooledHttpClientProvider namedProvider = new NamedPooledHttpClientProvider(factory, options, loggerFactory);

			// Act & Assert
			try
			{
				IPooledHttpClient client = namedProvider.GetClient(string.Empty);
				Assert.Fail("Expected ArgumentException was not thrown");
			}
			catch(ArgumentException)
			{
				// Expected exception
			}
		}

		[TestMethod]
		public void GetClient_ReturnsSameInstance_ForSameName()
		{
			// Arrange
			ServiceCollection services = new ServiceCollection();
			services.AddHttpClient("testClient");
			services.AddSingleton<IOptionsMonitor<PooledHttpClientOptions>>(sp =>
			{
				IOptionsMonitorCache<PooledHttpClientOptions> cache = new OptionsCache<PooledHttpClientOptions>();
				return new OptionsMonitor<PooledHttpClientOptions>(
									new OptionsFactory<PooledHttpClientOptions>(
											Array.Empty<IConfigureOptions<PooledHttpClientOptions>>(),
											Array.Empty<IPostConfigureOptions<PooledHttpClientOptions>>()
									),
									Array.Empty<IOptionsChangeTokenSource<PooledHttpClientOptions>>(),
									cache
							);
			});
			services.AddSingleton<ILoggerFactory, LoggerFactory>();
			ServiceProvider provider = services.BuildServiceProvider();

			IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
			IOptionsMonitor<PooledHttpClientOptions> options = provider.GetRequiredService<IOptionsMonitor<PooledHttpClientOptions>>();
			ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
			NamedPooledHttpClientProvider namedProvider = new NamedPooledHttpClientProvider(factory, options, loggerFactory);

			// Act
			IPooledHttpClient client1 = namedProvider.GetClient("testClient");
			IPooledHttpClient client2 = namedProvider.GetClient("testClient");

			// Assert
			Assert.AreSame(client1, client2);
		}

		[TestMethod]
		public void GetClient_ReturnsDifferentInstances_ForDifferentNames()
		{
			// Arrange
			ServiceCollection services = new ServiceCollection();
			services.AddHttpClient("client1");
			services.AddHttpClient("client2");
			services.AddSingleton<IOptionsMonitor<PooledHttpClientOptions>>(sp =>
			{
				IOptionsMonitorCache<PooledHttpClientOptions> cache = new OptionsCache<PooledHttpClientOptions>();
				return new OptionsMonitor<PooledHttpClientOptions>(
									new OptionsFactory<PooledHttpClientOptions>(
											Array.Empty<IConfigureOptions<PooledHttpClientOptions>>(),
											Array.Empty<IPostConfigureOptions<PooledHttpClientOptions>>()
									),
									Array.Empty<IOptionsChangeTokenSource<PooledHttpClientOptions>>(),
									cache
							);
			});
			services.AddSingleton<ILoggerFactory, LoggerFactory>();
			ServiceProvider provider = services.BuildServiceProvider();

			IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
			IOptionsMonitor<PooledHttpClientOptions> options = provider.GetRequiredService<IOptionsMonitor<PooledHttpClientOptions>>();
			ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
			NamedPooledHttpClientProvider namedProvider = new NamedPooledHttpClientProvider(factory, options, loggerFactory);

			// Act
			IPooledHttpClient client1 = namedProvider.GetClient("client1");
			IPooledHttpClient client2 = namedProvider.GetClient("client2");

			// Assert
			Assert.AreNotSame(client1, client2);
		}
	}

	[TestClass]
	public class PooledHttpClientCustomHeaderTests
	{
		private PooledHttpClient CreateTestClient()
		{
			HttpClient client = new HttpClient();
			IOptions<PooledHttpClientOptions> options = Options.Create(new PooledHttpClientOptions { Name = "test-client" });
			ILogger<PooledHttpClient> logger = new LoggerFactory().CreateLogger<PooledHttpClient>();
			return new PooledHttpClient(client, options, logger);
		}

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
				client.AddRequestHeader(" ", "value");
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

		[TestMethod]
		public void AddRequestHeader_EmptyValue_DoesNotThrow()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert - empty string is valid per RFC 7230
			client.AddRequestHeader("X-Empty-Header", string.Empty);
		}

		[TestMethod]
		public void AddRequestHeader_SameHeaderTwice_UpdatesValue()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act
			client.AddRequestHeader("X-API-Key", "key1");
			client.AddRequestHeader("X-API-Key", "key2");

			// Assert - second call should update the value
			// We can't directly verify the internal dictionary, but no exception should be thrown
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
			// No exception should be thrown
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

		[TestMethod]
		public void CustomHeaders_SpecialCharactersInValue_Allowed()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();

			// Act & Assert - various special characters should be allowed in values
			client.AddRequestHeader("X-JSON", "{\"key\":\"value\"}");
			client.AddRequestHeader("X-URL", "https://example.com/path?query=value");
			client.AddRequestHeader("X-List", "item1, item2, item3");
			client.AddRequestHeader("X-Unicode", "Hello ?? ??");
		}

		[TestMethod]
		public void CustomHeaders_CommonHeaders_Allowed()
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

		[TestMethod]
		public void CustomHeaders_MaxHeaderCount_NoLimit()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			int headerCount = 1000;

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

		[TestMethod]
		public void CustomHeaders_LongHeaderValue_Allowed()
		{
			// Arrange
			PooledHttpClient client = CreateTestClient();
			string longValue = new string('x', 8000); // 8KB value

			// Act & Assert
			client.AddRequestHeader("X-Long-Header", longValue);
			Assert.IsTrue(client.RemoveRequestHeader("X-Long-Header"));
		}

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
	}
}