using FileShare.Infrastructure.Security;

namespace FileShare.Tests.Infrastructure.Security;

public sealed class SasTokenServiceTests
{
    [Fact]
    public void Generate_ReturnsExactly64Characters()
    {
        // Arrange
        var service = new SasTokenService();

        // Act
        var token = service.Generate();

        // Assert
        Assert.Equal(64, token.Length);
    }

    [Fact]
    public void Generate_ReturnsLowercaseHexString()
    {
        // Arrange
        var service = new SasTokenService();

        // Act
        var token = service.Generate();

        // Assert
        Assert.Matches("^[0-9a-f]{64}$", token);
    }

    [Fact]
    public void Generate_ReturnsDifferentTokensOnEachCall()
    {
        // Arrange
        var service = new SasTokenService();

        // Act
        var token1 = service.Generate();
        var token2 = service.Generate();

        // Assert
        Assert.NotEqual(token1, token2);
    }
}
