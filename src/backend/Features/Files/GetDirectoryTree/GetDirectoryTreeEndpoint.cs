using Wolverine.Http;

namespace FileShare.Features.Files.GetDirectoryTree;

public static class GetDirectoryTreeEndpoint
{
    [WolverineGet("/api/v1/directories")]
    public static GetDirectoryTreeResponse Handle(
        IConfiguration config,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetDirectoryTreeEndpoint));
        var rootPath = config.GetValue<string>("MonitoredFolder") ?? "/app/shared-files";

        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Monitored folder not found: {rootPath}");

        logger.LogInformation("Building directory tree for: {RootPath}", rootPath);

        var root = BuildTree(rootPath, "");
        return new GetDirectoryTreeResponse(root);
    }

    static DirectoryNode BuildTree(string absolutePath, string relativePath)
    {
        var name = Path.GetFileName(absolutePath) is { Length: > 0 } n ? n : absolutePath;
        var subdirs = Directory.GetDirectories(absolutePath)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                var dirName = Path.GetFileName(d);
                var childRelative = relativePath.Length == 0 ? dirName : $"{relativePath}/{dirName}";
                return BuildTree(d, childRelative);
            })
            .ToList();
        return new DirectoryNode(name, relativePath, subdirs);
    }
}
