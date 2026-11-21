using Ardalis.Specification;
using FileShare.Domain;
using FileShare.Infrastructure.Security;

namespace FileShare.Features.Download.GetShareInfo;

public sealed class ShareByTokenSpec : SingleResultSpecification<Share>
{
    public ShareByTokenSpec(string token)
    {
        var hash = TokenHashing.Hash(token);
        Query.Where(s => s.TokenHash == hash);
    }
}
