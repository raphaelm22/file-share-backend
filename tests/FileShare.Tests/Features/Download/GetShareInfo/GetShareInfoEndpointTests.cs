using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Domain;
using FileShare.Features.Download.GetShareInfo;
using FileShare.Infrastructure.Data;

namespace FileShare.Tests.Features.Download.GetShareInfo;

public sealed class GetShareInfoEndpointTests : IDisposable
{
    readonly ApplicationDbContext _db;
    readonly EfRepository<Share> _repo;
    readonly List<string> _tempFiles = [];

    public GetShareInfoEndpointTests()
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

    string CreateTempFile(string content = "test content")
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task Handle_ValidToken_Returns200WithShareInfo()
    {
        // Arrange
        var filePath = CreateTempFile();
        await _db.Shares.AddAsync(new Share
        {
            Token = new string('a', 64),
            FilePath = filePath,
            FileName = "test.pdf",
            FileSize = 1024,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        // Act
        var result = await GetShareInfoEndpoint.Handle(new string('a', 64), _repo, NullLoggerFactory.Instance, default);

        // Assert
        var ok = Assert.IsType<Ok<ShareInfoResponse>>(result);
        Assert.Equal("test.pdf", ok.Value!.FileName);
        Assert.Equal(1024L, ok.Value.FileSize);
        Assert.NotNull(ok.Value.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, ok.Value.ExpiresAt!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, ok.Value.CreatedAt.Kind);
    }

    [Fact]
    public async Task Handle_TokenNotFound_Returns404WithTokenNotFoundDetail()
    {
        // Arrange
        // Act
        var result = await GetShareInfoEndpoint.Handle(new string('z', 64), _repo, NullLoggerFactory.Instance, default);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(404, problem.StatusCode);
        Assert.Equal("TOKEN_NOT_FOUND", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Handle_ExpiredToken_Returns404WithShareExpiredDetail()
    {
        // Arrange
        var filePath = CreateTempFile();
        await _db.Shares.AddAsync(new Share
        {
            Token = new string('e', 64),
            FilePath = filePath,
            FileName = "expired.pdf",
            FileSize = 512,
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-25)
        });
        await _db.SaveChangesAsync();
        // Act
        var result = await GetShareInfoEndpoint.Handle(new string('e', 64), _repo, NullLoggerFactory.Instance, default);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(404, problem.StatusCode);
        Assert.Equal("SHARE_EXPIRED_OR_INVALID", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Handle_FileRemovedFromDisk_Returns404WithFileNotFoundDetail()
    {
        // Arrange
        await _db.Shares.AddAsync(new Share
        {
            Token = new string('f', 64),
            FilePath = "/nonexistent/path/file.pdf",
            FileName = "missing.pdf",
            FileSize = 256,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        // Act
        var result = await GetShareInfoEndpoint.Handle(new string('f', 64), _repo, NullLoggerFactory.Instance, default);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(404, problem.StatusCode);
        Assert.Equal("FILE_NOT_FOUND", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Handle_InfiniteToken_Returns200WithNullExpiresAt()
    {
        // Arrange
        var filePath = CreateTempFile();
        await _db.Shares.AddAsync(new Share
        {
            Token = new string('i', 64),
            FilePath = filePath,
            FileName = "infinite.pdf",
            FileSize = 2048,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        // Act
        var result = await GetShareInfoEndpoint.Handle(new string('i', 64), _repo, NullLoggerFactory.Instance, default);

        // Assert
        var ok = Assert.IsType<Ok<ShareInfoResponse>>(result);
        Assert.Null(ok.Value!.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, ok.Value.CreatedAt.Kind);
    }
}
