using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;

namespace HttpLibraryTests
{
	/// <summary>
	/// Tests for atomic file operations including temp file usage, atomic replacement, and concurrent access handling
	/// </summary>
	[TestClass]
	public class AtomicFileOperationsTests
	{
		private string? testDirectory;

		[TestInitialize]
		public void Initialize()
		{
			// Create temporary directory for test files
			testDirectory = Path.Combine(Path.GetTempPath(), $"HttpLibraryAtomicTests_{Guid.NewGuid():N}");
			Directory.CreateDirectory(testDirectory);
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

		#region Temp File Extension Tests

		[TestMethod]
		public void HttpLibraryConfig_TempFileExtension_Is_Tmp()
		{
			// Assert
			Assert.AreEqual(".tmp", Constants.TempFileExtension);
		}

		[TestMethod]
		public void TempFileExtension_StartsWithDot()
		{
			// Arrange
			string extension = Constants.TempFileExtension;

			// Assert
			Assert.IsTrue(extension.StartsWith("."));
		}

		[TestMethod]
		public void TempFileExtension_IsLowerCase()
		{
			// Arrange
			string extension = Constants.TempFileExtension;

			// Assert
			Assert.AreEqual(extension.ToLowerInvariant(), extension);
		}

		#endregion

		#region Temp File Creation Tests

		[TestMethod]
		public void Path_GetTempFileName_CreatesUniqueFiles()
		{
			// Act
			string tempFile1 = Path.GetTempFileName();
			string tempFile2 = Path.GetTempFileName();
			string tempFile3 = Path.GetTempFileName();

			// Assert
			Assert.AreNotEqual(tempFile1, tempFile2);
			Assert.AreNotEqual(tempFile2, tempFile3);
			Assert.AreNotEqual(tempFile1, tempFile3);

			// Cleanup
			File.Delete(tempFile1);
			File.Delete(tempFile2);
			File.Delete(tempFile3);
		}

		[TestMethod]
		public void Path_Combine_WithTempExtension_CreatesValidPath()
		{
			// Arrange
			string directory = testDirectory!;
			string fileName = "download.bin";

			// Act
			string tempPath = Path.Combine(directory, fileName + Constants.TempFileExtension);

			// Assert
			Assert.IsTrue(tempPath.EndsWith(".tmp"));
			Assert.IsTrue(tempPath.Contains(fileName));
		}

		[TestMethod]
		public void TempFile_Creation_InTestDirectory()
		{
			// Arrange
			string tempPath = Path.Combine(testDirectory!, "test.tmp");

			// Act
			File.WriteAllBytes(tempPath, new byte[] { 1, 2, 3 });

			// Assert
			Assert.IsTrue(File.Exists(tempPath));

			// Cleanup
			File.Delete(tempPath);
		}

		#endregion

		#region Atomic Move/Replace Tests

		[TestMethod]
		public void File_Move_RenamesFileAtomically()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "source.tmp");
			string destPath = Path.Combine(testDirectory!, "destination.bin");
			File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4, 5 });

			// Act
			File.Move(sourcePath, destPath);

			// Assert
			Assert.IsFalse(File.Exists(sourcePath), "Source should not exist after move");
			Assert.IsTrue(File.Exists(destPath), "Destination should exist after move");

			byte[] content = File.ReadAllBytes(destPath);
			Assert.AreEqual(5, content.Length);
		}

		[TestMethod]
		public void File_Replace_ReplacesExistingFileAtomically()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "new-data.tmp");
			string destPath = Path.Combine(testDirectory!, "existing-data.bin");

			File.WriteAllBytes(sourcePath, new byte[] { 10, 20, 30 });
			File.WriteAllBytes(destPath, new byte[] { 1, 2, 3, 4, 5 });

			// Act
			File.Replace(sourcePath, destPath, null);

			// Assert
			Assert.IsFalse(File.Exists(sourcePath), "Source should not exist after replace");
			Assert.IsTrue(File.Exists(destPath), "Destination should exist after replace");

			byte[] content = File.ReadAllBytes(destPath);
			Assert.AreEqual(3, content.Length);
			Assert.AreEqual(10, content[ 0 ]);
			Assert.AreEqual(30, content[ 2 ]);
		}

		[TestMethod]
		public void File_Replace_WithBackup_CreatesBackupFile()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "new-data.tmp");
			string destPath = Path.Combine(testDirectory!, "data.bin");
			string backupPath = Path.Combine(testDirectory!, "data.bak");

			File.WriteAllBytes(sourcePath, new byte[] { 10, 20 });
			File.WriteAllBytes(destPath, new byte[] { 1, 2, 3 });

			// Act
			File.Replace(sourcePath, destPath, backupPath);

			// Assert
			Assert.IsFalse(File.Exists(sourcePath));
			Assert.IsTrue(File.Exists(destPath));
			Assert.IsTrue(File.Exists(backupPath), "Backup should be created");

			byte[] backupContent = File.ReadAllBytes(backupPath);
			Assert.AreEqual(3, backupContent.Length);
			Assert.AreEqual(1, backupContent[ 0 ]);
		}

		#endregion

		#region Concurrent Access Tests

		[TestMethod]
		public void File_ExclusiveWrite_BlocksOtherWrites()
		{
			// Arrange
			string filePath = Path.Combine(testDirectory!, "exclusive.bin");
			File.WriteAllBytes(filePath, new byte[ 10 ]);

			using FileStream fs1 = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

			// Act & Assert
			bool exceptionThrown = false;
			try
			{
				FileStream fs2 = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
				fs2.Dispose();
			}
			catch(IOException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "Exclusive access should block other writes");
		}

		[TestMethod]
		public void File_SharedRead_AllowsMultipleReaders()
		{
			// Arrange
			string filePath = Path.Combine(testDirectory!, "shared.bin");
			File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });

			// Act - Open for reading with shared read access
			using FileStream fs1 = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using FileStream fs2 = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

			// Assert - Both streams should be open successfully
			Assert.IsTrue(fs1.CanRead);
			Assert.IsTrue(fs2.CanRead);
		}

		[TestMethod]
		public void File_Delete_OnOpenFile_ThrowsIOException()
		{
			// Arrange
			string filePath = Path.Combine(testDirectory!, "locked.bin");
			File.WriteAllBytes(filePath, new byte[ 10 ]);

			using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

			// Act & Assert
			bool exceptionThrown = false;
			try
			{
				File.Delete(filePath);
			}
			catch(IOException)
			{
				exceptionThrown = true;
			}
			catch(UnauthorizedAccessException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "Cannot delete file while it's open exclusively");
		}

		#endregion

		#region Fallback Mechanism Tests

		[TestMethod]
		public void File_Replace_FallsBackToMove_WhenDestinationMissing()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "source.tmp");
			string destPath = Path.Combine(testDirectory!, "dest-not-exists.bin");

			File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });

			// Act - Replace fails if destination doesn't exist, use Move instead
			if(File.Exists(destPath))
			{
				File.Replace(sourcePath, destPath, null);
			}
			else
			{
				File.Move(sourcePath, destPath);
			}

			// Assert
			Assert.IsFalse(File.Exists(sourcePath));
			Assert.IsTrue(File.Exists(destPath));
		}

		[TestMethod]
		public void File_Exists_CheckBeforeOperation()
		{
			// Arrange
			string filePath = Path.Combine(testDirectory!, "check-exists.bin");

			// Act & Assert - Before creation
			Assert.IsFalse(File.Exists(filePath));

			// Create file
			File.WriteAllBytes(filePath, new byte[ 10 ]);

			// After creation
			Assert.IsTrue(File.Exists(filePath));
		}

		#endregion

		#region Temporary File Pattern Tests

		[TestMethod]
		public void TempFilePattern_DownloadWithTempFile_ThenMove()
		{
			// Simulate download pattern: write to .tmp, then move to final

			// Arrange
			string tempPath = Path.Combine(testDirectory!, "download.bin.tmp");
			string finalPath = Path.Combine(testDirectory!, "download.bin");
			byte[] data = new byte[ 100 ];
			new Random().NextBytes(data);

			// Act - Write to temp file
			File.WriteAllBytes(tempPath, data);
			Assert.IsTrue(File.Exists(tempPath));

			// Move to final location
			File.Move(tempPath, finalPath);

			// Assert
			Assert.IsFalse(File.Exists(tempPath), "Temp file should be gone");
			Assert.IsTrue(File.Exists(finalPath), "Final file should exist");

			byte[] readData = File.ReadAllBytes(finalPath);
			CollectionAssert.AreEqual(data, readData);
		}

		[TestMethod]
		public void TempFilePattern_ReplaceExistingWithTempFile()
		{
			// Simulate update pattern: write to .tmp, then replace existing

			// Arrange
			string tempPath = Path.Combine(testDirectory!, "update.bin.tmp");
			string finalPath = Path.Combine(testDirectory!, "update.bin");

			// Existing file
			File.WriteAllBytes(finalPath, new byte[] { 1, 2, 3 });

			// New data in temp file
			byte[] newData = new byte[] { 10, 20, 30, 40 };
			File.WriteAllBytes(tempPath, newData);

			// Act - Replace existing with temp
			File.Replace(tempPath, finalPath, null);

			// Assert
			Assert.IsFalse(File.Exists(tempPath));
			Assert.IsTrue(File.Exists(finalPath));

			byte[] readData = File.ReadAllBytes(finalPath);
			CollectionAssert.AreEqual(newData, readData);
		}

		#endregion

		#region Error Handling Tests

		[TestMethod]
		public void File_Move_ToExistingFile_ThrowsIOException()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "source.bin");
			string destPath = Path.Combine(testDirectory!, "dest.bin");

			File.WriteAllBytes(sourcePath, new byte[ 10 ]);
			File.WriteAllBytes(destPath, new byte[ 5 ]);

			// Act & Assert
			bool exceptionThrown = false;
			try
			{
				File.Move(sourcePath, destPath); // Will throw because dest exists
			}
			catch(IOException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "Move should fail when destination exists");
		}

		[TestMethod]
		public void File_Replace_NonExistentSource_ThrowsFileNotFoundException()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "does-not-exist.tmp");
			string destPath = Path.Combine(testDirectory!, "dest.bin");

			File.WriteAllBytes(destPath, new byte[ 5 ]);

			// Act & Assert
			bool exceptionThrown = false;
			try
			{
				File.Replace(sourcePath, destPath, null);
			}
			catch(FileNotFoundException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "Replace should fail when source doesn't exist");
		}

		[TestMethod]
		public void File_Replace_NonExistentDestination_ThrowsFileNotFoundException()
		{
			// Arrange
			string sourcePath = Path.Combine(testDirectory!, "source.tmp");
			string destPath = Path.Combine(testDirectory!, "does-not-exist.bin");

			File.WriteAllBytes(sourcePath, new byte[ 5 ]);

			// Act & Assert
			bool exceptionThrown = false;
			try
			{
				File.Replace(sourcePath, destPath, null);
			}
			catch(FileNotFoundException)
			{
				exceptionThrown = true;
			}

			Assert.IsTrue(exceptionThrown, "Replace should fail when destination doesn't exist");
		}

		#endregion

		#region Directory Creation Tests

		[TestMethod]
		public void Directory_CreateDirectory_BeforeFileWrite()
		{
			// Arrange
			string subDir = Path.Combine(testDirectory!, "subdir", "nested");
			string filePath = Path.Combine(subDir, "file.bin");

			// Act - Create directory structure
			Directory.CreateDirectory(subDir);
			File.WriteAllBytes(filePath, new byte[ 10 ]);

			// Assert
			Assert.IsTrue(Directory.Exists(subDir));
			Assert.IsTrue(File.Exists(filePath));
		}

		[TestMethod]
		public void Directory_CreateDirectory_Idempotent()
		{
			// Arrange
			string subDir = Path.Combine(testDirectory!, "idempotent");

			// Act - Create multiple times
			Directory.CreateDirectory(subDir);
			Directory.CreateDirectory(subDir);
			Directory.CreateDirectory(subDir);

			// Assert - No exception, directory exists
			Assert.IsTrue(Directory.Exists(subDir));
		}

		#endregion

		#region Path Manipulation Tests

		[TestMethod]
		public void Path_GetDirectoryName_ReturnsParentDirectory()
		{
			// Arrange
			string filePath = Path.Combine(testDirectory!, "subdir", "file.bin");

			// Act
			string? directory = Path.GetDirectoryName(filePath);

			// Assert
			Assert.IsNotNull(directory);
			Assert.IsTrue(directory.EndsWith("subdir"));
		}

		[TestMethod]
		public void Path_GetFileName_ExtractsFileName()
		{
			// Arrange
			string filePath = Path.Combine(testDirectory!, "download.bin");

			// Act
			string fileName = Path.GetFileName(filePath);

			// Assert
			Assert.AreEqual("download.bin", fileName);
		}

		[TestMethod]
		public void Path_ChangeExtension_ModifiesExtension()
		{
			// Arrange
			string filePath = "download.bin";

			// Act
			string tempPath = Path.ChangeExtension(filePath, ".tmp");

			// Assert
			Assert.AreEqual("download.tmp", tempPath);
		}

		[TestMethod]
		public void Path_Combine_ThreeParts_CreatesCorrectPath()
		{
			// Arrange
			string part1 = testDirectory!;
			string part2 = "subdir";
			string part3 = "file.bin";

			// Act
			string fullPath = Path.Combine(part1, part2, part3);

			// Assert
			Assert.IsTrue(fullPath.Contains("subdir"));
			Assert.IsTrue(fullPath.EndsWith("file.bin"));
		}

		#endregion

		#region Cleanup and Rollback Tests

		[TestMethod]
		public void TempFile_Cleanup_DeletesOrphanedTempFiles()
		{
			// Arrange
			string tempPath = Path.Combine(testDirectory!, "orphaned.tmp");
			File.WriteAllBytes(tempPath, new byte[ 100 ]);

			Assert.IsTrue(File.Exists(tempPath));

			// Act - Cleanup orphaned temp files
			if(File.Exists(tempPath) && tempPath.EndsWith(".tmp"))
			{
				File.Delete(tempPath);
			}

			// Assert
			Assert.IsFalse(File.Exists(tempPath));
		}

		[TestMethod]
		public void TempFile_Rollback_DeletesTempFileOnError()
		{
			// Arrange
			string tempPath = Path.Combine(testDirectory!, "rollback.tmp");
			File.WriteAllBytes(tempPath, new byte[ 50 ]);

			// Simulate error during processing
			bool errorOccurred = true;

			// Act - Rollback on error
			if(errorOccurred && File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}

			// Assert
			Assert.IsFalse(File.Exists(tempPath), "Temp file should be deleted on error");
		}

		#endregion
	}
}