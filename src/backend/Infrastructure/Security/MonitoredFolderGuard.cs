namespace FileShare.Infrastructure.Security;

/// <summary>
/// Ensures a path points to a file whose <b>real</b> location is inside the monitored folder.
/// Blocks absolute paths outside the root, "..", hidden segments and symlink chains that leave
/// the root (e.g. /etc/passwd, /proc/self/environ, the SQLite DB) — including links whose target
/// goes through another symlinked directory (x -> d/file with d -> /etc).
/// </summary>
public sealed class MonitoredFolderGuard
{
    const int MaxLinkHops = 40;

    static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    readonly string _root;

    public MonitoredFolderGuard(IConfiguration config)
        : this(config.GetValue<string>("MonitoredFolder") ?? "/app/shared-files") { }

    public MonitoredFolderGuard(string root)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        // Canonicalize the root too, so a root that lives under a symlinked path still matches
        _root = Path.TrimEndingDirectorySeparator(ResolveRealPath(full) ?? full);
    }

    /// <summary>
    /// On success <paramref name="fullPath"/> is the fully resolved path (no symlink components).
    /// Always open that path, never the caller-supplied one.
    /// </summary>
    public bool TryResolve(string? path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0'))
            return false;

        string lexical;
        try
        {
            lexical = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return false;
        }

        if (!IsUnderRoot(lexical) || HasHiddenSegment(lexical))
            return false;

        var real = ResolveRealPath(lexical);
        if (real is null || !IsUnderRoot(real) || HasHiddenSegment(real))
            return false;

        // File.Exists is false for directories and for dangling links
        if (!File.Exists(real))
            return false;

        fullPath = real;
        return true;
    }

    bool HasHiddenSegment(string fullPath) =>
        Path.GetRelativePath(_root, fullPath)
            .Split(Separators)
            .Any(segment => segment.StartsWith('.'));

    bool IsUnderRoot(string fullPath) =>
        fullPath.StartsWith(_root + Path.DirectorySeparatorChar, PathComparison);

    /// <summary>
    /// Walks the path one component at a time from the filesystem root. Whenever a component is a
    /// symlink, its target is substituted and the walk restarts, so links nested inside link targets
    /// are resolved as well. The result contains no symlink components. Returns null on loops,
    /// too many hops or I/O errors.
    /// </summary>
    static string? ResolveRealPath(string fullPath)
    {
        var pending = fullPath;
        var hops = 0;

        while (true)
        {
            var fsRoot = Path.GetPathRoot(pending);
            if (string.IsNullOrEmpty(fsRoot))
                return null;

            var segments = pending[fsRoot.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            var current = fsRoot;
            string? restartAt = null;

            for (var i = 0; i < segments.Length; i++)
            {
                var next = Path.Combine(current, segments[i]);
                string? target;
                try
                {
                    target = new FileInfo(next).LinkTarget;
                }
                catch (Exception)
                {
                    return null;
                }

                if (target is null)
                {
                    current = next;
                    continue;
                }

                if (++hops > MaxLinkHops)
                    return null;

                var baseDir = Path.IsPathRooted(target) ? target : Path.Combine(current, target);
                restartAt = Path.GetFullPath(Path.Combine([baseDir, .. segments[(i + 1)..]]));
                break;
            }

            if (restartAt is null)
                return current;

            pending = restartAt;
        }
    }
}
