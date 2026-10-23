using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FeedbackHub.Application.Services.Accounts;

namespace FeedbackHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(IAccountService accountService) : ControllerBase
{
    /// <summary>Returns the profile of the currently authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken cancellationToken)
    {
        ProfileDto profile = await accountService.GetCurrentProfileAsync(cancellationToken);

        return Ok(profile);
    }

    /// <summary>Updates the profile of the currently authenticated user.</summary>
    [HttpPut]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileDto>> Update(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        ProfileDto profile = await accountService.UpdateCurrentProfileAsync(request, cancellationToken);

        return Ok(profile);
    }

    /// <summary>Mails a new confirmation link for the pending email change.</summary>
    [HttpPost("email/resend")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendEmailChange(CancellationToken cancellationToken)
    {
        await accountService.ResendEmailChangeAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Cancels the pending email change, if any.</summary>
    [HttpDelete("email/pending")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelEmailChange(CancellationToken cancellationToken)
    {
        await accountService.CancelEmailChangeAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Sets the current user's avatar. Send as multipart/form-data; PNG, JPEG or WebP, 2 MB at most.
    /// </summary>
    [HttpPut("avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileDto>> UploadAvatar(
        IFormFile avatar,
        CancellationToken cancellationToken)
    {
        await using Stream content = avatar.OpenReadStream();

        ProfileDto profile = await accountService.UploadAvatarAsync(
            new AvatarUploadRequest(content, avatar.ContentType, avatar.Length),
            cancellationToken);

        return Ok(profile);
    }

    /// <summary>Removes the avatar of the currently authenticated user, if any.</summary>
    [HttpDelete("avatar")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileDto>> DeleteAvatar(CancellationToken cancellationToken)
    {
        ProfileDto profile = await accountService.DeleteAvatarAsync(cancellationToken);

        return Ok(profile);
    }

    /// <summary>Changes the password of the currently authenticated user.</summary>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await accountService.ChangePasswordAsync(request, cancellationToken);

        return NoContent();
    }
}
