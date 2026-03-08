namespace FileShare.Infrastructure.System;

internal static class SystemMetricsCalculations
{
    internal static (long UsedMb, long TotalMb) CalculateRam(long totalKb, long availKb)
        => (UsedMb: (totalKb - availKb) / 1024, TotalMb: totalKb / 1024);

    internal static (double UsedGb, double TotalGb) CalculateDisk(long totalBytes, long freeBytes)
    {
        const double gb = 1024.0 * 1024.0 * 1024.0;
        return (
            UsedGb: Math.Round((totalBytes - freeBytes) / gb, 2),
            TotalGb: Math.Round(totalBytes / gb, 2));
    }
}
