namespace FileShare.Features.Shares.CreateShare;

public sealed record CreateShareCommand(string FilePath, int? TtlHours);
