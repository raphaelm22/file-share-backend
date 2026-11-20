namespace FileShare.Infrastructure.Security;

/// <summary>
/// Ensures a path points to a regular file inside the monitored folder.
/// Blocks absolute paths outside the root, "..", hidden segments and symlinks
/// that resolve outside the root (e.g. /etc/passwd, /proc/self/environ, the SQLite DB).
/// </summary>
public sealed class MonitoredFolderGuard
{
    static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    readonly string _root;

    public MonitoredFolderGuard(IConfiguration config)
        : this(config.GetValue<string>("MonitoredFolder") ?? "/app/shared-files") { }

    public MonitoredFolderGuard(string root)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    public bool TryResolve(string? path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0'))
            return false;

        string candidate;
        try
        {
            candidate = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return false;
        }

        if (!IsUnderRoot(candidate))
            return false;

        var relative = Path.GetRelativePath(_root, candidate);
        if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.StartsWith('.')))
            return false;

        var info = new FileInfo(candidate);
        if (!info.Exists)
            return false;

        // Every component (file and parent dirs) must resolve inside the root
        if (!LinksStayInsideRoot(candidate))
            return false;

        fullPath = candidate;
        return true;
    }

    bool LinksStayInsideRoot(string candidate)
    {
        try
        {
            FileSystemInfo? current = new FileInfo(candidate);
            while (current is not null && IsUnderRoot(current.FullName) &&
                   !string.Equals(current.FullName, _root, PathComparison))
            {
                if (current.LinkTarget is not null)
                {
                    var target = current.ResolveLinkTarget(returnFinalTarget: true);
                    if (target is null || !IsUnderRoot(Path.GetFullPath(target.FullName)))
                        return false;
                }
                current = current is FileInfo f ? f.Directory : ((DirectoryInfo)current).Parent;
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    bool IsUnderRoot(string fullPath) =>
        fullPath.StartsWith(_root + Path.DirectorySeparatorChar, PathComparison);
}
