using System.Collections.Concurrent;

namespace FileShare.Infrastructure.FileSystem;

public sealed class FileStateTracker
{
    readonly ConcurrentDictionary<string, byte> _files =
        new(StringComparer.OrdinalIgnoreCase);

    public void Initialize(IEnumerable<string> fileNames)
    {
        _files.Clear();
        foreach (var name in fileNames)
            _files.TryAdd(name, 0);
    }

    public bool TryAdd(string fileName) => _files.TryAdd(fileName, 0);

    public bool TryRemove(string fileName) => _files.TryRemove(fileName, out _);

    public IReadOnlyCollection<string> CurrentFiles => _files.Keys.ToArray();
}
