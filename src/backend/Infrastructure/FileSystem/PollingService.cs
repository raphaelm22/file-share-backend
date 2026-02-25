using Microsoft.AspNetCore.SignalR;
using FileShare.Infrastructure.Hubs;

namespace FileShare.Infrastructure.FileSystem;

public sealed class PollingService : BackgroundService
{
    readonly IHubContext<FileShareHub> _hub;
    readonly IConfiguration _config;
    readonly ILogger<PollingService> _logger;
    readonly FileStateTracker _tracker;

    public PollingService(
        IHubContext<FileShareHub> hub,
        IConfiguration config,
        ILogger<PollingService> logger,
        FileStateTracker tracker)
    {
        _hub = hub;
        _config = config;
        _logger = logger;
        _tracker = tracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var folder = _config.GetValue<string>("MonitoredFolder") ?? "/app/shared-files";
        _logger.LogInformation("PollingService starting. Folder: {Folder}", folder);

        if (Directory.Exists(folder))
        {
            try
            {
                _tracker.Initialize(GetVisibleFileNames(folder));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PollingService: Failed to initialize tracker from {Folder} (non-fatal)", folder);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            await PollOnceAsync(folder, stoppingToken);
        }
    }

    internal async Task PollOnceAsync(string folder, CancellationToken ct)
    {
        if (!Directory.Exists(folder))
        {
            _logger.LogWarning("Monitored folder not found during poll: {Folder}", folder);
            return;
        }

        try
        {
            var currentFiles = GetVisibleFileNames(folder).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var trackedFiles = _tracker.CurrentFiles;

            foreach (var fileName in currentFiles.Where(f => !trackedFiles.Contains(f, StringComparer.OrdinalIgnoreCase)))
            {
                if (!_tracker.TryAdd(fileName)) continue;
                var path = Path.Combine(folder, fileName);
                var info = new FileInfo(path);
                if (!info.Exists) { _tracker.TryRemove(fileName); continue; }
                _logger.LogInformation("FileAdded detected by polling: {FileName}", fileName);
                await _hub.Clients.All.SendAsync("FileAdded",
                    new FileAddedPayload(info.Name, info.Length, new DateTimeOffset(info.LastWriteTimeUtc)), ct);
            }

            foreach (var fileName in _tracker.CurrentFiles.Where(f => !currentFiles.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList())
            {
                if (!_tracker.TryRemove(fileName)) continue;
                _logger.LogInformation("FileRemoved detected by polling: {FileName}", fileName);
                await _hub.Clients.All.SendAsync("FileRemoved", new FileRemovedPayload(fileName), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during polling cycle for {Folder}", folder);
        }
    }

    static IEnumerable<string> GetVisibleFileNames(string folder) =>
        Directory.GetFiles(folder)
            .Select(p => Path.GetFileName(p)!)
            .Where(name => !name.StartsWith('.'));
}
