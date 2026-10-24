using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Persistence.Repositories;
using FeedbackHub.Infrastructure.Services.Context;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

// Commits rows on purpose: the organization lock only orders two real transactions.
[Collection(nameof(PostgresCollection))]
public sealed class MemberConcurrencyTests(PostgresContainerFixture fixture)
{
    // How long the second request is watched while the first one holds the lock.
    private static readonly TimeSpan BlockedWindow = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan StepLimit = TimeSpan.FromSeconds(10);

    /// <summary>Holds the first request inside its transaction, after its checks and writes, until released.</summary>
    private sealed class PausingAuditTrail(IAuditTrail inner, TaskCompletionSource entered, Task release) : IAuditTrail
    {
        public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await release.WaitAsync(StepLimit, cancellationToken);
            await inner.RecordAsync(auditEvent, cancellationToken);
        }
    }

    private static ServiceProvider RepositoriesFor(AppDbContext context) =>
        new ServiceCollection()
            .AddSingleton(context)
            .AddScoped(typeof(IRepository<,>), typeof(GenericRepository<,>))
            .BuildServiceProvider();

    private static MemberService CreateService(
        AppDbContext context, Guid organizationId, Guid callerId, Func<IAuditTrail, IAuditTrail>? wrapTrail = null)
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(callerId.ToString());
        ICurrentTenantProvider tenant = Substitute.For<ICurrentTenantProvider>();
        tenant.OrganizationId.Returns(organizationId);
        IAuditTrail auditTrail = new AuditTrail(context, new DateTimeProvider());

        return new MemberService(
            currentUser,
            tenant,
            new OrganizationMemberQueries(context),
            new RoleGrantReader(context),
            new UnitOfWork(context, RepositoriesFor(context)),
            new OrganizationLock(context),
            wrapTrail?.Invoke(auditTrail) ?? auditTrail,
            Substitute.For<ITenantAccessService>(),
            Substitute.For<IPermissionResolver>());
    }

    private async Task<(Guid OrganizationId, Guid FirstOwnerId, Guid SecondOwnerId)> SeedTwoOwnersAsync()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        Organization organization = Organization.Create(
            new OrganizationParams("Bảo Minh", $"members-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        Role owner = Role.Create(new RoleParams(organization.Id, "Owner", SystemRoleKind.Owner));
        context.Roles.Add(owner);

        Guid[] ownerIds = new Guid[2];
        for (int i = 0; i < ownerIds.Length; i++)
        {
            string handle = $"owner{i}-{Guid.NewGuid():N}"[..20];
            Account account = Account.Create(new AccountParams($"Owner {i}", handle, $"{handle}@example.com"));
            context.Accounts.Add(account);
            OrganizationMember member =
                OrganizationMember.Create(new OrganizationMemberParams(organization.Id, account.Id));
            context.OrganizationMembers.Add(member);
            context.OrganizationMemberRoles.Add(
                OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, owner.Id)));
            ownerIds[i] = account.Id;
        }

        await context.SaveChangesAsync();
        return (organization.Id, ownerIds[0], ownerIds[1]);
    }

    // TECH00 §5.4, F11: the second request waits for the lock, then finds its caller already removed.
    [Fact]
    public async Task RemoveAsync_TwoOwnersRemoveEachOtherConcurrently_OneSucceedsOtherForbidden()
    {
        (Guid organizationId, Guid firstOwnerId, Guid secondOwnerId) = await SeedTwoOwnersAsync();
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        MemberService first = CreateService(
            firstContext, organizationId, firstOwnerId,
            trail => new PausingAuditTrail(trail, firstEntered, releaseFirst.Task));
        MemberService second = CreateService(secondContext, organizationId, secondOwnerId);

        Task firstRemove = first.RemoveAsync(secondOwnerId, CancellationToken.None);
        await Task.WhenAny(firstEntered.Task, firstRemove).WaitAsync(StepLimit);
        Assert.False(firstRemove.IsCompleted, firstRemove.Exception?.ToString());
        Task secondRemove = second.RemoveAsync(firstOwnerId, CancellationToken.None);
        await Task.WhenAny(secondRemove, Task.Delay(BlockedWindow));
        bool secondWaitedForLock = !secondRemove.IsCompleted;
        releaseFirst.SetResult();

        await firstRemove;
        ForbiddenException refused = await Assert.ThrowsAsync<ForbiddenException>(() => secondRemove);

        Assert.True(secondWaitedForLock);
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, refused.Message);
        await using AppDbContext reader = fixture.CreateDbContext();
        List<Guid> activeOwners = await reader.OrganizationMembers
            .Where(member => member.OrganizationId == organizationId && member.IsActive
                && reader.OrganizationMemberRoles.Any(link => link.OrganizationMemberId == member.Id
                    && reader.Roles.Any(role => role.Id == link.RoleId && role.SystemRoleKind == SystemRoleKind.Owner)))
            .Select(member => member.AccountId)
            .ToListAsync();
        Assert.Equal([firstOwnerId], activeOwners);
        Assert.Equal(
            1,
            await reader.AuditLogs.CountAsync(log => log.OrganizationId == organizationId
                && log.Kind == AuditLogKind.Event
                && log.Action == nameof(AuditEventType.MemberRemoved)));
    }
}
