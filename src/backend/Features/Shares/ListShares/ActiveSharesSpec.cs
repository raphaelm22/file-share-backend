using Ardalis.Specification;
using FileShare.Domain;

namespace FileShare.Features.Shares.ListShares;

/// <summary>
/// Retrieves ALL shares regardless of status, ordered by CreatedAt DESC.
/// Status (active/expired/file-removed) is computed in-memory by <see cref="ListSharesEndpoint"/>.
/// The name refers to the Admin SPA "active shares screen", not the "active" status value.
/// </summary>
public sealed class ActiveSharesSpec : Specification<Share>
{
    public ActiveSharesSpec()
        => Query.OrderByDescending(s => s.CreatedAt);
}
