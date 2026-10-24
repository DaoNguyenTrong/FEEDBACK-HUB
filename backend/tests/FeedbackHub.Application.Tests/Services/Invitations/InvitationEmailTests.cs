using System.Globalization;
using System.Net;
using FeedbackHub.Application.Services.Invitations;

namespace FeedbackHub.Application.Tests.Services.Invitations;

public class InvitationEmailTests
{
    private static readonly DateTime ExpiresAt = new(2026, 10, 23, 17, 30, 0, DateTimeKind.Utc);

    private const string Url = "https://hub.example.com/#/invite?token=abc_DEF-123";

    // Render

    [Fact]
    public void Render_EncodesEveryValue()
    {
        (string subject, string body) = InvitationEmail.Render(
            "Bảo <b>&</b> Minh", "Trưởng <b>&</b>", ExpiresAt, "https://hub.example.com/#/invite?token=a&b=\"<x>\"");

        Assert.Equal("Lời mời tham gia Bảo <b>&</b> Minh trên FeedbackHub", subject);
        Assert.DoesNotContain("<b>", body);
        Assert.DoesNotContain("<x>", body);
        Assert.Contains("<strong>Bảo &lt;b&gt;&amp;&lt;/b&gt; Minh</strong>", body);
        Assert.Contains("<strong>Trưởng &lt;b&gt;&amp;&lt;/b&gt;</strong>", body);
        Assert.Contains(
            "href=\"https://hub.example.com/#/invite?token=a&amp;b=&quot;&lt;x&gt;&quot;\"", body);
    }

    [Fact]
    public void Render_ContainsOrganizationRoleExpiryAndLinkButNoInviter()
    {
        (_, string body) = InvitationEmail.Render("Bảo Minh", "Nhân sự", ExpiresAt, Url);

        Assert.Contains("<p>Xin chào,</p>", body);
        Assert.Contains(
            "<p>Bạn được mời tham gia tổ chức <strong>Bảo Minh</strong> trên FeedbackHub với vai trò "
            + $"<strong>{WebUtility.HtmlEncode("Nhân sự")}</strong>.</p>",
            body);
        Assert.Contains($"<p><a href=\"{Url}\">{Url}</a></p>", body);
        Assert.Contains("Lời mời có hiệu lực đến", body);
        Assert.Contains("<p>Nếu bạn không chờ lời mời này, hãy bỏ qua thư này.</p>", body);
        Assert.DoesNotContain("Người mời", body, StringComparison.OrdinalIgnoreCase);
    }

    // Vietnam keeps UTC+7 all year; the machine's zone and culture must not change the text.
    [Fact]
    public void Render_ExpiryUsesFixedUtcPlus7AndVietnamLabel()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            (_, string body) = InvitationEmail.Render("Bảo Minh", "Nhân sự", ExpiresAt, Url);

            Assert.Contains("<p>Lời mời có hiệu lực đến 24/10/2026 00:30 (giờ Việt Nam).</p>", body);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
