using FileShare.Infrastructure.System;

namespace FileShare.Features.System.GetSystemStats;

public sealed record SystemStatsResponse(
    double CpuPercent,
    long RamUsedMb,
    long RamTotalMb,
    double DiskUsedGb,
    double DiskTotalGb)
{
    public static SystemStatsResponse From(SystemMetrics m) =>
        new(m.CpuPercent, m.RamUsedMb, m.RamTotalMb, m.DiskUsedGb, m.DiskTotalGb);
}
