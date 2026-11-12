using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests.Entities;

public class SampleTests
{
    private static SampleParams ValidParams()
    {
        return new SampleParams(Name: "First");
    }

//#if (OrgScoped)
    private static Sample CreateEntity(SampleParams p) => Sample.Create(p, Guid.NewGuid());
//#else
    private static Sample CreateEntity(SampleParams p) => Sample.Create(p);
//#endif

    // Create

    [Fact]
    public void Create_WithValidParams_AssignsFieldsAndGeneratesId()
    {
        Sample entity = CreateEntity(ValidParams());

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("First", entity.Name);
    }

    [Fact]
    public void Create_TrimsName()
    {
        Sample entity = CreateEntity(new SampleParams(Name: "  First  "));

        Assert.Equal("First", entity.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string name)
    {
        SampleParams p = ValidParams() with { Name = name };

        DomainAssert.ThrowsWithMessage(DomainMessages.SampleNameRequired, () => CreateEntity(p));
    }
//#if (OrgScoped)

    [Fact]
    public void Create_AssignsTheOrganization()
    {
        Guid organizationId = Guid.NewGuid();

        Sample entity = Sample.Create(ValidParams(), organizationId);

        Assert.Equal(organizationId, entity.OrganizationId);
    }
//#endif

    // Update

    [Fact]
    public void Update_WithValidParams_RenamesEntity()
    {
        Sample entity = CreateEntity(ValidParams());

        entity.Update(new SampleParams(Name: "Renamed"));

        Assert.Equal("Renamed", entity.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithBlankName_ThrowsDomainException(string name)
    {
        Sample entity = CreateEntity(ValidParams());

        DomainAssert.ThrowsWithMessage(DomainMessages.SampleNameRequired, () => entity.Update(new SampleParams(Name: name)));
    }
}
