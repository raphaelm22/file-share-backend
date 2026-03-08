namespace FileShare.Infrastructure.System;

public interface ISystemMetricsService
{
    Task<SystemMetrics> GetMetricsAsync(CancellationToken ct = default);
}
