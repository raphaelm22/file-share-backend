namespace FileShare.Features.Files.GetDirectoryTree;

public sealed record DirectoryNode(string Name, string Path, IReadOnlyList<DirectoryNode> Children);
