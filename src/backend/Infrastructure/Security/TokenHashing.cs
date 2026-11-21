using System.Security.Cryptography;
using System.Text;

namespace FileShare.Infrastructure.Security;

/// <summary>
/// Share tokens are 256-bit random values, so a plain SHA-256 is enough (no salt or slow KDF needed):
/// the database only ever stores the hash, and a leaked DB or backup doesn't reveal working links.
/// </summary>
public static class TokenHashing
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant();
}
