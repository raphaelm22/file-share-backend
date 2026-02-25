using Microsoft.EntityFrameworkCore;

namespace FileShare.Infrastructure.Data;

public static class DatabaseMigrationExtensions
{
    public static WebApplication MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
        return app;
    }
}
