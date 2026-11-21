using Ardalis.Specification;
using Wolverine.Http;
using FileShare.Domain;
using FileShare.Features.Download;

namespace FileShare.Features.Shares.ListShares;

public static class ListSharesEndpoint
{
    [WolverineGet("/api/v1/shares")]
    public static async Task<ListSharesResponse[]> Handle(
        IReadRepositoryBase<Share> repo,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(ListSharesEndpoint));
        var shares = await repo.ListAsync(new ActiveSharesSpec(), ct);
        logger.LogInformation("Listing {Count} shares", shares.Count);
        return shares.Select(ToResponse).ToArray();
    }

    static ListSharesResponse ToResponse(Share share)
    {
        var utcExpiresAt = share.ExpiresAt.HasValue
            ? DateTime.SpecifyKind(share.ExpiresAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var status = ComputeStatus(share, utcExpiresAt);
        return new ListSharesResponse(
            share.Id,
            share.FileName,
            share.FileSize,
            utcExpiresAt,
            DateTime.SpecifyKind(share.CreatedAt, DateTimeKind.Utc),
            status);
    }

    static string ComputeStatus(Share share, DateTime? utcExpiresAt)
    {
        if (utcExpiresAt.HasValue && utcExpiresAt.Value <= DateTime.UtcNow)
            return "expired";
        var file = new FileInfo(share.FilePath);
        // A different file at the same path is not the shared one: report it as removed
        if (!file.Exists || !ShareValidation.MatchesSharedFile(share, file))
            return "file-removed";
        return "active";
    }
}
