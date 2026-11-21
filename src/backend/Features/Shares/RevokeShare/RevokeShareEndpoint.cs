using Ardalis.Specification;
using FileShare.Domain;
using FileShare.Infrastructure.Security;
using Wolverine.Http;

namespace FileShare.Features.Shares.RevokeShare;

public static class RevokeShareEndpoint
{
    /// <summary>Deletes a share so its link stops working immediately.</summary>
    [WolverineDelete("/api/v1/shares/{id}")]
    public static async Task<IResult> Handle(
        string id,
        IRepositoryBase<Share> repo,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(RevokeShareEndpoint));

        var share = await repo.GetByIdAsync(id, ct);
        if (share is null)
            return TypedResults.NotFound();

        await repo.DeleteAsync(share, ct);
        logger.LogInformation("Share revoked: token={Token} file={FileName}",
            TokenFormat.Redact(share.Token), share.FileName);

        return TypedResults.NoContent();
    }
}
