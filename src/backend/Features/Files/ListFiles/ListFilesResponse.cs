namespace FileShare.Features.Files.ListFiles;

public sealed record ListFilesResponse(string FilePath, string FileName, long FileSize, DateTimeOffset ModifiedAt, string Directory);
