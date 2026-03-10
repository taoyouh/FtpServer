// <copyright file="SimpleFileProviderTests.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using Zhaobang.FtpServer.File;

namespace Zhaobang.FtpServer.Tests.File
{
    /// <summary>
    /// Tests for <see cref="SimpleFileProvider"/>.
    /// </summary>
    [TestClass]
    public sealed class SimpleFileProviderTests : IDisposable
    {
        private readonly TestContext testContext;
        private readonly string testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleFileProviderTests"/> class.
        /// </summary>
        /// <param name="testContext">The context of test operation used to cancel the test.</param>
        public SimpleFileProviderTests(TestContext testContext)
        {
            ArgumentNullException.ThrowIfNull(testContext);
            this.testContext = testContext;
            Directory.CreateDirectory(this.testDirectory);
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetItemAsync"/> returns file info correctly.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetItemAsyncFileReturnsFileInfo()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string fileName = "testfile.txt";
            string filePath = Path.Combine(this.testDirectory, fileName);
            string content = "test content";
            await System.IO.File.WriteAllTextAsync(filePath, content, this.testContext.CancellationToken);

            FileSystemEntry entry = await provider.GetItemAsync(fileName);

            Assert.IsNotNull(entry);
            Assert.IsFalse(entry.IsDirectory);
            Assert.AreEqual(fileName, entry.Name);
            Assert.AreEqual(content.Length, entry.Length);
            Assert.IsFalse(entry.IsReadOnly);
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetItemAsync"/> returns directory info correctly.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetItemAsyncDirectoryReturnsDirectoryInfo()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string dirName = "testdir";
            string dirPath = Path.Combine(this.testDirectory, dirName);
            Directory.CreateDirectory(dirPath);

            FileSystemEntry entry = await provider.GetItemAsync(dirName);

            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.IsDirectory);
            Assert.AreEqual(dirName, entry.Name);
            Assert.AreEqual(0, entry.Length);
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetItemAsync"/> throws exception when path doesn't exist.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetItemAsyncNotExistsThrowsFileNoAccessException()
        {
            var provider = new SimpleFileProvider(this.testDirectory);

            await Assert.ThrowsAsync<FileNoAccessException>(
                () => provider.GetItemAsync("nonexistent"));
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetItemAsync"/> handles absolute paths.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetItemAsyncAbsolutePathReturnsFileInfo()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string fileName = "testfile.txt";
            string filePath = Path.Combine(this.testDirectory, fileName);
            await System.IO.File.WriteAllTextAsync(filePath, "test content", this.testContext.CancellationToken);

            FileSystemEntry entry = await provider.GetItemAsync($"/{fileName}");

            Assert.IsNotNull(entry);
            Assert.AreEqual(fileName, entry.Name);
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetItemAsync"/> throws exception when accessing parent directory.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetItemAsyncParentDirectoryThrowsUnauthorizedAccessException()
        {
            var provider = new SimpleFileProvider(this.testDirectory);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => provider.GetItemAsync(".."));
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetChildItems"/> returns child entries correctly.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetChildItemsReturnsAllChildren()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string dirName = "testdir";
            string dirPath = Path.Combine(this.testDirectory, dirName);
            Directory.CreateDirectory(dirPath);

            string file1 = Path.Combine(dirPath, "file1.txt");
            string file2 = Path.Combine(dirPath, "file2.txt");
            string subdir = Path.Combine(dirPath, "subdir");

            await System.IO.File.WriteAllTextAsync(file1, "content1", this.testContext.CancellationToken);
            await System.IO.File.WriteAllTextAsync(file2, "content2", this.testContext.CancellationToken);
            Directory.CreateDirectory(subdir);

            IEnumerable<FileSystemEntry> children = await provider.GetChildItems(dirName);

            Assert.HasCount(3, children);
            Assert.IsTrue(children.Any(c => c.Name == "file1.txt" && !c.IsDirectory));
            Assert.IsTrue(children.Any(c => c.Name == "file2.txt" && !c.IsDirectory));
            Assert.IsTrue(children.Any(c => c.Name == "subdir" && c.IsDirectory));
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetChildItems"/> returns empty collection for empty directory.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetChildItemsEmptyDirectoryReturnsEmptyCollection()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string dirName = "emptydir";
            string dirPath = Path.Combine(this.testDirectory, dirName);
            Directory.CreateDirectory(dirPath);

            IEnumerable<FileSystemEntry> children = await provider.GetChildItems(dirName);

            Assert.IsEmpty(children);
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetChildItems"/> throws exception when path doesn't exist.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetChildItemsNotExistsThrowsFileNoAccessException()
        {
            var provider = new SimpleFileProvider(this.testDirectory);

            await Assert.ThrowsAsync<FileNoAccessException>(
                () => provider.GetChildItems("nonexistent"));
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetChildItems"/> throws exception when path points to a file.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetChildItemsFilePathThrowsFileNoAccessException()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string fileName = "testfile.txt";
            string filePath = Path.Combine(this.testDirectory, fileName);
            System.IO.File.WriteAllText(filePath, "test content");

            await Assert.ThrowsAsync<ArgumentException>(
                () => provider.GetChildItems(fileName));
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetChildItems"/> handles absolute paths.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetChildItemsAbsolutePathReturnsChildren()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string dirName = "testdir";
            string dirPath = Path.Combine(this.testDirectory, dirName);
            Directory.CreateDirectory(dirPath);

            string file1 = Path.Combine(dirPath, "file1.txt");
            await System.IO.File.WriteAllTextAsync(file1, "content1", this.testContext.CancellationToken);

            List<FileSystemEntry> children = [.. await provider.GetChildItems($"/{dirName}")];

            Assert.HasCount(1, children);
            Assert.AreEqual("file1.txt", children[0].Name);
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetChildItems"/> throws exception when accessing parent directory.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetChildItemsParentDirectoryThrowsUnauthorizedAccessException()
        {
            var provider = new SimpleFileProvider(this.testDirectory);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => provider.GetChildItems(".."));
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetItemAsync"/> returns UTC time for file.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetItemAsyncFileReturnsUtcTime()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string fileName = "testfile.txt";
            string filePath = Path.Combine(this.testDirectory, fileName);
            await System.IO.File.WriteAllTextAsync(filePath, "test content", this.testContext.CancellationToken);

            FileSystemEntry entry = await provider.GetItemAsync(fileName);
            var fileInfo = new FileInfo(filePath);

            Assert.AreEqual(fileInfo.LastWriteTimeUtc, entry.LastWriteTime);
            Assert.AreEqual(DateTimeKind.Utc, entry.LastWriteTime.Kind);
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetItemAsync"/> returns UTC time for directory.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetItemAsyncDirectoryReturnsUtcTime()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string dirName = "testdir";
            string dirPath = Path.Combine(this.testDirectory, dirName);
            Directory.CreateDirectory(dirPath);

            FileSystemEntry entry = await provider.GetItemAsync(dirName);
            var dirInfo = new DirectoryInfo(dirPath);

            Assert.AreEqual(dirInfo.LastWriteTimeUtc, entry.LastWriteTime);
            Assert.AreEqual(DateTimeKind.Utc, entry.LastWriteTime.Kind);
        }

        /// <summary>
        /// Tests that <see cref="SimpleFileProvider.GetChildItems"/> returns UTC time for children.
        /// </summary>
        /// <returns>The task representing the async operation.</returns>
        [TestMethod]
        public async Task GetChildItemsReturnsUtcTime()
        {
            var provider = new SimpleFileProvider(this.testDirectory);
            string dirName = "testdir";
            string dirPath = Path.Combine(this.testDirectory, dirName);
            Directory.CreateDirectory(dirPath);

            string file1 = Path.Combine(dirPath, "file1.txt");
            string subdir = Path.Combine(dirPath, "subdir");

            await System.IO.File.WriteAllTextAsync(file1, "content1", this.testContext.CancellationToken);
            Directory.CreateDirectory(subdir);

            IEnumerable<FileSystemEntry> children = await provider.GetChildItems(dirName);
            IEnumerable<FileSystemEntry> childrenList = children;

            var fileEntry = childrenList.FirstOrDefault(c => c.Name == "file1.txt");
            var dirEntry = childrenList.FirstOrDefault(c => c.Name == "subdir");

            Assert.IsNotNull(fileEntry);
            Assert.IsNotNull(dirEntry);

            var fileInfo = new FileInfo(file1);
            var dirInfo = new DirectoryInfo(subdir);

            Assert.AreEqual(fileInfo.LastWriteTimeUtc, fileEntry.LastWriteTime);
            Assert.AreEqual(DateTimeKind.Utc, fileEntry.LastWriteTime.Kind);
            Assert.AreEqual(dirInfo.LastWriteTimeUtc, dirEntry.LastWriteTime);
            Assert.AreEqual(DateTimeKind.Utc, dirEntry.LastWriteTime.Kind);
        }

        /// <summary>
        /// Cleans up test resources.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(this.testDirectory))
            {
                try
                {
                    Directory.Delete(this.testDirectory, true);
                }
                catch
                {
                }
            }
        }
    }
}
