using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Domain;
using FileShare.Infrastructure.Security;
using FileShare.Features.Shares.RevokeShare;
using FileShare.Infrastructure.Data;

namespace FileShare.Tests.Features.Shares.RevokeShare;

public sealed class RevokeShareEndpointTests : IDisposable
{
    readonly ApplicationDbContext _db;
    readonly EfRepository<Share> _repo;

    public RevokeShareEndpointTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _repo = new EfRepository<Share>(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_ExistingShare_DeletesItAndReturns204()
    {
        var share = new Share { TokenHash = TokenHashing.Hash(new string('a', 64)), FilePath = "/x", FileName = "x" };
        await _db.Shares.AddAsync(share);
        await _db.SaveChangesAsync();

        var result = await RevokeShareEndpoint.Handle(share.Id, _repo, NullLoggerFactory.Instance, default);

        Assert.IsType<NoContent>(result);
        Assert.False(await _db.Shares.AnyAsync(s => s.Id == share.Id));
    }

    [Fact]
    public async Task Handle_UnknownShare_Returns404()
    {
        var result = await RevokeShareEndpoint.Handle(Guid.NewGuid().ToString(), _repo, NullLoggerFactory.Instance, default);

        Assert.IsType<NotFound>(result);
    }
}
