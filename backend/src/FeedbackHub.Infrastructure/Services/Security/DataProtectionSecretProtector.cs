using Microsoft.AspNetCore.DataProtection;
using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.Infrastructure.Services.Security;

public sealed class DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider) : ISecretProtector
{
    private readonly IDataProtector protector =
        dataProtectionProvider.CreateProtector("FeedbackHub.Secrets");

    public string Protect(string plainText) => protector.Protect(plainText);

    public string Unprotect(string protectedText) => protector.Unprotect(protectedText);
}
