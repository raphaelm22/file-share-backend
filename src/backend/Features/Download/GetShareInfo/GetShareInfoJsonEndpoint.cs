using Ardalis.Specification;
using FileShare.Domain;
using FileShare.Infrastructure.Security;
using Wolverine.Http;

namespace FileShare.Features.Download.GetShareInfo;

/// <summary>
/// Unambiguous JSON route used by the public SPA. /dl/{token} is also the URL of the HTML page,
/// and browsers can serve the cached page for a fetch to the same URL — a distinct path avoids it.
/// </summary>
public static class GetShareInfoJsonEndpoint
{
    [WolverineGet("/dl/{token}/info")]
    public static Task<IResult> Handle(
        string token,
        IReadRepositoryBase<Share> repo,
        MonitoredFolderGuard folderGuard,
        ILoggerFactory loggerFactory,
        CancellationToken ct) =>
        GetShareInfoEndpoint.Handle(token, repo, folderGuard, loggerFactory, ct);
}
