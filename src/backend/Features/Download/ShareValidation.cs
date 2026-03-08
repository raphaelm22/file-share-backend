using Ardalis.Specification;
using FileShare.Domain;
using FileShare.Features.Download.GetShareInfo;

namespace FileShare.Features.Download;

public static class ShareValidation
{
    public static async Task<(Share? Share, IResult? Error)> ValidateToken(
        string token,
        IReadRepositoryBase<Share> repo,
        ILogger logger,
        CancellationToken ct)
    {
        var share = await repo.SingleOrDefaultAsync(new ShareByTokenSpec(token), ct);
        if (share is null)
        {
            logger.LogWarning("Token not found: {Token}", token);
            return (null, TokenNotFound());
        }

        var utcExpiresAt = share.ExpiresAt.HasValue
            ? DateTime.SpecifyKind(share.ExpiresAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        if (utcExpiresAt.HasValue && utcExpiresAt.Value <= DateTime.UtcNow)
        {
            logger.LogWarning("Token expired: {Token}", token);
            return (null, ShareExpired());
        }

        if (!File.Exists(share.FilePath))
        {
            logger.LogWarning("File not found on disk for token: {Token}", token);
            return (null, FileNotFound());
        }

        return (share, null);
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
