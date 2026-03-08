using Ardalis.Specification;
using FileShare.Domain;
using Microsoft.AspNetCore.StaticFiles;
using Wolverine.Http;

namespace FileShare.Features.Download.DownloadFile;

public static class DownloadFileEndpoint
{
    [WolverineGet("/dl/{token}/file")]
    public static async Task<IResult> Handle(
        string token,
        IReadRepositoryBase<Share> repo,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(DownloadFileEndpoint));

        var (share, error) = await ShareValidation.ValidateToken(token, repo, logger, ct);
        if (error is not null)
            return error;

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(share!.FileName, out var contentType))
            contentType = "application/octet-stream";

        logger.LogInformation("Serving file download for token: {Token}, file: {FileName}", token, share.FileName);

        return TypedResults.PhysicalFile(
            share.FilePath,
            contentType,
            share.FileName,
            enableRangeProcessing: true);
    }
}
