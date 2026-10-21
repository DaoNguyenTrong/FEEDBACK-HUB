using Microsoft.AspNetCore.DataProtection;
using FeedbackHub.Infrastructure.Services.Security;

namespace FeedbackHub.Infrastructure.Tests.Services.Security;

public class DataProtectionSecretProtectorTests
{
    private static DataProtectionSecretProtector CreateProtector()
    {
        IDataProtectionProvider provider = DataProtectionProvider.Create("FeedbackHub.Infrastructure.Tests");
        return new DataProtectionSecretProtector(provider);
    }

    [Fact]
    public void Protect_ThenUnprotect_RoundTrips()
    {
        DataProtectionSecretProtector protector = CreateProtector();

        string protectedText = protector.Protect("plain-secret");
        string unprotected = protector.Unprotect(protectedText);

        Assert.Equal("plain-secret", unprotected);
        Assert.NotEqual("plain-secret", protectedText);
    }

    [Fact]
    public void Unprotect_TamperedCiphertext_Throws()
    {
        DataProtectionSecretProtector protector = CreateProtector();
        string protectedText = protector.Protect("plain-secret");
        string tampered = protectedText[..^1] + (protectedText[^1] == 'A' ? 'B' : 'A');

        Assert.ThrowsAny<Exception>(() => protector.Unprotect(tampered));
    }
}
