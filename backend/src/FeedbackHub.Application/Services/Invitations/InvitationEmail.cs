using System.Globalization;
using System.Net;

namespace FeedbackHub.Application.Services.Invitations;

/// <summary>
/// The interim invitation mail (TECH00 §6.2, §14 P3): Vietnamese only, hard-coded like the other notification mails
/// (<c>localization.md</c>), without the inviter's name. PRD05 decides the final template.
/// </summary>
internal static class InvitationEmail
{
    // Vietnam has no daylight saving time, so a fixed offset needs no time zone database on the host.
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static (string Subject, string HtmlBody) Render(
        string organizationName, string roleName, DateTime expiresAtUtc, string url)
    {
        string organization = WebUtility.HtmlEncode(organizationName);
        string role = WebUtility.HtmlEncode(roleName);
        string link = WebUtility.HtmlEncode(url);
        string expiry = WebUtility.HtmlEncode(
            (expiresAtUtc + VietnamOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));

        string body = $"""
            <p>Xin chào,</p>
            <p>Bạn được mời tham gia tổ chức <strong>{organization}</strong> trên FeedbackHub với vai trò <strong>{role}</strong>.</p>
            <p>Nhấn vào liên kết bên dưới để mở lời mời:</p>
            <p><a href="{link}">{link}</a></p>
            <p>Lời mời có hiệu lực đến {expiry} (giờ Việt Nam).</p>
            <p>Nếu bạn không chờ lời mời này, hãy bỏ qua thư này.</p>
            """;

        return ($"Lời mời tham gia {organizationName} trên FeedbackHub", body);
    }
}
