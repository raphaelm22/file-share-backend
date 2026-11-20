using FileShare.Infrastructure.Security;

namespace FileShare.Tests.Infrastructure.Security;

public sealed class TokenFormatTests
{
    [Fact]
    public void GeneratedToken_IsValid() =>
        Assert.True(TokenFormat.IsValid(new SasTokenService().Generate()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("../../etc/passwd")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void MalformedTokens_AreInvalid(string? token) =>
        Assert.False(TokenFormat.IsValid(token));

    [Fact]
    public void Redact_NeverReturnsFullToken()
    {
        var token = new SasTokenService().Generate();
        var redacted = TokenFormat.Redact(token);
        Assert.DoesNotContain(token, redacted);
        Assert.StartsWith(token[..8], redacted);
    }
}
