using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests.Entities;

public class OrganizationInvitationTests
{
    private static readonly DateTime Issued = new(2026, 10, 23, 10, 17, 33, 123, DateTimeKind.Utc);

    private static OrganizationInvitationParams ValidParams() => new(
        OrganizationId: Guid.NewGuid(),
        Email: " A@X.com ",
        RoleId: Guid.NewGuid(),
        TokenHash: new string('a', 64),
        IssuedAt: Issued);

    [Fact]
    public void Create_ValidParams_NormalizesEmailAndExpiresExactly168HoursLater()
    {
        OrganizationInvitationParams p = ValidParams();

        OrganizationInvitation invitation = OrganizationInvitation.Create(p);

        Assert.Equal(7, invitation.Id.Version);
        Assert.Equal(p.OrganizationId, invitation.OrganizationId);
        Assert.Equal(p.RoleId, invitation.RoleId);
        Assert.Equal("a@x.com", invitation.Email);
        Assert.Equal(p.TokenHash, invitation.TokenHash);
        Assert.Equal(new DateTime(2026, 10, 30, 10, 17, 33, 123, DateTimeKind.Utc), invitation.ExpiresAt);
    }

    [Fact]
    public void Create_EmptyOrganization_Throws()
    {
        OrganizationInvitationParams p = ValidParams() with { OrganizationId = Guid.Empty };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.InvitationOrganizationRequired, () => OrganizationInvitation.Create(p));
    }

    [Fact]
    public void Create_EmptyRole_Throws()
    {
        OrganizationInvitationParams p = ValidParams() with { RoleId = Guid.Empty };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.InvitationRoleRequired, () => OrganizationInvitation.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankEmail_Throws(string email)
    {
        OrganizationInvitationParams p = ValidParams() with { Email = email };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.AccountEmailRequired, () => OrganizationInvitation.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\n")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Create_TokenHashNot64LowerHex_Throws(string hash)
    {
        OrganizationInvitationParams p = ValidParams() with { TokenHash = hash };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.InvitationTokenHashInvalid, () => OrganizationInvitation.Create(p));
    }

    [Fact]
    public void IsExpiredAt_ExactlyAtExpiry_IsTrue()
    {
        OrganizationInvitation invitation = OrganizationInvitation.Create(ValidParams());

        Assert.True(invitation.IsExpiredAt(invitation.ExpiresAt));
    }

    [Fact]
    public void IsExpiredAt_OneTickBefore_IsFalse()
    {
        OrganizationInvitation invitation = OrganizationInvitation.Create(ValidParams());

        Assert.False(invitation.IsExpiredAt(invitation.ExpiresAt.AddTicks(-1)));
    }
}
