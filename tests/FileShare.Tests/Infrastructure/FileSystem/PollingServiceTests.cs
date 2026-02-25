using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Infrastructure.FileSystem;
using FileShare.Infrastructure.Hubs;

namespace FileShare.Tests.Infrastructure.FileSystem;

public sealed class PollingServiceTests
{
    static IConfiguration BuildConfig(string folder) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MonitoredFolder"] = folder })
            .Build();

    static PollingService CreateService(string folder, FileStateTracker tracker, TestHubContext hub) =>
        new(hub, BuildConfig(folder), NullLogger<PollingService>.Instance, tracker);

    [Fact]
    public async Task PollOnce_NewFileAppears_SendsFileAddedAndTracksIt()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var tracker = new FileStateTracker();
            var hub = new TestHubContext();
            var service = CreateService(tempDir, tracker, hub);
            File.WriteAllText(Path.Combine(tempDir, "alpha.txt"), "hello");

            // Act
            await service.PollOnceAsync(tempDir, CancellationToken.None);

            // Assert
            Assert.Single(hub.SentMessages);
            Assert.Equal("FileAdded", hub.SentMessages[0].Method);
            var payload = Assert.IsType<FileAddedPayload>(hub.SentMessages[0].Arg);
            Assert.Equal("alpha.txt", payload.FileName);
            Assert.Equal(5, payload.FileSize);
            Assert.Equal(TimeSpan.Zero, payload.ModifiedAt.Offset);
            Assert.Contains("alpha.txt", tracker.CurrentFiles);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task PollOnce_FileRemoved_SendsFileRemovedAndUntracksIt()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var tracker = new FileStateTracker();
            tracker.Initialize(["alpha.txt"]); // alpha.txt is tracked but does not exist on disk
            var hub = new TestHubContext();
            var service = CreateService(tempDir, tracker, hub);

            // Act
            await service.PollOnceAsync(tempDir, CancellationToken.None);

            // Assert
            Assert.Single(hub.SentMessages);
            Assert.Equal("FileRemoved", hub.SentMessages[0].Method);
            var payload = Assert.IsType<FileRemovedPayload>(hub.SentMessages[0].Arg);
            Assert.Equal("alpha.txt", payload.FileName);
            Assert.Empty(tracker.CurrentFiles);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task PollOnce_AlreadyTrackedFile_DoesNotSendDuplicate()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "alpha.txt"), "hello");
            var tracker = new FileStateTracker();
            tracker.Initialize(["alpha.txt"]);
            var hub = new TestHubContext();
            var service = CreateService(tempDir, tracker, hub);

            // Act
            await service.PollOnceAsync(tempDir, CancellationToken.None);

            // Assert
            Assert.Empty(hub.SentMessages);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task PollOnce_HiddenFilesAreIgnored()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".hidden"), "secret");
            File.WriteAllText(Path.Combine(tempDir, ".gitkeep"), "");
            var tracker = new FileStateTracker();
            var hub = new TestHubContext();
            var service = CreateService(tempDir, tracker, hub);

            // Act
            await service.PollOnceAsync(tempDir, CancellationToken.None);

            // Assert
            Assert.Empty(hub.SentMessages);
            Assert.Empty(tracker.CurrentFiles);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task PollOnce_FolderNotFound_DoesNotThrow()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid());
        var tracker = new FileStateTracker();
        var hub = new TestHubContext();
        var service = CreateService(nonExistentDir, tracker, hub);

        // Act
        await service.PollOnceAsync(nonExistentDir, CancellationToken.None);

        // Assert
        Assert.Empty(hub.SentMessages);
    }
}

// Manual test double — no mock framework installed
internal sealed class TestHubContext : IHubContext<FileShareHub>
{
    readonly TestHubClients _clients;

    public List<(string Method, object Arg)> SentMessages { get; } = [];

    public TestHubContext() => _clients = new TestHubClients(SentMessages);

    public IHubClients Clients => _clients;
    public IGroupManager Groups => throw new NotImplementedException();
}

internal sealed class TestHubClients(List<(string Method, object Arg)> messages) : IHubClients
{
    public IClientProxy All => new TestClientProxy(messages);
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Client(string connectionId) => throw new NotImplementedException();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
    public IClientProxy Group(string groupName) => throw new NotImplementedException();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
    public IClientProxy User(string userId) => throw new NotImplementedException();
    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
}

internal sealed class TestClientProxy(List<(string Method, object Arg)> messages) : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length > 0 && args[0] is not null)
            messages.Add((method, args[0]!));
        return Task.CompletedTask;
    }
}
