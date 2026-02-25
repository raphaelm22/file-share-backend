namespace FileShare.Features.Download.GetShareInfo;

public sealed record ShareInfoResponse(
    string FileName,
    long FileSize,
    DateTime? ExpiresAt,
    DateTime CreatedAt);
