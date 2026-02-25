using Wolverine.Http;

namespace FileShare.Features.Files.ListFiles;

public static class ListFilesEndpoint
{
    [WolverineGet("/api/v1/files")]
    public static ListFilesResponse[] Handle(
        ListFilesQuery query,
        IConfiguration config,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(ListFilesEndpoint));
        var folder = config.GetValue<string>("MonitoredFolder") ?? "/app/shared-files";

        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Monitored folder not found: {folder}");

        logger.LogInformation("Listing files in monitored folder: {Folder}", folder);

        return Directory.GetFiles(folder)
            .Where(path => !Path.GetFileName(path).StartsWith('.'))
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists)
            .Select(info => new ListFilesResponse(info.FullName, info.Name, info.Length, new DateTimeOffset(info.LastWriteTimeUtc)))
            .ToArray();
    }
}
