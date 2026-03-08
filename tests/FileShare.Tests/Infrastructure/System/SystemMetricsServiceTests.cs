using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Infrastructure.System;

namespace FileShare.Tests.Infrastructure.System;

public sealed class LinuxSystemMetricsServiceTests
{
    // ─── ParseProcStatLine ──────────────────────────────────────────────────

    [Fact]
    public void ParseProcStatLine_SkipsFirstToken_ReturnsNumericValues()
    {
        // Arrange
        var line = "cpu  100 0 200 800 100 0 0 0 0 0";

        // Act
        var result = LinuxSystemMetricsService.ParseProcStatLine(line);

        // Assert
        Assert.Equal(new long[] { 100, 0, 200, 800, 100, 0, 0, 0, 0, 0 }, result);
    }

    [Fact]
    public void ParseProcStatLine_SingleSpace_ParsesCorrectly()
    {
        // Arrange
        var line = "cpu 50 0 50 800 0";

        // Act
        var result = LinuxSystemMetricsService.ParseProcStatLine(line);

        // Assert
        Assert.Equal(new long[] { 50, 0, 50, 800, 0 }, result);
    }

    // ─── TryParseMemInfo ────────────────────────────────────────────────────

    [Fact]
    public void TryParseMemInfo_WithValidLines_ReturnsTrueAndValues()
    {
        // Arrange
        var lines = new[]
        {
            "MemTotal:        8192000 kB",
            "MemFree:          512000 kB",
            "MemAvailable:    2048000 kB",
            "Buffers:          128000 kB",
        };

        // Act
        var ok = LinuxSystemMetricsService.TryParseMemInfo(lines, out var total, out var avail);

        // Assert
        Assert.True(ok);
        Assert.Equal(8192000L, total);
        Assert.Equal(2048000L, avail);
    }

    [Fact]
    public void TryParseMemInfo_StopsAfterFindingBothKeys()
    {
        // Arrange: bad line after the two keys must not be reached/parsed
        var lines = new[]
        {
            "MemTotal:        8192000 kB",
            "MemAvailable:    2048000 kB",
            "BadEntry:        not_a_number kB",
        };

        // Act
        var ok = LinuxSystemMetricsService.TryParseMemInfo(lines, out var total, out var avail);

        // Assert
        Assert.True(ok);
        Assert.Equal(8192000L, total);
        Assert.Equal(2048000L, avail);
    }

    [Fact]
    public void TryParseMemInfo_WhenMissingMemTotal_ReturnsFalse()
    {
        // Arrange
        var lines = new[] { "MemAvailable: 2048000 kB" };

        // Act
        var ok = LinuxSystemMetricsService.TryParseMemInfo(lines, out _, out _);

        // Assert
        Assert.False(ok);
    }

    [Fact]
    public void TryParseMemInfo_WhenMissingMemAvailable_ReturnsFalse()
    {
        // Arrange
        var lines = new[] { "MemTotal: 8192000 kB" };

        // Act
        var ok = LinuxSystemMetricsService.TryParseMemInfo(lines, out _, out _);

        // Assert
        Assert.False(ok);
    }

    [Fact]
    public void TryParseMemInfo_EmptyLines_ReturnsFalse()
    {
        // Arrange
        var lines = Array.Empty<string>();

        // Act
        var ok = LinuxSystemMetricsService.TryParseMemInfo(lines, out _, out _);

        // Assert
        Assert.False(ok);
    }

    // ─── CalculateCpu ───────────────────────────────────────────────────────

    [Fact]
    public void CalculateCpu_WhenHalfIdle_Returns50Percent()
    {
        // Arrange
        // v1: user=100, nice=0, system=100, idle=200, iowait=0 → total=400, idle=200
        // v2: user=200, nice=0, system=200, idle=400, iowait=0 → total=800, idle=400
        // deltaTotal=400, deltaIdle=200 → cpu=50%
        var v1 = new long[] { 100, 0, 100, 200, 0, 0, 0, 0 };
        var v2 = new long[] { 200, 0, 200, 400, 0, 0, 0, 0 };

        // Act
        var result = LinuxSystemMetricsService.CalculateCpu(v1, v2);

        // Assert
        Assert.Equal(50.0, result);
    }

