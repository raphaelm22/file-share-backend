using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Domain;
using FileShare.Features.Download.DownloadFile;
using FileShare.Infrastructure.Data;

namespace FileShare.Tests.Features.Download.DownloadFile;

public sealed class DownloadFileEndpointTests : IDisposable
{
    readonly ApplicationDbContext _db;
    readonly EfRepository<Share> _repo;
    readonly List<string> _tempFiles = [];

    public DownloadFileEndpointTests()
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

    string CreateTempFile(string content = "file data")
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsPhysicalFileWithRangeProcessing()
    {
        // Arrange
        var filePath = CreateTempFile("hello world");
        await _db.Shares.AddAsync(new Share
        {
            Token = new string('a', 64),
            FilePath = filePath,
            FileName = "document.pdf",
            FileSize = 11,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        // Act
        var result = await DownloadFileEndpoint.Handle(new string('a', 64), _repo, NullLoggerFactory.Instance, default);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileHttpResult>(result);
        Assert.Equal(filePath, fileResult.FileName);
        Assert.Equal("document.pdf", fileResult.FileDownloadName);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.True(fileResult.EnableRangeProcessing);
    }

    [Fact]
    public async Task Handle_ValidToken_WithUnknownExtension_ReturnsFallbackContentType()
    {
        // Arrange
        var filePath = CreateTempFile("binary data");
        await _db.Shares.AddAsync(new Share
        {
            Token = new string('d', 64),
            FilePath = filePath,
            FileName = "archive.xyz",
            FileSize = 11,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        // Act
        var result = await DownloadFileEndpoint.Handle(new string('d', 64), _repo, NullLoggerFactory.Instance, default);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileHttpResult>(result);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.True(fileResult.EnableRangeProcessing);
    }

    [Fact]
    public async Task Handle_TokenNotFound_Returns404WithTokenNotFoundDetail()
    {
        // Arrange
        // Act
        var result = await DownloadFileEndpoint.Handle(new string('x', 64), _repo, NullLoggerFactory.Instance, default);

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
            Token = new string('b', 64),
            FilePath = filePath,
            FileName = "old.pdf",
            FileSize = 100,
            ExpiresAt = DateTime.UtcNow.AddHours(-2),
            CreatedAt = DateTime.UtcNow.AddHours(-26)
        });
        await _db.SaveChangesAsync();
        // Act
        var result = await DownloadFileEndpoint.Handle(new string('b', 64), _repo, NullLoggerFactory.Instance, default);

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
            Token = new string('c', 64),
            FilePath = "/does/not/exist/file.zip",
            FileName = "ghost.zip",
            FileSize = 500,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        // Act
        var result = await DownloadFileEndpoint.Handle(new string('c', 64), _repo, NullLoggerFactory.Instance, default);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(404, problem.StatusCode);
        Assert.Equal("FILE_NOT_FOUND", problem.ProblemDetails.Detail);
    }
}
