using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Features.Files.GetDirectoryTree;

namespace FileShare.Tests.Features.Files.GetDirectoryTree;

public sealed class GetDirectoryTreeEndpointTests
{
    static readonly string[] ExpectedAlphabeticalOrder = ["alpha", "mango", "zebra"];

    [Fact]
    public void Handle_EmptyRoot_ReturnsRootWithNoChildren()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            var config = BuildConfig(tempDir);

            // Act
            var result = GetDirectoryTreeEndpoint.Handle(config, NullLoggerFactory.Instance);

            // Assert
            Assert.Equal("", result.Root.Path);
            Assert.Equal(Path.GetFileName(tempDir), result.Root.Name);
            Assert.Empty(result.Root.Children);
        });
    }

    [Fact]
    public void Handle_OneLevelOfSubdirectories_ReturnsCorrectChildren()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "backups"));
            Directory.CreateDirectory(Path.Combine(tempDir, "docs"));
            var config = BuildConfig(tempDir);

            // Act
            var result = GetDirectoryTreeEndpoint.Handle(config, NullLoggerFactory.Instance);

            // Assert
            Assert.Equal(2, result.Root.Children.Count);
            Assert.Contains(result.Root.Children, c => c.Name == "backups" && c.Path == "backups");
            Assert.Contains(result.Root.Children, c => c.Name == "docs" && c.Path == "docs");
            Assert.All(result.Root.Children, c => Assert.Empty(c.Children));
        });
    }

    [Fact]
    public void Handle_TwoLevelsNested_ReturnsRecursiveTree()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "backups", "2025"));
            var config = BuildConfig(tempDir);

            // Act
            var result = GetDirectoryTreeEndpoint.Handle(config, NullLoggerFactory.Instance);

            // Assert
            Assert.Single(result.Root.Children);
            var backupsNode = result.Root.Children[0];
            Assert.Equal("backups", backupsNode.Name);
            Assert.Equal("backups", backupsNode.Path);
            Assert.Single(backupsNode.Children);
            var yearNode = backupsNode.Children[0];
            Assert.Equal("2025", yearNode.Name);
            Assert.Equal("backups/2025", yearNode.Path);
            Assert.Empty(yearNode.Children);
        });
    }

    [Fact]
    public void Handle_DirectoriesReturnedAlphabetically()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "zebra"));
            Directory.CreateDirectory(Path.Combine(tempDir, "alpha"));
            Directory.CreateDirectory(Path.Combine(tempDir, "mango"));
            var config = BuildConfig(tempDir);

            // Act
            var result = GetDirectoryTreeEndpoint.Handle(config, NullLoggerFactory.Instance);

            // Assert
            var names = result.Root.Children.Select(c => c.Name).ToArray();
            Assert.Equal(ExpectedAlphabeticalOrder, names);
        });
    }

    [Fact]
    public void Handle_FilesInDirectory_NotIncludedInTree()
    {
        // Arrange
        WithTempDir(tempDir =>
        {
            File.WriteAllText(Path.Combine(tempDir, "file.txt"), "content");
            Directory.CreateDirectory(Path.Combine(tempDir, "subdir"));
            var config = BuildConfig(tempDir);

            // Act
            var result = GetDirectoryTreeEndpoint.Handle(config, NullLoggerFactory.Instance);

            // Assert
            Assert.Single(result.Root.Children);
            Assert.Equal("subdir", result.Root.Children[0].Name);
        });
    }

    [Fact]
    public void Handle_NonExistentFolder_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), "non-existent-" + Guid.NewGuid());
        var config = BuildConfig(nonExistentDir);

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() =>
            GetDirectoryTreeEndpoint.Handle(config, NullLoggerFactory.Instance));
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
