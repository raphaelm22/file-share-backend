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

        return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => (path, rel: Path.GetRelativePath(folder, path)))
            .Where(x => !x.rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.StartsWith('.')))
            .Select(x => (info: new FileInfo(x.path), x.rel))
            .Where(x => x.info.Exists)
            .Select(x =>
            {
                var dir = Path.GetDirectoryName(x.rel) ?? "";
                dir = dir.Replace('\\', '/');
                if (dir == ".") dir = "";
                return new ListFilesResponse(x.info.FullName, x.info.Name, x.info.Length, new DateTimeOffset(x.info.LastWriteTimeUtc), dir);
            })
            .ToArray();
    }
}
