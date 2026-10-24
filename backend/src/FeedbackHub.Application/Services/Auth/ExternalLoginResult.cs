namespace FeedbackHub.Application.Services.Auth;

/// <param name="AccountCreated">True only when this sign-in created the account (TECH00 §4.4 API-23, AC-178).</param>
public sealed record ExternalLoginResult(AuthResult Auth, bool AccountCreated);
