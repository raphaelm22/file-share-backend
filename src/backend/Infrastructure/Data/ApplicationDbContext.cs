using Microsoft.EntityFrameworkCore;
using FileShare.Domain;

namespace FileShare.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Share> Shares => Set<Share>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Share>()
            .HasIndex(s => s.Token)
            .IsUnique()
            .HasDatabaseName("IX_Shares_Token");
    }
}
