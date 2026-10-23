using System.Linq.Expressions;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.SystemOrganizations;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.SystemOrganizations;

public class SystemAdministratorActorTests
{
    private static SystemAdministratorActor Create(params OrganizationMember[] seed)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<OrganizationMember, Guid> repo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(repo);
        RepositoryPredicateStub.StubFirstOrDefault(repo, seed);
        return new SystemAdministratorActor(unitOfWork);
    }

    [Fact]
    public async Task ResolveAsync_ActiveMemberOfThatOrganization_ReturnsAccount()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        SystemAdministratorActor actor = Create(
            OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId)));

        ActorKind kind = await actor.ResolveAsync(organizationId, accountId, CancellationToken.None);

        Assert.Equal(ActorKind.Account, kind);
    }

    [Fact]
    public async Task ResolveAsync_InactiveMemberOrMemberOfOtherOrganization_ReturnsSystemAdministrator()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        OrganizationMember inactive = OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId));
        inactive.Deactivate();
        OrganizationMember otherOrganization =
            OrganizationMember.Create(new OrganizationMemberParams(Guid.NewGuid(), accountId));
        SystemAdministratorActor actor = Create(inactive, otherOrganization);

        ActorKind kind = await actor.ResolveAsync(organizationId, accountId, CancellationToken.None);

        Assert.Equal(ActorKind.SystemAdministrator, kind);
    }
}
