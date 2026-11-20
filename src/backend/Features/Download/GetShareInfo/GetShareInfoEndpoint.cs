using Ardalis.Specification;
using FileShare.Domain;
using FileShare.Infrastructure.Security;
using Wolverine.Http;

namespace FileShare.Features.Download.GetShareInfo;

public static class GetShareInfoEndpoint
{
    [WolverineGet("/dl/{token}")]
    public static async Task<IResult> Handle(
        string token,
        IReadRepositoryBase<Share> repo,
        MonitoredFolderGuard folderGuard,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetShareInfoEndpoint));

        var (share, error) = await ShareValidation.ValidateToken(token, repo, folderGuard, logger, ct);
        if (error is not null)
            return error;

        logger.LogInformation("Share info retrieved for token: {Token}", TokenFormat.Redact(token));
        return TypedResults.Ok(new ShareInfoResponse(
            share!.FileName,
            share.FileSize,
            ShareValidation.ToUtcExpiresAt(share),
            DateTime.SpecifyKind(share.CreatedAt, DateTimeKind.Utc)));
    }
}
