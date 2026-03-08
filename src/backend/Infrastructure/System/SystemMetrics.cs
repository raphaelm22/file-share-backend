namespace FileShare.Infrastructure.System;

public sealed record SystemMetrics(
    double CpuPercent,
    long RamUsedMb,
    long RamTotalMb,
    double DiskUsedGb,
    double DiskTotalGb);
