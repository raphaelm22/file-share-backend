using Ardalis.Specification;
using FileShare.Domain;

namespace FileShare.Features.Download.GetShareInfo;

public sealed class ShareByTokenSpec : SingleResultSpecification<Share>
{
    public ShareByTokenSpec(string token)
        => Query.Where(s => s.Token == token);
}
