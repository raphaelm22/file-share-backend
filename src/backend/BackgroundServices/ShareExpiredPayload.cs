namespace FileShare.BackgroundServices;

public sealed record ShareExpiredPayload(string Id, string FileName);
