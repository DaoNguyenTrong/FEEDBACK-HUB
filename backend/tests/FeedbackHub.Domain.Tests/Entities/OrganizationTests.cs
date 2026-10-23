using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests.Entities;

public class OrganizationTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    private static OrganizationParams ValidParams() => new(Name: "Acme Inc", Slug: "acme");

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        OrganizationParams p = ValidParams();

        Organization organization = Organization.Create(p);

        Assert.NotEqual(Guid.Empty, organization.Id);
        Assert.Equal(7, organization.Id.Version);
        Assert.Equal(p.Name, organization.Name);
        Assert.Equal(p.Slug, organization.Slug);
        Assert.True(organization.Status);
        Assert.Null(organization.DeactivatedAt);
        Assert.Null(organization.DeactivatedByAccountId);
        Assert.False(organization.DeactivatedBySystemAdministrator);
        Assert.Null(organization.DeactivationReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string name)
    {
        OrganizationParams p = ValidParams() with { Name = name };

        DomainAssert.ThrowsWithMessage(DomainMessages.OrganizationNameRequired, () => Organization.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankSlug_ThrowsDomainException(string slug)
    {
        OrganizationParams p = ValidParams() with { Slug = slug };

        DomainAssert.ThrowsWithMessage(DomainMessages.OrganizationSlugRequired, () => Organization.Create(p));
    }

    [Theory]
    [InlineData("sao_mai")]
    [InlineData("-saomai")]
    [InlineData("saomai-")]
    [InlineData("sao--mai")] // AC-283
    [InlineData("sao-mài")]
    public void Create_SlugBreaksFormat_ThrowsSlugInvalid(string slug)
    {
        OrganizationParams p = ValidParams() with { Slug = slug };

        DomainAssert.ThrowsWithMessage(DomainMessages.OrganizationSlugInvalid, () => Organization.Create(p));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 51 chars, AC-284
    public void Create_SlugLengthOutside3To50_ThrowsSlugInvalid(string slug)
    {
        Assert.True(slug.Length is 2 or 51);

        OrganizationParams p = ValidParams() with { Slug = slug };

        DomainAssert.ThrowsWithMessage(DomainMessages.OrganizationSlugInvalid, () => Organization.Create(p));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 50 chars, AC-285
    public void Create_SlugAtLengthLimits_Succeeds(string slug)
    {
        Organization organization = Organization.Create(ValidParams() with { Slug = slug });

        Assert.Equal(slug, organization.Slug);
    }

    [Fact]
    public void Create_SlugWithUpperCaseAndSpaces_IsNormalized()
    {
        Organization organization = Organization.Create(ValidParams() with { Slug = " Sao-Mai " }); // AC-329

        Assert.Equal("sao-mai", organization.Slug);
    }

    [Fact]
    public void Create_TrimsName()
    {
        Organization organization = Organization.Create(ValidParams() with { Name = " Acme Inc " });

        Assert.Equal("Acme Inc", organization.Name);
    }

    [Fact]
    public void Update_RenamesAndKeepsSlug()
    {
        Organization organization = Organization.Create(ValidParams());

        organization.Update(new OrganizationNameParams(" Acme Global "));

        Assert.Equal("Acme Global", organization.Name); // AC-225
        Assert.Equal("acme", organization.Slug);
        Assert.True(organization.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_BlankName_ThrowsNameRequired(string name)
    {
        Organization organization = Organization.Create(ValidParams());

        DomainAssert.ThrowsWithMessage(
            DomainMessages.OrganizationNameRequired, () => organization.Update(new OrganizationNameParams(name))); // AC-187
    }

    [Fact]
    public void Deactivate_SetsStatusAndAllFourFields()
    {
        Organization organization = Organization.Create(ValidParams());
        Guid actor = Guid.NewGuid();

        organization.Deactivate(new DeactivationParams(actor, true, "  Hết hợp đồng  ", Now));

        Assert.False(organization.Status);
        Assert.Equal(Now, organization.DeactivatedAt);
        Assert.Equal(actor, organization.DeactivatedByAccountId);
        Assert.True(organization.DeactivatedBySystemAdministrator);
        Assert.Equal("Hết hợp đồng", organization.DeactivationReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deactivate_BlankReason_StoresNull(string? reason)
    {
        Organization organization = Organization.Create(ValidParams());

        organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, reason, Now));

        Assert.Null(organization.DeactivationReason);
    }

    [Fact]
    public void Deactivate_AlreadyDeactivated_KeepsFirstActorAndReason()
    {
        Organization organization = Organization.Create(ValidParams());
        Guid first = Guid.NewGuid();
        organization.Deactivate(new DeactivationParams(first, false, "first", Now));

        organization.Deactivate(new DeactivationParams(Guid.NewGuid(), true, "second", Now.AddHours(1)));

        Assert.Equal(first, organization.DeactivatedByAccountId);
        Assert.False(organization.DeactivatedBySystemAdministrator);
        Assert.Equal("first", organization.DeactivationReason);
        Assert.Equal(Now, organization.DeactivatedAt);
    }

    [Fact]
    public void Reactivate_ClearsAllFourFields()
    {
        Organization organization = Organization.Create(ValidParams());
        organization.Deactivate(new DeactivationParams(Guid.NewGuid(), true, "reason", Now));

        organization.Reactivate();

        Assert.True(organization.Status);
        Assert.Null(organization.DeactivatedAt);
        Assert.Null(organization.DeactivatedByAccountId);
        Assert.False(organization.DeactivatedBySystemAdministrator);
        Assert.Null(organization.DeactivationReason);
    }
}
