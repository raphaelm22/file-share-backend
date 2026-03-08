using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using FileShare.Infrastructure.Data;
using FileShare.Infrastructure.Hubs;

namespace FileShare.BackgroundServices;

public sealed class CleanupBackgroundService : BackgroundService
{
    readonly IHubContext<FileShareHub> _hub;
    readonly IServiceScopeFactory _scopeFactory;
    readonly ILogger<CleanupBackgroundService> _logger;

    public CleanupBackgroundService(
        IHubContext<FileShareHub> hub,
        IServiceScopeFactory scopeFactory,
        ILogger<CleanupBackgroundService> logger)
    {
        _hub = hub;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cleanup service started");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await RunCleanupCycleAsync(db, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Cleanup cycle failed, next attempt in 60s");
            }
        }
        _logger.LogInformation("Cleanup service stopped");
    }

    internal async Task RunCleanupCycleAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = await db.Shares
            .Where(s => s.ExpiresAt != null && s.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return;

        db.Shares.RemoveRange(expired);
        await db.SaveChangesAsync(ct);

        foreach (var share in expired)
        {
            await _hub.Clients.All.SendAsync(
                "ShareExpired",
                new ShareExpiredPayload(share.Token, share.FileName),
                ct);
        }

        _logger.LogInformation("Removed {Count} expired shares", expired.Count);
    }
}
