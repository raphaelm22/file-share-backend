using System.Text.RegularExpressions;

namespace FileShare.Infrastructure.Security;

public static partial class TokenFormat
{
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool IsValid(string? token) => token is not null && Pattern().IsMatch(token);

    /// <summary>Short prefix for logs — never log full tokens (they are bearer credentials).</summary>
    public static string Redact(string? token) =>
        string.IsNullOrEmpty(token) ? "<empty>" : token.Length <= 8 ? "***" : $"{token[..8]}…";
}
