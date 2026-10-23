using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.SystemOrganizations;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;
using System.Linq.Expressions;

namespace FeedbackHub.Application.Tests.Services.SystemOrganizations;

public class SystemAdministratorActorTests
{
    private sealed record Fixture(
        SystemAdministratorActor Actor,
        IUnitOfWork UnitOfWork,
        IRepository<OrganizationMember, Guid> Members);

    private static Fixture CreateFixture(params OrganizationMember[] seed)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<OrganizationMember, Guid> members = Substitute.For<IRepository<OrganizationMember, Guid>>();
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(members);
        RepositoryPredicateStub.StubFirstOrDefault(members, seed);
        return new Fixture(new SystemAdministratorActor(unitOfWork), unitOfWork, members);
    }

    // ResolveAsync

    [Fact]
    public async Task ResolveAsync_ActiveMemberOfThatOrganization_ReturnsAccount()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture fixture = CreateFixture(
            OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId)));

        ActorKind kind = await fixture.Actor.ResolveAsync(organizationId, accountId, CancellationToken.None);

        Assert.Equal(ActorKind.Account, kind);
        await fixture.Members.Received(1).FirstOrDefaultAsync(
            Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("otherOrganization")]
    [InlineData("otherAccount")]
    public async Task ResolveAsync_RowExcludedByOneClause_ReturnsSystemAdministrator(string excluded)
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        OrganizationMember row = excluded switch
        {
            "otherOrganization" => OrganizationMember.Create(new OrganizationMemberParams(Guid.NewGuid(), accountId)),
            "otherAccount" => OrganizationMember.Create(new OrganizationMemberParams(organizationId, Guid.NewGuid())),
            _ => OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId)),
        };
        if (excluded == "inactive")
        {
            row.Deactivate();
        }

        Fixture fixture = CreateFixture(row);

        ActorKind kind = await fixture.Actor.ResolveAsync(organizationId, accountId, CancellationToken.None);

        Assert.Equal(ActorKind.SystemAdministrator, kind);
        await fixture.Members.Received(1).FirstOrDefaultAsync(
            Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
