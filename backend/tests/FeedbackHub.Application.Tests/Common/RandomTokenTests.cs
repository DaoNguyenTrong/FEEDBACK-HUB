using FeedbackHub.Application.Common;

namespace FeedbackHub.Application.Tests.Common;

public class RandomTokenTests
{
    // Create

    [Fact]
    public void Create_Always_Returns32BytesAsUnpaddedBase64Url()
    {
        string token = RandomToken.Create();

        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        string padded = token.Replace('-', '+').Replace('_', '/') + "=";
        Assert.Equal(32, Convert.FromBase64String(padded).Length);
    }

    [Fact]
    public void Create_CalledTwice_ReturnsDifferentTokens() =>
        Assert.NotEqual(RandomToken.Create(), RandomToken.Create());
}
