namespace FileShare.Infrastructure.System;

public sealed class LinuxSystemMetricsService : ISystemMetricsService
{
    readonly ILogger<LinuxSystemMetricsService> _logger;

    public LinuxSystemMetricsService(ILogger<LinuxSystemMetricsService> logger)
    {
        _logger = logger;
    }

    public async Task<SystemMetrics> GetMetricsAsync(CancellationToken ct = default)
    {
        try
        {
            var cpuTask = GetCpuPercentAsync(ct);
            var ramResult = await GetRamAsync(ct);
            var diskResult = GetDisk();
            var cpuPercent = await cpuTask;

            return new SystemMetrics(
                CpuPercent: cpuPercent,
                RamUsedMb: ramResult.UsedMb,
                RamTotalMb: ramResult.TotalMb,
                DiskUsedGb: diskResult.UsedGb,
                DiskTotalGb: diskResult.TotalGb);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect system metrics");
            throw;
        }
    }

    static async Task<double> GetCpuPercentAsync(CancellationToken ct)
    {
        var v1 = await SampleProcStatAsync(ct);
        if (v1.Length < 5) return 0.0;

        await Task.Delay(150, ct);

        var v2 = await SampleProcStatAsync(ct);
        if (v2.Length < 5) return 0.0;

        return CalculateCpu(v1, v2);
    }

    static async Task<long[]> SampleProcStatAsync(CancellationToken ct)
    {
        using var reader = new StreamReader("/proc/stat");
        var line = await reader.ReadLineAsync(ct) ?? string.Empty;
        return ParseProcStatLine(line);
    }

    static async Task<(long UsedMb, long TotalMb)> GetRamAsync(CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync("/proc/meminfo", ct);
        if (!TryParseMemInfo(lines, out var totalKb, out var availKb))
            return (0L, 0L);

        return SystemMetricsCalculations.CalculateRam(totalKb, availKb);
    }

    static (double UsedGb, double TotalGb) GetDisk()
    {
        var drive = new DriveInfo("/");
        return SystemMetricsCalculations.CalculateDisk(drive.TotalSize, drive.AvailableFreeSpace);
    }

    // ─── Helpers (internal for testing) ─────────────────────────────────────

    internal static long[] ParseProcStatLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Skip(1).Select(long.Parse).ToArray();
    }

    internal static bool TryParseMemInfo(string[] lines, out long totalKb, out long availKb)
    {
        totalKb = 0;
        availKb = 0;
        var found = 0;

        foreach (var line in lines)
        {
            if (!line.Contains(':')) continue;

            var colon = line.IndexOf(':');
            var key = line[..colon].Trim();
            if (key is not ("MemTotal" or "MemAvailable")) continue;

            var value = long.Parse(line[(colon + 1)..].TrimStart().Split(' ')[0]);
            if (key == "MemTotal") totalKb = value;
            else availKb = value;

            if (++found == 2) return true;
        }

        return false;
    }

    internal static double CalculateCpu(long[] v1, long[] v2)
    {
        var idle1 = v1[3] + v1[4]; // idle + iowait
        var total1 = v1.Sum();
        var idle2 = v2[3] + v2[4];
        var total2 = v2.Sum();

        var deltaTotal = total2 - total1;
        var deltaIdle = idle2 - idle1;

        if (deltaTotal == 0) return 0.0;
        return Math.Round((1.0 - (double)deltaIdle / deltaTotal) * 100.0, 1);
    }
}
