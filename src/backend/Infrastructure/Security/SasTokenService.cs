using System.Security.Cryptography;

namespace FileShare.Infrastructure.Security;

public sealed class SasTokenService
{
    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
