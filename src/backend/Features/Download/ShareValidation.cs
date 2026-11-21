using Ardalis.Specification;
using FileShare.Domain;
using FileShare.Features.Download.GetShareInfo;
using FileShare.Infrastructure.Security;

namespace FileShare.Features.Download;

public static class ShareValidation
{
    public static async Task<(Share? Share, string ResolvedPath, IResult? Error)> ValidateToken(
        string token,
        IReadRepositoryBase<Share> repo,
        MonitoredFolderGuard folderGuard,
        ILogger logger,
        CancellationToken ct)
    {
        if (!TokenFormat.IsValid(token))
        {
            logger.LogWarning("Malformed token rejected: {Token}", TokenFormat.Redact(token));
            return (null, string.Empty, TokenNotFound());
        }

        var share = await repo.SingleOrDefaultAsync(new ShareByTokenSpec(token), ct);
        if (share is null)
        {
            logger.LogWarning("Token not found: {Token}", TokenFormat.Redact(token));
            return (null, string.Empty, TokenNotFound());
        }

        var utcExpiresAt = share.ExpiresAt.HasValue
            ? DateTime.SpecifyKind(share.ExpiresAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        if (utcExpiresAt.HasValue && utcExpiresAt.Value <= DateTime.UtcNow)
        {
            logger.LogWarning("Token expired: {Token}", TokenFormat.Redact(token));
            return (null, string.Empty, ShareExpired());
        }

        // Re-checked on every request: covers old rows and symlinks swapped after creation.
        // Callers must serve resolvedPath (no symlink components), never share.FilePath.
        if (!folderGuard.TryResolve(share.FilePath, out var resolvedPath))
        {
            logger.LogWarning("File missing or outside monitored folder for token: {Token}", TokenFormat.Redact(token));
            return (null, string.Empty, FileNotFound());
        }

        // The link is bound to the file that was shared, not to its path: a different file later
        // placed at the same path must not be served on an old token.
        if (!MatchesSharedFile(share, new FileInfo(resolvedPath)))
        {
            logger.LogWarning("Shared file was replaced for token: {Token}", TokenFormat.Redact(token));
            return (null, string.Empty, FileNotFound());
        }

        return (share, resolvedPath, null);
    }

    /// <summary>
    /// True when the file on disk is still the one captured at share creation (size + last write).
    /// Shares created before the snapshot existed (FileLastWriteUtc null) are not checked.
    /// </summary>
    public static bool MatchesSharedFile(Share share, FileInfo file)
    {
        if (!share.FileLastWriteUtc.HasValue)
            return true;

        var expected = DateTime.SpecifyKind(share.FileLastWriteUtc.Value, DateTimeKind.Utc);
        return file.Length == share.FileSize && file.LastWriteTimeUtc == expected;
    }

    public static DateTime? ToUtcExpiresAt(Share share)
    {
        return share.ExpiresAt.HasValue
            ? DateTime.SpecifyKind(share.ExpiresAt.Value, DateTimeKind.Utc)
            : null;
    }

    static IResult TokenNotFound() => TypedResults.Problem(
        detail: "TOKEN_NOT_FOUND",
        statusCode: 404,
        title: "Share not found",
        type: "/errors/token-not-found");

    static IResult ShareExpired() => TypedResults.Problem(
        detail: "SHARE_EXPIRED_OR_INVALID",
        statusCode: 404,
        title: "Share expired or invalid",
        type: "/errors/share-expired-or-invalid");

    static IResult FileNotFound() => TypedResults.Problem(
        detail: "FILE_NOT_FOUND",
        statusCode: 404,
        title: "File not found",
        type: "/errors/file-not-found");
}
