using Microsoft.EntityFrameworkCore;
using FileShare.Domain;
using FileShare.Features.Shares.ListShares;
using FileShare.Infrastructure.Data;

namespace FileShare.Tests.Features.Shares.ListShares;

public sealed class ActiveSharesSpecTests
{
    [Fact]
    public async Task ListAsync_WithMultipleShares_ReturnsOrderedByCreatedAtDesc()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options);
        var repo = new EfRepository<Share>(db);

        var older = new Share { CreatedAt = DateTime.UtcNow.AddHours(-2) };
        var newer = new Share { CreatedAt = DateTime.UtcNow.AddHours(-1) };
        await db.Shares.AddRangeAsync(older, newer);
        await db.SaveChangesAsync();

        // Act
        var result = await repo.ListAsync(new ActiveSharesSpec(), default);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result[0].CreatedAt >= result[1].CreatedAt);
    }
}
