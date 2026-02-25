using Ardalis.Specification;
using Wolverine.Http;
using FileShare.Domain;
using FileShare.Infrastructure.Security;

namespace FileShare.Features.Shares.CreateShare;

public static class CreateShareEndpoint
{
    [WolverinePost("/api/v1/shares")]
    public static async Task<IResult> Handle(
        CreateShareCommand command,
        IRepositoryBase<Share> repo,
        SasTokenService tokenService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(CreateShareEndpoint));

        if (command.TtlHours.HasValue && command.TtlHours.Value <= 0)
        {
            logger.LogWarning("Invalid TtlHours for share creation: {TtlHours}", command.TtlHours);
            return TypedResults.Problem(
                detail: "TTL_MUST_BE_POSITIVE",
                statusCode: 422,
                title: "Invalid TTL",
                type: "https://fileshare.raphaelm22.net/errors/invalid-ttl");
        }

        if (!File.Exists(command.FilePath))
        {
            logger.LogWarning("File not found for share creation: {FilePath}", command.FilePath);
            return TypedResults.Problem(
                detail: "FILE_NOT_FOUND",
                statusCode: 422,
                title: "File not found",
                type: "https://fileshare.raphaelm22.net/errors/file-not-found");
        }

        var fileInfo = new FileInfo(command.FilePath);
        var token = tokenService.Generate();
        DateTime? expiresAt = command.TtlHours.HasValue
            ? DateTime.UtcNow.AddHours(command.TtlHours.Value)
            : null;

        var share = new Share
        {
            Token = token,
            FilePath = command.FilePath,
            FileName = fileInfo.Name,
            FileSize = fileInfo.Length,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        };

        await repo.AddAsync(share, ct);

        logger.LogInformation("Share created: token={Token} file={FileName} expiresAt={ExpiresAt}",
            token, share.FileName, share.ExpiresAt?.ToString("O") ?? "never");

        return TypedResults.Created(
            $"/api/v1/shares/{share.Id}",
            new CreateShareResponse(
                share.Id,
                share.Token,
                share.FileName,
                share.FileSize,
                share.ExpiresAt,
                share.CreatedAt));
    }
}
