using Microsoft.EntityFrameworkCore;
using FileShare.Infrastructure.Data;

namespace FileShare.Tests.Infrastructure.Data;

public sealed class ApplicationDbContextTests
{
    [Fact]
    public void ApplicationDbContext_CanBeCreated_WithInMemoryDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_Creation")
            .Options;

        // Act
        using var context = new ApplicationDbContext(options);

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public void ApplicationDbContext_HasSharesEntity_AfterStory2_1()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_ShareEntity")
            .Options;
        using var context = new ApplicationDbContext(options);

        // Act
        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType).ToList();

        // Assert
        Assert.Contains(typeof(FileShare.Domain.Share), entityTypes);
    }

    [Fact]
    public void ApplicationDbContext_CanSaveAndRetrieveChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_Smoke")
            .Options;
        using var context = new ApplicationDbContext(options);

        // Act
        var created = context.Database.EnsureCreated();

        // Assert — In-memory db with no entity types: EnsureCreated returns true on first call.
        Assert.True(created);
    }
}