    [Fact]
    public void CalculateCpu_WhenFullyBusy_Returns100Percent()
    {
        // Arrange
        // All delta is user time, idle stays 0 → cpu=100%
        var v1 = new long[] { 0, 0, 0, 0, 0 };
        var v2 = new long[] { 100, 0, 0, 0, 0 };

        // Act
        var result = LinuxSystemMetricsService.CalculateCpu(v1, v2);

        // Assert
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void CalculateCpu_WhenFullyIdle_ReturnsZero()
    {
        // Arrange
        // All delta is idle → cpu=0%
        var v1 = new long[] { 0, 0, 0, 0, 0 };
        var v2 = new long[] { 0, 0, 0, 100, 0 };

        // Act
        var result = LinuxSystemMetricsService.CalculateCpu(v1, v2);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CalculateCpu_WhenDeltaTotalIsZero_ReturnsZero()
    {
        // Arrange
        var v1 = new long[] { 100, 0, 100, 200, 0 };
        var v2 = new long[] { 100, 0, 100, 200, 0 };

        // Act
        var result = LinuxSystemMetricsService.CalculateCpu(v1, v2);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CalculateCpu_IowaitCountsAsIdle()
    {
        // Arrange
        // idle=100, iowait=100 → totalIdle=200, deltaTotal=200 → cpu=0%
        var v1 = new long[] { 0, 0, 0, 0, 0 };
        var v2 = new long[] { 0, 0, 0, 100, 100 };

        // Act
        var result = LinuxSystemMetricsService.CalculateCpu(v1, v2);

        // Assert
        Assert.Equal(0.0, result);
    }
}

public sealed class WindowsSystemMetricsServiceTests
{
    // ─── CalculateCpu ───────────────────────────────────────────────────────

    [Fact]
    public void CalculateCpu_WhenHalfIdle_Returns50Percent()
    {
        // Arrange
        // kernel includes idle; t1: idle=200, kernel=300(idle+sys), user=100 → total=400
        //                       t2: idle=400, kernel=600,           user=200 → total=800
        // deltaTotal=400, deltaIdle=200 → cpu=50%

        // Act
        var result = WindowsSystemMetricsService.CalculateCpu(
            idle1: 200, kernel1: 300, user1: 100,
            idle2: 400, kernel2: 600, user2: 200);

        // Assert
        Assert.Equal(50.0, result);
    }

    [Fact]
    public void CalculateCpu_WhenFullyBusy_Returns100Percent()
    {
        // Arrange
        // idle stays 0; all delta is kernel/user busy → cpu=100%

        // Act
        var result = WindowsSystemMetricsService.CalculateCpu(
            idle1: 0, kernel1: 0, user1: 0,
            idle2: 0, kernel2: 100, user2: 100);

        // Assert
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void CalculateCpu_WhenDeltaTotalIsZero_ReturnsZero()
    {
        // Arrange & Act
        var result = WindowsSystemMetricsService.CalculateCpu(
            idle1: 100, kernel1: 100, user1: 100,
            idle2: 100, kernel2: 100, user2: 100);

        // Assert
        Assert.Equal(0.0, result);
    }
}

public sealed class SystemMetricsCalculationsTests
{
    // ─── CalculateRam ───────────────────────────────────────────────────────

    [Fact]
    public void CalculateRam_ReturnsCorrectMb()
    {
        // Arrange: 8 GB total, 2 GB available → 6 GB used
        var totalKb = 8L * 1024 * 1024;
        var availKb = 2L * 1024 * 1024;

        // Act
        var (used, total) = SystemMetricsCalculations.CalculateRam(totalKb, availKb);

        // Assert
        Assert.Equal(6L * 1024, used);  // 6144 MB
        Assert.Equal(8L * 1024, total); // 8192 MB
    }

    [Fact]
    public void CalculateRam_WhenAvailEqualsTotal_UsedIsZero()
    {
        // Arrange
        var totalKb = 4L * 1024 * 1024;

        // Act
        var (used, total) = SystemMetricsCalculations.CalculateRam(totalKb, availKb: totalKb);

        // Assert
        Assert.Equal(0L, used);
        Assert.Equal(4L * 1024, total);
    }

    // ─── CalculateDisk ──────────────────────────────────────────────────────

    [Fact]
    public void CalculateDisk_ReturnsCorrectGb()
    {
        // Arrange: 120 GB total, 20 GB free → 100 GB used
        const long gb = 1024L * 1024 * 1024;

        // Act
        var (used, total) = SystemMetricsCalculations.CalculateDisk(
            totalBytes: 120L * gb,
            freeBytes: 20L * gb);

        // Assert
        Assert.Equal(100.0, used);
        Assert.Equal(120.0, total);
    }

    [Fact]
    public void CalculateDisk_RoundsToTwoDecimalPlaces()
    {
        // Arrange: 1.5 GB total, 0.25 GB free → 1.25 GB used
        const long gb = 1024L * 1024 * 1024;

        // Act
        var (used, total) = SystemMetricsCalculations.CalculateDisk(
            totalBytes: (long)(1.5 * gb),
            freeBytes: (long)(0.25 * gb));

        // Assert
        Assert.Equal(1.25, used);
        Assert.Equal(1.5, total);
    }
}

public sealed class SystemMetricsServiceIntegrationTests
{
    // ─── Smoke test (roda no SO actual) ─────────────────────────────────────

    [Fact]
    public async Task GetMetricsAsync_ReturnsValuesInReasonableRange()
    {
        // Arrange
        ISystemMetricsService service = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new WindowsSystemMetricsService(NullLogger<WindowsSystemMetricsService>.Instance)
            : new LinuxSystemMetricsService(NullLogger<LinuxSystemMetricsService>.Instance);

        // Act
        var metrics = await service.GetMetricsAsync();

        // Assert
        Assert.InRange(metrics.CpuPercent, 0.0, 100.0);
        Assert.True(metrics.RamUsedMb > 0, "RAM used should be > 0");
        Assert.True(metrics.RamTotalMb > metrics.RamUsedMb, "Total RAM should exceed used");
        Assert.True(metrics.DiskTotalGb > 0, "Disk total should be > 0");
        Assert.True(metrics.DiskUsedGb >= 0, "Disk used should be >= 0");
        Assert.True(metrics.DiskTotalGb >= metrics.DiskUsedGb, "Disk total should be >= used");
    }
}
