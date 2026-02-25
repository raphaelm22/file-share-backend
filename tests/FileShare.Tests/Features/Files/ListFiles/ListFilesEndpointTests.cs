using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Features.Files.ListFiles;

namespace FileShare.Tests.Features.Files.ListFiles;

public sealed class ListFilesEndpointTests
{
    [Fact]
    public void Handle_EmptyDirectory_ReturnsEmptyArray()
    {
        // Arrange & Act & Assert
        WithTempDir(tempDir =>
        {
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
        // Arrange & Act & Assert
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
        // Arrange & Act & Assert
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
