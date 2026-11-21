namespace FileShare.Domain;

public sealed class Share
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>SHA-256 of the share token (<see cref="Infrastructure.Security.TokenHashing"/>). The token itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    /// <summary>
    /// Identity snapshot of the shared file (with <see cref="FileSize"/>). A different file later
    /// placed at the same path must not be served on this token. Null for shares created before
    /// this column existed.
    /// </summary>
    public DateTime? FileLastWriteUtc { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
