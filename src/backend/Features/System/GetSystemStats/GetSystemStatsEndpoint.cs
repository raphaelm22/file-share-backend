using Microsoft.AspNetCore.SignalR;
using Wolverine.Http;
using FileShare.Infrastructure.Hubs;
using FileShare.Infrastructure.System;

namespace FileShare.Features.System.GetSystemStats;

public static class GetSystemStatsEndpoint
{
    [WolverineGet("/api/v1/system")]
    public static async Task<SystemStatsResponse> Handle(
        GetSystemStatsQuery query,
        ISystemMetricsService metricsService,
        IHubContext<FileShareHub> hub,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetSystemStatsEndpoint));

        var metrics = await metricsService.GetMetricsAsync(ct);
        var response = SystemStatsResponse.From(metrics);

        logger.LogInformation(
            "SystemStats: CPU={Cpu}% RAM={RamUsed}/{RamTotal}MB Disk={DiskUsed}/{DiskTotal}GB",
            response.CpuPercent, response.RamUsedMb, response.RamTotalMb,
            response.DiskUsedGb, response.DiskTotalGb);

        try
        {
            await hub.Clients.All.SendAsync("SystemStats", response, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SystemStats SignalR broadcast failed");
        }

        return response;
    }
}
