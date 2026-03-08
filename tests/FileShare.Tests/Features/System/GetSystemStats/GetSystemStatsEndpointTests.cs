using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using FileShare.Features.System.GetSystemStats;
using FileShare.Infrastructure.Hubs;
using FileShare.Infrastructure.System;
using FileShare.Tests.Infrastructure.FileSystem; // TestHubContext

namespace FileShare.Tests.Features.System.GetSystemStats;

public sealed class GetSystemStatsEndpointTests
{
    // ─── Helpers ───────────────────────────────────────────────────────────

    static FakeSystemMetricsService DefaultFake() =>
        new(new SystemMetrics(
            CpuPercent: 42.5,
            RamUsedMb: 1024,
            RamTotalMb: 4096,
            DiskUsedGb: 12.34,
            DiskTotalGb: 119.24));

    static Task<SystemStatsResponse> CallHandle(
        ISystemMetricsService fake,
        IHubContext<FileShareHub> hub) =>
        GetSystemStatsEndpoint.Handle(
            new GetSystemStatsQuery(),
            fake,
            hub,
            NullLoggerFactory.Instance,
            CancellationToken.None);

    // ─── Testes ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsCorrectResponseFields()
    {
        // Arrange
        var fake = DefaultFake();
        var hub = new TestHubContext();

        // Act
        var response = await CallHandle(fake, hub);

        // Assert
        Assert.Equal(42.5, response.CpuPercent);
        Assert.Equal(1024L, response.RamUsedMb);
        Assert.Equal(4096L, response.RamTotalMb);
        Assert.Equal(12.34, response.DiskUsedGb);
        Assert.Equal(119.24, response.DiskTotalGb);
    }

    [Fact]
    public async Task Handle_BroadcastsSystemStatsEvent()
    {
        // Arrange
        var fake = DefaultFake();
        var hub = new TestHubContext();

        // Act
        await CallHandle(fake, hub);

        // Assert
        Assert.Single(hub.SentMessages);
        Assert.Equal("SystemStats", hub.SentMessages[0].Method);
    }

    [Fact]
    public async Task Handle_BroadcastPayloadMatchesResponse()
    {
        // Arrange
        var fake = DefaultFake();
        var hub = new TestHubContext();

        // Act
        var response = await CallHandle(fake, hub);

        // Assert
        var payload = Assert.IsType<SystemStatsResponse>(hub.SentMessages[0].Arg);
        Assert.Equal(response, payload);
    }

    [Fact]
    public async Task Handle_WithZeroCpu_ReturnsZeroCpuPercent()
    {
        // Arrange
        var fake = new FakeSystemMetricsService(
            new SystemMetrics(0.0, 512, 8192, 5.0, 119.24));
        var hub = new TestHubContext();

        // Act
        var response = await CallHandle(fake, hub);

        // Assert
        Assert.Equal(0.0, response.CpuPercent);
    }

    [Fact]
    public async Task Handle_CallsGetMetricsAsyncOnce()
    {
        // Arrange
        var fake = DefaultFake();
        var hub = new TestHubContext();

        // Act
        await CallHandle(fake, hub);

        // Assert
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task Handle_WhenHubThrows_StillReturnsResponse()
    {
        // Arrange
        var fake = DefaultFake();
        var hub = new ThrowingHubContext();

        // Act
        var response = await CallHandle(fake, hub);

        // Assert
        Assert.Equal(42.5, response.CpuPercent);
    }

    [Fact]
    public async Task Handle_WhenMetricsThrows_PropagatesException()
    {
        // Arrange
        var fake = new ThrowingSystemMetricsService();
        var hub = new TestHubContext();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CallHandle(fake, hub));
    }
}

// ─── Test Doubles ──────────────────────────────────────────────────────────

sealed class FakeSystemMetricsService : ISystemMetricsService
{
    readonly SystemMetrics _metrics;
    public int CallCount { get; private set; }

    public FakeSystemMetricsService(SystemMetrics metrics) => _metrics = metrics;

    public Task<SystemMetrics> GetMetricsAsync(CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(_metrics);
    }
}

sealed class ThrowingSystemMetricsService : ISystemMetricsService
{
    public Task<SystemMetrics> GetMetricsAsync(CancellationToken ct = default) =>
        Task.FromException<SystemMetrics>(new InvalidOperationException("Simulated metrics failure"));
}

sealed class ThrowingHubContext : IHubContext<FileShareHub>
{
    public IHubClients Clients => new ThrowingHubClients();
    public IGroupManager Groups => throw new NotImplementedException();
}

sealed class ThrowingHubClients : IHubClients
{
    public IClientProxy All => new ThrowingClientProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Client(string connectionId) => throw new NotImplementedException();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
    public IClientProxy Group(string groupName) => throw new NotImplementedException();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
    public IClientProxy User(string userId) => throw new NotImplementedException();
    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
}

sealed class ThrowingClientProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Simulated SignalR failure"));
}
