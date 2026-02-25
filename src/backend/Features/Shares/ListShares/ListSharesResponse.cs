namespace FileShare.Features.Shares.ListShares;

public sealed record ListSharesResponse(
    string Id,
    string Token,
    string FileName,
    long FileSize,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    string Status);
