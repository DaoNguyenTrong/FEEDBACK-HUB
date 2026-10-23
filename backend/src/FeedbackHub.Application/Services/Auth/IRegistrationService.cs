namespace FeedbackHub.Application.Services.Auth;

public interface IRegistrationService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task ResendVerificationEmailAsync(ResendVerificationRequest request, CancellationToken cancellationToken);

    /// <summary>Applies a pending profile email change from its mailed link. Opens no session.</summary>
    Task ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken cancellationToken);
}
