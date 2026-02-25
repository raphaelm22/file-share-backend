using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Infrastructure.FileSystem;

namespace FileShare.Tests.Infrastructure.FileSystem;

public sealed class FileSystemWatcherServiceTests
{
    static IConfiguration BuildConfig(string folder) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MonitoredFolder"] = folder })
            .Build();

    static FileSystemWatcherService CreateService(string folder, FileStateTracker tracker, TestHubContext hub) =>
        new(hub, BuildConfig(folder), NullLogger<FileSystemWatcherService>.Instance, tracker);

    [Fact]
    public void OnFileCreated_NewFile_SendsFileAddedAndTracksIt()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "alpha.txt"), "hello");
            var tracker = new FileStateTracker();
            var hub = new TestHubContext();
            var service = CreateService(tempDir, tracker, hub);

            // Act
            service.OnFileCreated(null!, new FileSystemEventArgs(
                WatcherChangeTypes.Created, tempDir, "alpha.txt"));

            // Assert
            Assert.Single(hub.SentMessages);
            Assert.Equal("FileAdded", hub.SentMessages[0].Method);
            var payload = Assert.IsType<FileAddedPayload>(hub.SentMessages[0].Arg);
            Assert.Equal("alpha.txt", payload.FileName);
            Assert.Equal(5, payload.FileSize);
            Assert.Contains("alpha.txt", tracker.CurrentFiles);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public void OnFileCreated_HiddenFile_NoSend()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".hidden"), "secret");
            var tracker = new FileStateTracker();
            var hub = new TestHubContext();
            var service = CreateService(tempDir, tracker, hub);

            // Act
            service.OnFileCreated(null!, new FileSystemEventArgs(
                WatcherChangeTypes.Created, tempDir, ".hidden"));

            // Assert
            Assert.Empty(hub.SentMessages);
            Assert.Empty(tracker.CurrentFiles);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public void OnFileCreated_AlreadyTracked_NoSend()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "alpha.txt"), "hello");
            var tracker = new FileStateTracker();
            tracker.TryAdd("alpha.txt");
            var hub = new TestHubContext();
            var service = CreateService(tempDir, tracker, hub);

            // Act
            service.OnFileCreated(null!, new FileSystemEventArgs(
                WatcherChangeTypes.Created, tempDir, "alpha.txt"));

            // Assert
            Assert.Empty(hub.SentMessages);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public void OnFileDeleted_TrackedFile_SendsFileRemovedAndUntracksIt()
    {
        // Arrange
        var tracker = new FileStateTracker();
        tracker.TryAdd("alpha.txt");
        var hub = new TestHubContext();
        var service = CreateService(Path.GetTempPath(), tracker, hub);

        // Act
        service.OnFileDeleted(null!, new FileSystemEventArgs(
            WatcherChangeTypes.Deleted, Path.GetTempPath(), "alpha.txt"));

        // Assert
        Assert.Single(hub.SentMessages);
        Assert.Equal("FileRemoved", hub.SentMessages[0].Method);
        var payload = Assert.IsType<FileRemovedPayload>(hub.SentMessages[0].Arg);
        Assert.Equal("alpha.txt", payload.FileName);
        Assert.Empty(tracker.CurrentFiles);
    }

    [Fact]
    public void OnFileDeleted_HiddenFile_NoSend()
    {
        // Arrange
        var tracker = new FileStateTracker();
        var hub = new TestHubContext();
        var service = CreateService(Path.GetTempPath(), tracker, hub);

        // Act
        service.OnFileDeleted(null!, new FileSystemEventArgs(
            WatcherChangeTypes.Deleted, Path.GetTempPath(), ".hidden"));

        // Assert
        Assert.Empty(hub.SentMessages);
    }

    [Fact]
    public void OnFileRenamed_RenamesFile_SendsRemovedAndAdded()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "beta.txt"), "world");
            var tracker = new FileStateTracker();
            tracker.TryAdd("alpha.txt");
            var hub = new TestHubContext();
            var service = CreateService(tempDir, tracker, hub);

            // Act
            service.OnFileRenamed(null!, new RenamedEventArgs(
                WatcherChangeTypes.Renamed, tempDir, "beta.txt", "alpha.txt"));

            // Assert
            Assert.Equal(2, hub.SentMessages.Count);
            Assert.Equal("FileRemoved", hub.SentMessages[0].Method);
            Assert.Equal("FileAdded", hub.SentMessages[1].Method);
            Assert.DoesNotContain("alpha.txt", tracker.CurrentFiles);
            Assert.Contains("beta.txt", tracker.CurrentFiles);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }
}
