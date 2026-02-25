using Microsoft.AspNetCore.SignalR;
using FileShare.Infrastructure.Hubs;

namespace FileShare.Infrastructure.FileSystem;

public sealed class FileSystemWatcherService : BackgroundService
{
    readonly IHubContext<FileShareHub> _hub;
    readonly IConfiguration _config;
    readonly ILogger<FileSystemWatcherService> _logger;
    readonly FileStateTracker _tracker;

    public FileSystemWatcherService(
        IHubContext<FileShareHub> hub,
        IConfiguration config,
        ILogger<FileSystemWatcherService> logger,
        FileStateTracker tracker)
    {
        _hub = hub;
        _config = config;
        _logger = logger;
        _tracker = tracker;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var folder = _config.GetValue<string>("MonitoredFolder") ?? "/app/shared-files";

        try
        {
            if (!Directory.Exists(folder))
            {
                _logger.LogWarning("FSW: Monitored folder not found, watcher not started: {Folder}", folder);
                return Task.CompletedTask;
            }

            var watcher = new FileSystemWatcher(folder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            watcher.Created += OnFileCreated;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnWatcherError;

            stoppingToken.Register(() =>
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _logger.LogInformation("FSW: FileSystemWatcher disposed");
            });

            _logger.LogInformation("FSW: FileSystemWatcher started on {Folder}", folder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FSW: FileSystemWatcher failed to start (non-fatal — polling handles detection)");
        }

        return Task.CompletedTask;
    }

    internal void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        var fileName = Path.GetFileName(e.FullPath);
        if (fileName.StartsWith('.')) return;

        if (!_tracker.TryAdd(fileName)) return;

        var info = new FileInfo(e.FullPath);
        if (!info.Exists) { _tracker.TryRemove(fileName); return; }

        _logger.LogInformation("FSW: FileAdded: {FileName}", fileName);
        _ = _hub.Clients.All.SendAsync("FileAdded",
            new FileAddedPayload(info.Name, info.Length, new DateTimeOffset(info.LastWriteTimeUtc)));
    }

    internal void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        var fileName = Path.GetFileName(e.FullPath);
        if (fileName.StartsWith('.')) return;

        if (!_tracker.TryRemove(fileName)) return;

        _logger.LogInformation("FSW: FileRemoved: {FileName}", fileName);
        _ = _hub.Clients.All.SendAsync("FileRemoved", new FileRemovedPayload(fileName));
    }

    internal void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        OnFileDeleted(sender, new FileSystemEventArgs(
            WatcherChangeTypes.Deleted,
            Path.GetDirectoryName(e.OldFullPath)!,
            e.OldName!));

        OnFileCreated(sender, new FileSystemEventArgs(
            WatcherChangeTypes.Created,
            Path.GetDirectoryName(e.FullPath)!,
            e.Name!));
    }

    void OnWatcherError(object sender, ErrorEventArgs e) =>
        _logger.LogWarning(e.GetException(), "FSW: FileSystemWatcher error (non-fatal — polling handles detection)");
}
