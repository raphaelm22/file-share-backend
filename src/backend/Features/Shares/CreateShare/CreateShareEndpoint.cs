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
        MonitoredFolderGuard folderGuard,
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
                type: "/errors/invalid-ttl");
        }

        // Same answer for "missing" and "outside monitored folder": don't reveal what exists on disk
        if (!folderGuard.TryResolve(command.FilePath, out var filePath))
        {
            logger.LogWarning("Rejected share creation for path: {FilePath}", command.FilePath);
            return TypedResults.Problem(
                detail: "FILE_NOT_FOUND",
                statusCode: 422,
                title: "File not found",
                type: "/errors/file-not-found");
        }

        // filePath is the resolved file; size and last-write identify it (link sizes would be wrong).
        // Keep the name the admin picked, which may be an in-root alias.
        var fileInfo = new FileInfo(filePath);
        var requestedName = Path.GetFileName(Path.GetFullPath(command.FilePath!));
        var token = tokenService.Generate();
        DateTime? expiresAt = command.TtlHours.HasValue
            ? DateTime.UtcNow.AddHours(command.TtlHours.Value)
            : null;

        var share = new Share
        {
            TokenHash = TokenHashing.Hash(token),
            FilePath = filePath,
            FileName = requestedName,
            FileSize = fileInfo.Length,
            FileLastWriteUtc = fileInfo.LastWriteTimeUtc,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        };

        await repo.AddAsync(share, ct);

        logger.LogInformation("Share created: token={Token} file={FileName} expiresAt={ExpiresAt}",
            TokenFormat.Redact(token), share.FileName, share.ExpiresAt?.ToString("O") ?? "never");

        return TypedResults.Created(
            $"/api/v1/shares/{share.Id}",
            new CreateShareResponse(
                share.Id,
                token, // returned once, at creation; only the hash is stored
                share.FileName,
                share.FileSize,
                share.ExpiresAt,
                share.CreatedAt));
    }
}
