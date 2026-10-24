using System.Security.Cryptography;

namespace FeedbackHub.Application.Common;

/// <summary>A secret for a link sent by email: 32 random bytes, base64url without padding. Store only its hash.</summary>
internal static class RandomToken
{
    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
