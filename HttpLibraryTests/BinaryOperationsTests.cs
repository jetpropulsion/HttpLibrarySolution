using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for binary operations including file upload/download, stream handling, and progress reporting
	/// </summary>
	[TestClass]
	public class BinaryOperationsTests
	{
		private string? testDirectory;
		private string? testFilePath;
		private const int SmallFileSize = 1024; // 1 KB
		private const int MediumFileSize = 1024 * 100; // 100 KB

		[TestInitialize]
		public void Initialize()
		{
			// Create temporary directory for test files
			testDirectory = Path.Combine(Path.GetTempPath(), $"HttpLibraryTests_{Guid.NewGuid():N}");
			Directory.CreateDirectory(testDirectory);

			// Create a test file
			testFilePath = Path.Combine(testDirectory, "test-file.bin");
			CreateTestFile(testFilePath, SmallFileSize);
		}

		[TestCleanup]
		public void Cleanup()
		{
			// Clean up test directory
			if(testDirectory != null && Directory.Exists(testDirectory))
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

		private void CreateTestFile(string path, int sizeInBytes)
		{
			byte[] data = new byte[ sizeInBytes ];
			Random random = new Random(42); // Seed for reproducibility
			random.NextBytes(data);
			File.WriteAllBytes(path, data);
		}

		#region Configuration Tests

		[TestMethod]
		public void HttpLibraryConfig_StreamBufferSize_IsCorrect()
		{
			// Assert
			Assert.AreEqual(81920, Constants.StreamBufferSize, "Stream buffer size should be 80 KB");
		}

		[TestMethod]
		public void HttpLibraryConfig_DefaultBinaryOperationTimeout_Is5Minutes()
		{
			// Assert
			Assert.AreEqual(TimeSpan.FromSeconds(300), Constants.DefaultBinaryOperationTimeout);
			Assert.AreEqual(TimeSpan.FromMinutes(5), Constants.DefaultBinaryOperationTimeout);
		}

		[TestMethod]
		public void HttpLibraryConfig_MediaTypeOctetStream_IsCorrect()
		{
			// Assert
			Assert.AreEqual("application/octet-stream", Constants.MediaTypeOctetStream);
		}

		#endregion

		#region File Operations Tests

		[TestMethod]
		public void File_Exists_ValidPath_ReturnsTrue()
		{
			// Arrange - testFilePath created in Initialize

			// Assert
			Assert.IsTrue(File.Exists(testFilePath));
		}

		[TestMethod]
		public void File_Exists_InvalidPath_ReturnsFalse()
		{
			// Arrange
			string nonExistentPath = Path.Combine(testDirectory!, "non-existent-file.bin");

			// Assert
			Assert.IsFalse(File.Exists(nonExistentPath));
		}

		[TestMethod]
		public void File_ReadAllBytes_ValidFile_ReturnsCorrectSize()
		{
			// Arrange - testFilePath created with SmallFileSize bytes

			// Act
			byte[] data = File.ReadAllBytes(testFilePath!);

			// Assert
			Assert.AreEqual(SmallFileSize, data.Length);
		}

		[TestMethod]
		public void File_WriteAllBytes_CreatesFile()
		{
			// Arrange
			string newFilePath = Path.Combine(testDirectory!, "new-file.bin");
			byte[] data = new byte[ 100 ];

			// Act
			File.WriteAllBytes(newFilePath, data);

			// Assert
			Assert.IsTrue(File.Exists(newFilePath));
			Assert.AreEqual(100, new FileInfo(newFilePath).Length);
		}

		[TestMethod]
		public void FileInfo_Length_ReturnsCorrectSize()
		{
			// Arrange
			FileInfo fileInfo = new FileInfo(testFilePath!);

			// Act
			long length = fileInfo.Length;

			// Assert
			Assert.AreEqual(SmallFileSize, length);
		}

		[TestMethod]
		public void Directory_CreateDirectory_CreatesNewDirectory()
		{
			// Arrange
			string newDirPath = Path.Combine(testDirectory!, "subdir");

			// Act
			DirectoryInfo dirInfo = Directory.CreateDirectory(newDirPath);

			// Assert
			Assert.IsTrue(Directory.Exists(newDirPath));
			Assert.IsNotNull(dirInfo);
		}

		[TestMethod]
		public void Directory_CreateDirectory_IdempotentCall_DoesNotThrow()
		{
			// Arrange
			string newDirPath = Path.Combine(testDirectory!, "subdir");

			// Act
			Directory.CreateDirectory(newDirPath);
			Directory.CreateDirectory(newDirPath); // Second call

			// Assert - no exception thrown
			Assert.IsTrue(Directory.Exists(newDirPath));
		}

		[TestMethod]
		public void Path_GetDirectoryName_ReturnsParentDirectory()
		{
			// Arrange
			string filePath = Path.Combine(testDirectory!, "subdir", "file.txt");

			// Act
			string? dirName = Path.GetDirectoryName(filePath);

			// Assert
			Assert.IsNotNull(dirName);
			Assert.AreEqual(Path.Combine(testDirectory!, "subdir"), dirName);
		}

		[TestMethod]
		public void Path_Combine_CombinesPathsCorrectly()
		{
			// Arrange
			string part1 = "C:\\temp";
			string part2 = "subdir";
			string part3 = "file.txt";

			// Act
			string combined = Path.Combine(part1, part2, part3);

			// Assert
			Assert.AreEqual("C:\\temp\\subdir\\file.txt", combined);
		}

		#endregion

		#region Stream Operations Tests

		[TestMethod]
		public async Task FileStream_ReadAsync_ReadsData()
		{
			// Arrange
			byte[] buffer = new byte[ 100 ];

			using FileStream fs = new FileStream(testFilePath!, FileMode.Open, FileAccess.Read, FileShare.Read, Constants.StreamBufferSize, useAsync: true);

			// Act
			int bytesRead = await fs.ReadAsync(buffer, CancellationToken.None);

			// Assert
			Assert.AreEqual(100, bytesRead);
		}

		[TestMethod]
		public async Task FileStream_WriteAsync_WritesData()
		{
			// Arrange
			string newFilePath = Path.Combine(testDirectory!, "write-test.bin");
			byte[] data = new byte[ 100 ];
			new Random().NextBytes(data);

			// Act
			using FileStream fs = new FileStream(newFilePath, FileMode.Create, FileAccess.Write, FileShare.None, Constants.StreamBufferSize, useAsync: true);
			await fs.WriteAsync(data, CancellationToken.None);
			await fs.FlushAsync(CancellationToken.None);

			// Assert
			Assert.IsTrue(File.Exists(newFilePath));
			Assert.AreEqual(100, new FileInfo(newFilePath).Length);
		}

		[TestMethod]
		public async Task MemoryStream_ReadWriteAsync_WorksCorrectly()
		{
			// Arrange
			byte[] originalData = new byte[ 100 ];
			new Random().NextBytes(originalData);

			using MemoryStream ms = new MemoryStream();

			// Act - Write
			await ms.WriteAsync(originalData, CancellationToken.None);
			ms.Position = 0;

			// Act - Read
			byte[] readData = new byte[ 100 ];
			int bytesRead = await ms.ReadAsync(readData, CancellationToken.None);

			// Assert
			Assert.AreEqual(100, bytesRead);
			CollectionAssert.AreEqual(originalData, readData);
		}

		[TestMethod]
		public void MemoryStream_CanSeek_ReturnsTrue()
		{
			// Arrange
			using MemoryStream ms = new MemoryStream();

			// Assert
			Assert.IsTrue(ms.CanSeek);
		}

		[TestMethod]
		public void FileStream_CanSeek_ReturnsTrue()
		{
			// Arrange
			using FileStream fs = new FileStream(testFilePath!, FileMode.Open);

			// Assert
			Assert.IsTrue(fs.CanSeek);
		}

		[TestMethod]
		public void Stream_Position_CanBeSetAndRead()
		{
			// Arrange
			using MemoryStream ms = new MemoryStream(new byte[ 100 ]);

			// Act
			ms.Position = 50;

			// Assert
			Assert.AreEqual(50, ms.Position);
		}

		[TestMethod]
		public void Stream_Length_ReturnsCorrectSize()
		{
			// Arrange
			byte[] data = new byte[ 200 ];
			using MemoryStream ms = new MemoryStream(data);

			// Assert
			Assert.AreEqual(200, ms.Length);
		}

		#endregion

		#region ByteArray Operations Tests

		[TestMethod]
		public void ByteArray_ToArray_CreatesNewArray()
		{
			// Arrange
			ReadOnlyMemory<byte> memory = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });

			// Act
			byte[] array = memory.ToArray();

			// Assert
			Assert.AreEqual(5, array.Length);
			Assert.AreEqual(1, array[ 0 ]);
			Assert.AreEqual(5, array[ 4 ]);
		}

		[TestMethod]
		public void ByteArray_AsMemory_CreatesMemory()
		{
			// Arrange
			byte[] array = new byte[] { 10, 20, 30 };

			// Act
			Memory<byte> memory = array.AsMemory();

			// Assert
			Assert.AreEqual(3, memory.Length);
		}

		[TestMethod]
		public void ReadOnlyMemory_Slice_CreatesSubset()
		{
			// Arrange
			ReadOnlyMemory<byte> memory = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 });

			// Act
			ReadOnlyMemory<byte> slice = memory.Slice(1, 3);

			// Assert
			Assert.AreEqual(3, slice.Length);
			byte[] sliceArray = slice.ToArray();
			Assert.AreEqual(2, sliceArray[ 0 ]);
			Assert.AreEqual(4, sliceArray[ 2 ]);
		}

		[TestMethod]
		public void ByteArray_CopyTo_CopiesData()
		{
			// Arrange
			byte[] source = new byte[] { 1, 2, 3 };
			byte[] destination = new byte[ 5 ];

			// Act
			Array.Copy(source, 0, destination, 1, 3);

			// Assert
			Assert.AreEqual(0, destination[ 0 ]);
			Assert.AreEqual(1, destination[ 1 ]);
			Assert.AreEqual(2, destination[ 2 ]);
			Assert.AreEqual(3, destination[ 3 ]);
			Assert.AreEqual(0, destination[ 4 ]);
		}

		#endregion

		#region Content Type Tests

		[TestMethod]
		public void MediaType_OctetStream_IsCorrect()
		{
			// Arrange
			string expectedMediaType = "application/octet-stream";

			// Assert
			Assert.AreEqual(expectedMediaType, Constants.MediaTypeOctetStream);
		}

		[TestMethod]
		public void MediaType_Json_IsCorrect()
		{
			// Assert
			Assert.AreEqual("application/json", Constants.MediaTypeJson);
		}

		[TestMethod]
		public void MediaType_Xml_IsCorrect()
		{
			// Assert
			Assert.AreEqual("application/xml", Constants.MediaTypeXml);
		}

		[TestMethod]
		public void MediaType_PlainText_IsCorrect()
		{
			// Assert
			Assert.AreEqual("text/plain", Constants.MediaTypePlainText);
		}

		[TestMethod]
		public void MediaType_Html_IsCorrect()
		{
			// Assert
			Assert.AreEqual("text/html", Constants.MediaTypeHtml);
		}

		#endregion

		#region Progress Reporting Tests

		[TestMethod]
		public void HttpProgressInfo_Constructor_SetsProperties()
		{
			// Arrange
			string clientName = "testClient";
			string url = "https://example.com/file.zip";

			// Act
			HttpProgressInfo progress = new HttpProgressInfo(clientName, url);

			// Assert
			Assert.AreEqual(clientName, progress.ClientName);
			Assert.AreEqual(url, progress.Url);
		}

		[TestMethod]
		public void ProgressCallback_CanBeNull()
		{
			// Arrange
			Action<HttpProgressInfo>? callback = null;

			// Assert
			Assert.IsNull(callback);
		}

		[TestMethod]
		public void ProgressCallback_CanBeSet()
		{
			// Arrange
			bool callbackInvoked = false;
			Action<HttpProgressInfo> callback = (info) =>
			{
				callbackInvoked = true;
			};

			// Act
			callback(new HttpProgressInfo("test", "https://example.com"));

			// Assert
			Assert.IsTrue(callbackInvoked);
		}

		[TestMethod]
		public void ProgressCallback_ReceivesCorrectInfo()
		{
			// Arrange
			string? receivedClientName = null;
			string? receivedUrl = null;

			Action<HttpProgressInfo> callback = (info) =>
			{
				receivedClientName = info.ClientName;
				receivedUrl = info.Url;
			};

			// Act
			callback(new HttpProgressInfo("myClient", "https://test.com/file"));

			// Assert
			Assert.AreEqual("myClient", receivedClientName);
			Assert.AreEqual("https://test.com/file", receivedUrl);
		}

		#endregion

		#region File Size and Buffer Tests

		[TestMethod]
		public void FileInfo_Length_SmallFile_ReturnsCorrectSize()
		{
			// Arrange
			FileInfo fileInfo = new FileInfo(testFilePath!);

			// Assert
			Assert.AreEqual(SmallFileSize, fileInfo.Length);
		}

		[TestMethod]
		public void CreateLargeFile_MediumSize_CreatesCorrectly()
		{
			// Arrange
			string largePath = Path.Combine(testDirectory!, "medium-file.bin");
			CreateTestFile(largePath, MediumFileSize);

			// Act
			FileInfo fileInfo = new FileInfo(largePath);

			// Assert
			Assert.AreEqual(MediumFileSize, fileInfo.Length);
		}

		[TestMethod]
		public void StreamBuffer_DefaultSize_Matches80KB()
		{
			// Arrange
			int expectedSize = 81920; // 80 KB

			// Assert
			Assert.AreEqual(expectedSize, Constants.StreamBufferSize);
		}

		[TestMethod]
		public void Buffer_Array_CanBeCreatedAtBufferSize()
		{
			// Arrange & Act
			byte[] buffer = new byte[ Constants.StreamBufferSize ];

			// Assert
			Assert.AreEqual(Constants.StreamBufferSize, buffer.Length);
		}

		#endregion

		#region Temporary File Tests

		[TestMethod]
		public void TempFileExtension_IsCorrect()
		{
			// Assert
			Assert.AreEqual(".tmp", Constants.TempFileExtension);
		}

		[TestMethod]
		public void Path_GetTempPath_ReturnsValidPath()
		{
			// Act
			string tempPath = Path.GetTempPath();

			// Assert
			Assert.IsNotNull(tempPath);
			Assert.IsTrue(Directory.Exists(tempPath));
		}

		[TestMethod]
		public void Path_GetTempFileName_CreatesUniqueFile()
		{
			// Act
			string tempFile1 = Path.GetTempFileName();
			string tempFile2 = Path.GetTempFileName();

			// Assert
			Assert.AreNotEqual(tempFile1, tempFile2);
			Assert.IsTrue(File.Exists(tempFile1));
			Assert.IsTrue(File.Exists(tempFile2));

			// Cleanup
			File.Delete(tempFile1);
			File.Delete(tempFile2);
		}

		[TestMethod]
		public void File_Move_RenamesFile()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "source.bin");
			string destPath = Path.Combine(testDirectory!, "destination.bin");
			File.WriteAllBytes(sourcePath, new byte[ 100 ]);

			// Act
			File.Move(sourcePath, destPath);

			// Assert
			Assert.IsFalse(File.Exists(sourcePath));
			Assert.IsTrue(File.Exists(destPath));
		}

		[TestMethod]
		public void File_Replace_ReplacesExistingFile()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "source.bin");
			string destPath = Path.Combine(testDirectory!, "dest.bin");

			File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });
			File.WriteAllBytes(destPath, new byte[] { 4, 5, 6 });

			// Act
			File.Replace(sourcePath, destPath, null);

			// Assert
			Assert.IsFalse(File.Exists(sourcePath));
			Assert.IsTrue(File.Exists(destPath));

			byte[] newContent = File.ReadAllBytes(destPath);
			Assert.AreEqual(3, newContent.Length);
			Assert.AreEqual(1, newContent[ 0 ]);
		}

		#endregion

		#region Exception Handling Tests

		[TestMethod]
		public void FileNotFoundException_ThrownForNonExistentFile()
		{
			// Arrange
			string nonExistentPath = Path.Combine(testDirectory!, "does-not-exist.bin");

			// Act & Assert
			bool exceptionThrown = false;
			try
			{
				FileStream fs = new FileStream(nonExistentPath, FileMode.Open);
				fs.Dispose();
			}
			catch(FileNotFoundException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "FileNotFoundException should be thrown");
		}

		[TestMethod]
		public void DirectoryNotFoundException_ThrownForInvalidPath()
		{
			// Arrange
			string invalidPath = Path.Combine(testDirectory!, "non-existent-dir", "file.bin");

			// Act & Assert
			bool exceptionThrown = false;
			try
			{
				FileStream fs = new FileStream(invalidPath, FileMode.Create);
				fs.Dispose();
			}
			catch(DirectoryNotFoundException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "DirectoryNotFoundException should be thrown");
		}

		[TestMethod]
		public void IOException_ThrownWhenAccessingLockedFile()
		{
			// Arrange
			string lockedPath = Path.Combine(testDirectory!, "locked.bin");
			File.WriteAllBytes(lockedPath, new byte[ 10 ]);

			using FileStream fs1 = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

			// Act & Assert
			bool exceptionThrown = false;
			try
			{
				FileStream fs2 = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
				fs2.Dispose();
			}
			catch(IOException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "IOException should be thrown for locked file");
		}

		#endregion

		#region Encoding and Character Set Tests

		[TestMethod]
		public void Encoding_Utf8_IsDefault()
		{
			// Assert
			Assert.AreEqual(System.Text.Encoding.UTF8, Constants.DefaultTextEncoding);
		}

		[TestMethod]
		public void CharsetUtf8_StringIsCorrect()
		{
			// Assert
			Assert.AreEqual("utf-8", Constants.CharsetUtf8);
		}

		#endregion
	}
}