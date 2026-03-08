namespace FileShare.BackgroundServices;

public sealed record ShareExpiredPayload(string Token, string FileName);
