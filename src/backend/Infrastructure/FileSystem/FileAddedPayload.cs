namespace FileShare.Infrastructure.FileSystem;

public sealed record FileAddedPayload(string FileName, long FileSize, DateTimeOffset ModifiedAt);
