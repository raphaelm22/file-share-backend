using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.BackgroundServices;
using FileShare.Domain;
using FileShare.Infrastructure.Data;
using FileShare.Tests.Infrastructure.FileSystem;

namespace FileShare.Tests.BackgroundServices;

public sealed class CleanupBackgroundServiceTests
{
    static ApplicationDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new ApplicationDbContext(options);
    }

    static CleanupBackgroundService CreateService(TestHubContext hub) =>
        new(hub, new NoOpScopeFactory(), NullLogger<CleanupBackgroundService>.Instance);

    static Share MakeShare(DateTime? expiresAt, string fileName = "test.txt") => new()
    {
        Id = Guid.NewGuid().ToString(),
        Token = Guid.NewGuid().ToString("N"),
        FilePath = "/app/shared-files/test.txt",
        FileName = fileName,
        FileSize = 1024,
        ExpiresAt = expiresAt,
        CreatedAt = DateTime.UtcNow.AddHours(-1)
    };

    [Fact]
    public async Task RunCleanupCycleAsync_WithExpiredShare_RemovesFromDb()
    {
        // Arrange
        using var db = CreateDb("Cleanup_RemovesExpired");
        var expiredShare = MakeShare(expiresAt: DateTime.UtcNow.AddHours(-1));
        db.Shares.Add(expiredShare);
        await db.SaveChangesAsync();
        var hub = new TestHubContext();
        var service = CreateService(hub);

        // Act
        await service.RunCleanupCycleAsync(db, CancellationToken.None);

        // Assert
        var remaining = await db.Shares.ToListAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task RunCleanupCycleAsync_WithExpiredShare_BroadcastsShareExpiredEvent()
    {
        // Arrange
        using var db = CreateDb("Cleanup_BroadcastsEvent");
        var expiredShare = MakeShare(expiresAt: DateTime.UtcNow.AddHours(-1), fileName: "report.pdf");
        db.Shares.Add(expiredShare);
        await db.SaveChangesAsync();
        var hub = new TestHubContext();
        var service = CreateService(hub);

        // Act
        await service.RunCleanupCycleAsync(db, CancellationToken.None);

        // Assert
        Assert.Single(hub.SentMessages);
        Assert.Equal("ShareExpired", hub.SentMessages[0].Method);
        var payload = Assert.IsType<ShareExpiredPayload>(hub.SentMessages[0].Arg);
        Assert.Equal(expiredShare.Token, payload.Token);
        Assert.Equal("report.pdf", payload.FileName);
    }

    [Fact]
    public async Task RunCleanupCycleAsync_WithExpiredShare_PreservesPhysicalFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            using var db = CreateDb("Cleanup_PreservesFile");
            var share = new Share
            {
                Id = Guid.NewGuid().ToString(),
                Token = Guid.NewGuid().ToString("N"),
                FilePath = tempFile,
                FileName = Path.GetFileName(tempFile),
                FileSize = 0,
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            };
            db.Shares.Add(share);
            await db.SaveChangesAsync();
            var service = CreateService(new TestHubContext());

            // Act
            await service.RunCleanupCycleAsync(db, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(tempFile), "Arquivo físico deve ser preservado após cleanup");
            Assert.Empty(await db.Shares.ToListAsync());
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task RunCleanupCycleAsync_WithActiveShare_DoesNotRemove()
    {
        // Arrange
        using var db = CreateDb("Cleanup_ActiveNotRemoved");
        var activeShare = MakeShare(expiresAt: DateTime.UtcNow.AddHours(24));
        db.Shares.Add(activeShare);
        await db.SaveChangesAsync();
        var hub = new TestHubContext();
        var service = CreateService(hub);

        // Act
        await service.RunCleanupCycleAsync(db, CancellationToken.None);

        // Assert
        var remaining = await db.Shares.ToListAsync();
        Assert.Single(remaining);
        Assert.Empty(hub.SentMessages);
    }

    [Fact]
    public async Task RunCleanupCycleAsync_WithNullExpiresAt_DoesNotRemove()
    {
        // Arrange
        using var db = CreateDb("Cleanup_InfiniteNotRemoved");
        var infiniteShare = MakeShare(expiresAt: null);
        db.Shares.Add(infiniteShare);
        await db.SaveChangesAsync();
        var hub = new TestHubContext();
        var service = CreateService(hub);

        // Act
        await service.RunCleanupCycleAsync(db, CancellationToken.None);

        // Assert
        var remaining = await db.Shares.ToListAsync();
        Assert.Single(remaining);
        Assert.Empty(hub.SentMessages);
    }

    [Fact]
    public async Task RunCleanupCycleAsync_WithMultipleExpired_RemovesAllAndBroadcastsAll()
    {
        // Arrange
        using var db = CreateDb("Cleanup_MultipleExpired");
        var shares = new[]
        {
            MakeShare(expiresAt: DateTime.UtcNow.AddHours(-2), fileName: "a.txt"),
            MakeShare(expiresAt: DateTime.UtcNow.AddHours(-1), fileName: "b.txt"),
            MakeShare(expiresAt: DateTime.UtcNow.AddHours(1), fileName: "active.txt")
        };
        db.Shares.AddRange(shares);
        await db.SaveChangesAsync();
        var hub = new TestHubContext();
        var service = CreateService(hub);

        // Act
        await service.RunCleanupCycleAsync(db, CancellationToken.None);

        // Assert
        var remaining = await db.Shares.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal("active.txt", remaining[0].FileName);
        Assert.Equal(2, hub.SentMessages.Count);
        Assert.All(hub.SentMessages, m => Assert.Equal("ShareExpired", m.Method));
        var broadcastedFileNames = hub.SentMessages
            .Select(m => Assert.IsType<ShareExpiredPayload>(m.Arg).FileName)
            .ToHashSet();
        Assert.Contains("a.txt", broadcastedFileNames);
        Assert.Contains("b.txt", broadcastedFileNames);
    }

    [Fact]
    public async Task RunCleanupCycleAsync_WithNoShares_NoBroadcastAndNoError()
    {
        // Arrange
        using var db = CreateDb("Cleanup_EmptyDb");
        var hub = new TestHubContext();
        var service = CreateService(hub);

        // Act
        await service.RunCleanupCycleAsync(db, CancellationToken.None);

        // Assert
        Assert.Empty(hub.SentMessages);
    }

    sealed class NoOpScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotImplementedException("Not used in unit tests");
    }
}
