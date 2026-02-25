using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Domain;
using FileShare.Features.Shares.CreateShare;
using FileShare.Infrastructure.Data;
using FileShare.Infrastructure.Security;

namespace FileShare.Tests.Features.Shares.CreateShare;

public sealed class CreateShareEndpointTests : IDisposable
{
    readonly ApplicationDbContext _db;
    readonly EfRepository<Share> _repo;
    readonly SasTokenService _tokenService;
    string? _tempFile;

    public CreateShareEndpointTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _repo = new EfRepository<Share>(_db);
        _tokenService = new SasTokenService();
    }

    public void Dispose()
    {
        if (_tempFile != null && File.Exists(_tempFile))
            File.Delete(_tempFile);
        _db.Dispose();
    }

    string CreateTempFile(string content = "test content")
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, content);
        return _tempFile;
    }

    [Fact]
    public async Task Handle_FileExists_Returns201WithExpectedFields()
    {
        // Arrange
        var filePath = CreateTempFile("hello world");
        var command = new CreateShareCommand(filePath, TtlHours: 24);

        // Act
        var result = await CreateShareEndpoint.Handle(command, _repo, _tokenService, NullLoggerFactory.Instance, default);

        // Assert
        var created = Assert.IsType<Created<CreateShareResponse>>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.NotNull(created.Value);
        Assert.Equal(64, created.Value!.Token.Length);
        Assert.Matches("^[0-9a-f]{64}$", created.Value.Token);
        Assert.Equal(Path.GetFileName(filePath), created.Value.FileName);
        Assert.NotNull(created.Value.ExpiresAt);
        Assert.True(created.Value.ExpiresAt!.Value > DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_FileMissing_Returns422WithProblemDetails()
    {
        // Arrange
        var command = new CreateShareCommand("/nonexistent/file.txt", TtlHours: 24);

        // Act
        var result = await CreateShareEndpoint.Handle(command, _repo, _tokenService, NullLoggerFactory.Instance, default);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(422, problem.StatusCode);
        Assert.Equal("FILE_NOT_FOUND", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Handle_NullTtlHours_ReturnsNullExpiresAt()
    {
        // Arrange
        var filePath = CreateTempFile();
        var command = new CreateShareCommand(filePath, TtlHours: null);

        // Act
        var result = await CreateShareEndpoint.Handle(command, _repo, _tokenService, NullLoggerFactory.Instance, default);

        // Assert
        var created = Assert.IsType<Created<CreateShareResponse>>(result);
        Assert.Null(created.Value!.ExpiresAt);
    }

    [Fact]
    public async Task Handle_FileExists_ResponseDoesNotContainFilePath()
    {
        // Arrange
        var filePath = CreateTempFile();
        var command = new CreateShareCommand(filePath, TtlHours: 1);

        // Act
        var result = await CreateShareEndpoint.Handle(command, _repo, _tokenService, NullLoggerFactory.Instance, default);

        // Assert — CreateShareResponse type has no FilePath property
        var created = Assert.IsType<Created<CreateShareResponse>>(result);
        var props = typeof(CreateShareResponse).GetProperties();
        Assert.DoesNotContain(props, p => p.Name.Equals("FilePath", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_ZeroTtlHours_Returns422WithProblemDetails()
    {
        // Arrange
        var command = new CreateShareCommand("/any/file.txt", TtlHours: 0);

        // Act
        var result = await CreateShareEndpoint.Handle(command, _repo, _tokenService, NullLoggerFactory.Instance, default);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(422, problem.StatusCode);
        Assert.Equal("TTL_MUST_BE_POSITIVE", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Handle_NegativeTtlHours_Returns422WithProblemDetails()
    {
        // Arrange
        var command = new CreateShareCommand("/any/file.txt", TtlHours: -24);

        // Act
        var result = await CreateShareEndpoint.Handle(command, _repo, _tokenService, NullLoggerFactory.Instance, default);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(422, problem.StatusCode);
        Assert.Equal("TTL_MUST_BE_POSITIVE", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Handle_FileExists_PersistsShareToRepository()
    {
        // Arrange
        var filePath = CreateTempFile("content");
        var command = new CreateShareCommand(filePath, TtlHours: 168);

        // Act
        await CreateShareEndpoint.Handle(command, _repo, _tokenService, NullLoggerFactory.Instance, default);

        // Assert
        var shares = await _db.Shares.ToListAsync();
        Assert.Single(shares);
        Assert.Equal(Path.GetFileName(filePath), shares[0].FileName);
        Assert.Equal(64, shares[0].Token.Length);
        Assert.Equal(filePath, shares[0].FilePath);
    }
}
