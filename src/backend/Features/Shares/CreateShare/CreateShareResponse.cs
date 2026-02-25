namespace FileShare.Features.Shares.CreateShare;

public sealed record CreateShareResponse(
    string Id,
    string Token,
    string FileName,
    long FileSize,
    DateTime? ExpiresAt,
    DateTime CreatedAt);
