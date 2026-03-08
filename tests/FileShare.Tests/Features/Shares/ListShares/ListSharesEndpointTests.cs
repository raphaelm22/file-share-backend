using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Domain;
using FileShare.Features.Shares.ListShares;
using FileShare.Infrastructure.Data;

namespace FileShare.Tests.Features.Shares.ListShares;

public sealed class ListSharesEndpointTests : IDisposable
{
    readonly ApplicationDbContext _db;
    readonly EfRepository<Share> _repo;
    readonly List<string> _tempFiles = [];

    public ListSharesEndpointTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _repo = new EfRepository<Share>(_db);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
        _db.Dispose();
    }

    string CreateTempFile()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmptyArray()
    {
        // Act
        var result = await ListSharesEndpoint.Handle(_repo, NullLoggerFactory.Instance, default);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ActiveShare_ReturnsStatusActive()
    {
        // Arrange
        var filePath = CreateTempFile();
        await _db.Shares.AddAsync(new Share
        {
            FilePath = filePath,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await ListSharesEndpoint.Handle(_repo, NullLoggerFactory.Instance, default);

        // Assert
        Assert.Single(result);
        Assert.Equal("active", result[0].Status);
        Assert.Equal(DateTimeKind.Utc, result[0].CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, result[0].ExpiresAt!.Value.Kind);
    }

    [Fact]
    public async Task Handle_ExpiredShare_ReturnsStatusExpired()
    {
        // Arrange
        var filePath = CreateTempFile();
        await _db.Shares.AddAsync(new Share
        {
            FilePath = filePath,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await ListSharesEndpoint.Handle(_repo, NullLoggerFactory.Instance, default);

        // Assert
        Assert.Single(result);
        Assert.Equal("expired", result[0].Status);
    }

    [Fact]
    public async Task Handle_FileRemovedShare_ReturnsStatusFileRemoved()
    {
        // Arrange
        await _db.Shares.AddAsync(new Share
        {
            FilePath = "/nonexistent/path/file.txt",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await ListSharesEndpoint.Handle(_repo, NullLoggerFactory.Instance, default);

        // Assert
        Assert.Single(result);
        Assert.Equal("file-removed", result[0].Status);
    }

    [Fact]
    public async Task Handle_InfiniteShare_ReturnsStatusActiveAndNullExpiresAt()
    {
        // Arrange
        var filePath = CreateTempFile();
        await _db.Shares.AddAsync(new Share
        {
            FilePath = filePath,
            ExpiresAt = null
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await ListSharesEndpoint.Handle(_repo, NullLoggerFactory.Instance, default);

        // Assert
        Assert.Single(result);
        Assert.Equal("active", result[0].Status);
        Assert.Null(result[0].ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, result[0].CreatedAt.Kind);
    }

    [Fact]
    public async Task Handle_InfiniteShareWithRemovedFile_ReturnsStatusFileRemoved()
    {
        // Arrange
        await _db.Shares.AddAsync(new Share
        {
            FilePath = "/nonexistent/path/file.txt",
            ExpiresAt = null
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await ListSharesEndpoint.Handle(_repo, NullLoggerFactory.Instance, default);

        // Assert
        Assert.Single(result);
        Assert.Equal("file-removed", result[0].Status);
        Assert.Null(result[0].ExpiresAt);
    }

    [Fact]
    public async Task Handle_ResponseDoesNotContainFilePath()
    {
        // Arrange
        var props = typeof(ListSharesResponse).GetProperties();

        // Act & Assert
        Assert.DoesNotContain(props, p => p.Name.Equals("FilePath", StringComparison.OrdinalIgnoreCase));
    }
}
