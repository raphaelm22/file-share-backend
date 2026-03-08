using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Features.Files.ListFiles;

namespace FileShare.Tests.Features.Files.ListFiles;

public sealed class ListFilesEndpointTests
{
    [Fact]
    public void Handle_EmptyDirectory_ReturnsEmptyArray()
    {
        WithTempDir(tempDir =>
        {
            // Arrange
            var config = BuildConfig(tempDir);

            // Act
            var result = ListFilesEndpoint.Handle(new ListFilesQuery(), config, NullLoggerFactory.Instance);

            // Assert
            Assert.Empty(result);
        });
    }

    [Fact]
    public void Handle_DirectoryWithFiles_ReturnsFilesMetadata()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            File.WriteAllText(Path.Combine(tempDir, "alpha.txt"), "hello");
            File.WriteAllText(Path.Combine(tempDir, "beta.pdf"), "world");
            var config = BuildConfig(tempDir);

            // Act
            var result = ListFilesEndpoint.Handle(new ListFilesQuery(), config, NullLoggerFactory.Instance);

            // Assert
            Assert.Equal(2, result.Length);
            Assert.Contains(result, r => r.FileName == "alpha.txt" && r.FileSize == 5);
            Assert.Contains(result, r => r.FileName == "beta.pdf" && r.FileSize == 5);
            Assert.All(result, r => Assert.NotEqual(default, r.ModifiedAt));
            Assert.All(result, r => Assert.Equal(TimeSpan.Zero, r.ModifiedAt.Offset));
            Assert.All(result, r => Assert.StartsWith(tempDir, r.FilePath));
            Assert.All(result, r => Assert.Equal("", r.Directory));
        });
    }

    [Fact]
    public void Handle_FileInSubdirectory_ReturnsCorrectDirectory()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            var backupsDir = Directory.CreateDirectory(Path.Combine(tempDir, "backups"));
            File.WriteAllText(Path.Combine(tempDir, "root_file.txt"), "");
            File.WriteAllText(Path.Combine(backupsDir.FullName, "backup.tar"), "");
            var config = BuildConfig(tempDir);

            // Act
            var result = ListFilesEndpoint.Handle(new ListFilesQuery(), config, NullLoggerFactory.Instance);

            // Assert
            var rootFile = result.Single(f => f.FileName == "root_file.txt");
            Assert.Equal("", rootFile.Directory);

            var backupFile = result.Single(f => f.FileName == "backup.tar");
            Assert.Equal("backups", backupFile.Directory);
        });
    }

    [Fact]
    public void Handle_FileInNestedSubdirectory_ReturnsFullRelativePath()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            var nestedDir = Directory.CreateDirectory(Path.Combine(tempDir, "backups", "2025"));
            File.WriteAllText(Path.Combine(nestedDir.FullName, "archive.zip"), "");
            var config = BuildConfig(tempDir);

            // Act
            var result = ListFilesEndpoint.Handle(new ListFilesQuery(), config, NullLoggerFactory.Instance);

            // Assert
            var archiveFile = result.Single(f => f.FileName == "archive.zip");
            Assert.Equal("backups/2025", archiveFile.Directory);
        });
    }

    [Fact]
    public void Handle_DirectoryNotFound_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), "non-existent-" + Guid.NewGuid());
        var config = BuildConfig(nonExistentDir);

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() =>
            ListFilesEndpoint.Handle(new ListFilesQuery(), config, NullLoggerFactory.Instance));
    }

    [Fact]
    public void Handle_DirectoryWithHiddenFiles_FiltersHiddenFiles()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            File.WriteAllText(Path.Combine(tempDir, "visible.txt"), "content");
            File.WriteAllText(Path.Combine(tempDir, ".gitkeep"), "");
            File.WriteAllText(Path.Combine(tempDir, ".hidden"), "secret");
            var config = BuildConfig(tempDir);

            // Act
            var result = ListFilesEndpoint.Handle(new ListFilesQuery(), config, NullLoggerFactory.Instance);

            // Assert
            Assert.Single(result);
            Assert.Equal("visible.txt", result[0].FileName);
            Assert.Equal("", result[0].Directory);
        });
    }

    [Fact]
    public void Handle_FileInHiddenDirectory_FiltersFile()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            var hiddenDir = Directory.CreateDirectory(Path.Combine(tempDir, ".secret"));
            File.WriteAllText(Path.Combine(tempDir, "visible.txt"), "");
            File.WriteAllText(Path.Combine(hiddenDir.FullName, "private.txt"), "");
            var config = BuildConfig(tempDir);

            // Act
            var result = ListFilesEndpoint.Handle(new ListFilesQuery(), config, NullLoggerFactory.Instance);

            // Assert — arquivo em diretório oculto não deve aparecer
            Assert.Single(result);
            Assert.Equal("visible.txt", result[0].FileName);
        });
    }

    static IConfiguration BuildConfig(string folder) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MonitoredFolder"] = folder })
            .Build();

    static void WithTempDir(Action<string> action)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try { action(tempDir); }
        finally { Directory.Delete(tempDir, recursive: true); }
    }
}
