using FileShare.Infrastructure.Security;

namespace FileShare.Tests.Infrastructure.Security;

public sealed class MonitoredFolderGuardTests : IDisposable
{
    readonly string _root;
    readonly string _outside;
    readonly MonitoredFolderGuard _guard;

    public MonitoredFolderGuardTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "guard-" + Guid.NewGuid().ToString("N"));
        _root = Path.Combine(baseDir, "shared");
        _outside = Path.Combine(baseDir, "secret");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
        _guard = new MonitoredFolderGuard(_root);
    }

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true);

    string Write(string dir, string relative)
    {
        var path = Path.Combine(dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void FileInsideRoot_IsAllowed()
    {
        var path = Write(_root, Path.Combine("sub", "file.txt"));
        Assert.True(_guard.TryResolve(path, out var full));
        Assert.Equal(Path.GetFullPath(path), full);
    }

    [Fact]
    public void FileOutsideRoot_IsRejected()
    {
        var path = Write(_outside, "db.sqlite");
        Assert.False(_guard.TryResolve(path, out _));
    }

    [Fact]
    public void DotDotTraversal_IsRejected()
    {
        Write(_outside, "db.sqlite");
        var path = Path.Combine(_root, "..", "secret", "db.sqlite");
        Assert.False(_guard.TryResolve(path, out _));
    }

    [Fact]
    public void SiblingFolderWithSamePrefix_IsRejected()
    {
        var sibling = _root + "-evil";
        var path = Write(sibling, "f.txt");
        try { Assert.False(_guard.TryResolve(path, out _)); }
        finally { Directory.Delete(sibling, true); }
    }

    [Fact]
    public void HiddenSegment_IsRejected()
    {
        var path = Write(_root, Path.Combine(".ssh", "id_rsa"));
        Assert.False(_guard.TryResolve(path, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/etc/passwd")]
    [InlineData("/proc/self/environ")]
    public void InvalidOrSystemPaths_AreRejected(string? path) =>
        Assert.False(_guard.TryResolve(path, out _));

    [Fact]
    public void RootDirectoryItself_IsRejected() =>
        Assert.False(_guard.TryResolve(_root, out _));

    [Fact]
    public void SymlinkPointingOutside_IsRejected()
    {
        var target = Write(_outside, "db.sqlite");
        var link = Path.Combine(_root, "innocent.txt");
        if (!TryCreateSymlink(() => File.CreateSymbolicLink(link, target))) return;
        Assert.False(_guard.TryResolve(link, out _));
    }

    [Fact]
    public void FileUnderSymlinkedDirectoryPointingOutside_IsRejected()
    {
        Write(_outside, "db.sqlite");
        var linkDir = Path.Combine(_root, "linked");
        if (!TryCreateSymlink(() => Directory.CreateSymbolicLink(linkDir, _outside))) return;
        Assert.False(_guard.TryResolve(Path.Combine(linkDir, "db.sqlite"), out _));
    }

    [Fact]
    public void SymlinkPointingInside_IsAllowed()
    {
        var target = Write(_root, "real.txt");
        var link = Path.Combine(_root, "alias.txt");
        if (!TryCreateSymlink(() => File.CreateSymbolicLink(link, target))) return;
        Assert.True(_guard.TryResolve(link, out _));
    }

    [Fact]
    public void SymlinkChainThroughSymlinkedDirectory_IsRejected()
    {
        // vd -> outside; x -> vd/db.sqlite : the final target string looks in-root but isn't
        Write(_outside, "db.sqlite");
        var vd = Path.Combine(_root, "vd");
        if (!TryCreateSymlink(() => Directory.CreateSymbolicLink(vd, _outside))) return;
        var x = Path.Combine(_root, "x");
        if (!TryCreateSymlink(() => File.CreateSymbolicLink(x, Path.Combine("vd", "db.sqlite")))) return;
        Assert.False(_guard.TryResolve(x, out _));
    }

    [Fact]
    public void SharedFileSwappedForIndirectLink_IsRejected()
    {
        Write(_outside, "db.sqlite");
        var shared = Write(_root, "plain.txt");
        Assert.True(_guard.TryResolve(shared, out _));

        var vd = Path.Combine(_root, "vd");
        if (!TryCreateSymlink(() => Directory.CreateSymbolicLink(vd, _outside))) return;
        File.Delete(shared);
        if (!TryCreateSymlink(() => File.CreateSymbolicLink(shared, Path.Combine("vd", "db.sqlite")))) return;
        Assert.False(_guard.TryResolve(shared, out _));
    }

    [Fact]
    public void SymlinkLoop_IsRejected()
    {
        var a = Path.Combine(_root, "a");
        var b = Path.Combine(_root, "b");
        if (!TryCreateSymlink(() => File.CreateSymbolicLink(a, b))) return;
        if (!TryCreateSymlink(() => File.CreateSymbolicLink(b, a))) return;
        Assert.False(_guard.TryResolve(a, out _));
    }

    [Fact]
    public void InRootAlias_ResolvesToRealFile()
    {
        var target = Write(_root, Path.Combine("docs", "real.txt"));
        var link = Path.Combine(_root, "alias.txt");
        if (!TryCreateSymlink(() => File.CreateSymbolicLink(link, Path.Combine("docs", "real.txt")))) return;
        Assert.True(_guard.TryResolve(link, out var full));
        Assert.Equal(new FileInfo(target).FullName, full, ignoreCase: OperatingSystem.IsWindows());
    }

    // Windows without developer mode can't create symlinks — skip silently there
    static bool TryCreateSymlink(Action create)
    {
        try { create(); return true; }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) when (OperatingSystem.IsWindows()) { return false; }
    }
}
