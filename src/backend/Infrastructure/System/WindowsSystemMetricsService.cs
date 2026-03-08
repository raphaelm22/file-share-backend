using System.Runtime.InteropServices;

namespace FileShare.Infrastructure.System;

public sealed class WindowsSystemMetricsService : ISystemMetricsService
{
    readonly ILogger<WindowsSystemMetricsService> _logger;

    public WindowsSystemMetricsService(ILogger<WindowsSystemMetricsService> logger)
    {
        _logger = logger;
    }

    public async Task<SystemMetrics> GetMetricsAsync(CancellationToken ct = default)
    {
        try
        {
            var cpuTask = GetCpuPercentAsync(ct);
            var ramResult = GetRam();
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
        GetSystemTimes(out long idle1, out long kernel1, out long user1);
        await Task.Delay(150, ct);
        GetSystemTimes(out long idle2, out long kernel2, out long user2);

        return CalculateCpu(idle1, kernel1, user1, idle2, kernel2, user2);
    }

    static (long UsedMb, long TotalMb) GetRam()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
            return (0L, 0L);

        var totalKb = (long)(status.ullTotalPhys / 1024);
        var availKb = (long)(status.ullAvailPhys / 1024);
        return SystemMetricsCalculations.CalculateRam(totalKb, availKb);
    }

    static (double UsedGb, double TotalGb) GetDisk()
    {
        var path = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";
        var drive = new DriveInfo(path);
        return SystemMetricsCalculations.CalculateDisk(drive.TotalSize, drive.AvailableFreeSpace);
    }

    // ─── Helpers (internal for testing) ─────────────────────────────────────

    internal static double CalculateCpu(
        long idle1, long kernel1, long user1,
        long idle2, long kernel2, long user2)
    {
        // kernel time includes idle time on Windows
        var total1 = kernel1 + user1;
        var total2 = kernel2 + user2;

        var deltaTotal = total2 - total1;
        var deltaIdle = idle2 - idle1;

        if (deltaTotal == 0) return 0.0;
        return Math.Round((1.0 - (double)deltaIdle / deltaTotal) * 100.0, 1);
    }

    // ─── P/Invoke ───────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetSystemTimes(
        out long lpIdleTime,
        out long lpKernelTime,
        out long lpUserTime);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
