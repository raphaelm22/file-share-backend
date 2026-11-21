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

    // Symlinked directories are skipped: following them can loop forever or leave the root
    static readonly EnumerationOptions TreeOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
    };

    static DirectoryNode BuildTree(string absolutePath, string relativePath)
    {
        var name = Path.GetFileName(absolutePath) is { Length: > 0 } n ? n : absolutePath;
        var subdirs = Directory.GetDirectories(absolutePath, "*", TreeOptions)
            .Where(d => !Path.GetFileName(d).StartsWith('.'))
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
