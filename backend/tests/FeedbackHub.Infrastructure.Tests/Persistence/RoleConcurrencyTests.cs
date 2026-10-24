using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.Roles;
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
public sealed class RoleConcurrencyTests(PostgresContainerFixture fixture)
{
    // How long the second request is watched while the first one holds the lock.
    private static readonly TimeSpan BlockedWindow = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan StepLimit = TimeSpan.FromSeconds(10);

    /// <summary>Holds the first request inside its transaction, after its checks, until released.</summary>
    private sealed class PausingAuditTrail(IAuditTrail inner, TaskCompletionSource entered, Task release) : IAuditTrail
    {
        public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await release.WaitAsync(StepLimit, cancellationToken);
            await inner.RecordAsync(auditEvent, cancellationToken);
        }
    }

    private sealed record Seeded(Guid OrganizationId, Guid OwnerId, Guid ManagerId, Guid PlainId, Role Member, Role Tester);

    private static ServiceProvider RepositoriesFor(AppDbContext context) =>
        new ServiceCollection()
            .AddSingleton(context)
            .AddScoped(typeof(IRepository<,>), typeof(GenericRepository<,>))
            .BuildServiceProvider();

    private static (ICurrentUserService CurrentUser, ICurrentTenantProvider Tenant) ContextFor(
        Guid organizationId, Guid callerId)
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(callerId.ToString());
        ICurrentTenantProvider tenant = Substitute.For<ICurrentTenantProvider>();
        tenant.OrganizationId.Returns(organizationId);
        return (currentUser, tenant);
    }

    private static RoleService CreateRoleService(
        AppDbContext context, ServiceProvider repositories, Guid organizationId, Guid callerId,
        Func<IAuditTrail, IAuditTrail>? wrapTrail = null)
    {
        (ICurrentUserService currentUser, ICurrentTenantProvider tenant) = ContextFor(organizationId, callerId);
        IAuditTrail auditTrail = new AuditTrail(context, new DateTimeProvider());

        return new RoleService(
            new UnitOfWork(context, repositories),
            currentUser,
            tenant,
            new RoleQueries(context),
            new RoleGrantReader(context),
            new OrganizationLock(context),
            wrapTrail?.Invoke(auditTrail) ?? auditTrail,
            Substitute.For<IPermissionResolver>());
    }

    private static MemberService CreateMemberService(
        AppDbContext context, ServiceProvider repositories, Guid organizationId, Guid callerId,
        Func<IAuditTrail, IAuditTrail>? wrapTrail = null)
    {
        (ICurrentUserService currentUser, ICurrentTenantProvider tenant) = ContextFor(organizationId, callerId);
        IAuditTrail auditTrail = new AuditTrail(context, new DateTimeProvider());

        return new MemberService(
            currentUser,
            tenant,
            new OrganizationMemberQueries(context),
            new RoleGrantReader(context),
            new UnitOfWork(context, repositories),
            new OrganizationLock(context),
            wrapTrail?.Invoke(auditTrail) ?? auditTrail,
            Substitute.For<ITenantAccessService>(),
            Substitute.For<IPermissionResolver>());
    }

    /// <summary>An Owner, a manager whose custom role has every permission, a plain Member and an unheld "Tester".</summary>
    private async Task<Seeded> SeedAsync()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        Organization organization = Organization.Create(new OrganizationParams("Bảo Minh", $"roles-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        Role owner = Role.Create(new RoleParams(organization.Id, "Owner", SystemRoleKind.Owner));
        Role member = Role.Create(new RoleParams(organization.Id, "Member", SystemRoleKind.Member));
        Role manager = Role.Create(new RoleParams(organization.Id, "Toàn quyền"));
        Role tester = Role.Create(new RoleParams(organization.Id, "Tester"));
        context.Roles.AddRange(owner, member, manager, tester);
        context.RolePermissions.AddRange(Permissions.All.Select(code =>
            RolePermission.Create(new RolePermissionParams(manager.Id, code))));

        Guid AddMember(string name, Role role)
        {
            string handle = $"r{Guid.NewGuid():N}"[..20];
            Account account = Account.Create(new AccountParams(name, handle, $"{handle}@example.com"));
            context.Accounts.Add(account);
            OrganizationMember row = OrganizationMember.Create(new OrganizationMemberParams(organization.Id, account.Id));
            context.OrganizationMembers.Add(row);
            context.OrganizationMemberRoles.Add(
                OrganizationMemberRole.Create(new OrganizationMemberRoleParams(row.Id, role.Id)));
            return account.Id;
        }

        Guid ownerId = AddMember("An Chủ", owner);
        Guid managerId = AddMember("Bảo Quản", manager);
        Guid plainId = AddMember("Cơ Bản", member);
        await context.SaveChangesAsync();
        return new Seeded(organization.Id, ownerId, managerId, plainId, member, tester);
    }

    // TECH00 §5.4: deleting a role never runs alongside assigning it, or the new holder would lose it by cascade.
    [Fact]
    public async Task DeleteAsync_RoleAssignedConcurrently_AssignmentWaitsThenFindsNoRole()
    {
        Seeded seeded = await SeedAsync();
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        await using ServiceProvider firstRepositories = RepositoriesFor(firstContext);
        await using ServiceProvider secondRepositories = RepositoriesFor(secondContext);
        RoleService roles = CreateRoleService(
            firstContext, firstRepositories, seeded.OrganizationId, seeded.OwnerId,
            trail => new PausingAuditTrail(trail, firstEntered, releaseFirst.Task));
        MemberService members = CreateMemberService(secondContext, secondRepositories, seeded.OrganizationId, seeded.OwnerId);

        Task delete = roles.DeleteAsync(seeded.Tester.Id, CancellationToken.None);
        await Task.WhenAny(firstEntered.Task, delete).WaitAsync(StepLimit);
        Assert.False(delete.IsCompleted, delete.Exception?.ToString());
        Task assign = members.ChangeRolesAsync(
            seeded.PlainId, new UpdateMemberRolesRequest([seeded.Member.Id, seeded.Tester.Id]), CancellationToken.None);
        await Task.WhenAny(assign, Task.Delay(BlockedWindow));
        bool assignWaitedForLock = !assign.IsCompleted;
        releaseFirst.SetResult();

        await delete;
        NotFoundException refused = await Assert.ThrowsAsync<NotFoundException>(() => assign);

        Assert.True(assignWaitedForLock);
        Assert.Equal(nameof(Role), refused.Args[0]);
        await using AppDbContext reader = fixture.CreateDbContext();
        Assert.False(await reader.Roles.AnyAsync(role => role.Id == seeded.Tester.Id));
        Assert.Equal(
            [seeded.Member.Id],
            await reader.OrganizationMemberRoles
                .Where(link => reader.OrganizationMembers.Any(row =>
                    row.Id == link.OrganizationMemberId && row.AccountId == seeded.PlainId))
                .Select(link => link.RoleId)
                .ToListAsync());
    }

    // F11: removed while waiting for the lock, the manager is refused as the gate would now refuse them.
    [Fact]
    public async Task DeleteAsync_CallerRemovedWhileWaitingForLock_Throws403AccessDenied()
    {
        Seeded seeded = await SeedAsync();
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        await using ServiceProvider firstRepositories = RepositoriesFor(firstContext);
        await using ServiceProvider secondRepositories = RepositoriesFor(secondContext);
        MemberService members = CreateMemberService(
            firstContext, firstRepositories, seeded.OrganizationId, seeded.OwnerId,
            trail => new PausingAuditTrail(trail, firstEntered, releaseFirst.Task));
        RoleService roles = CreateRoleService(secondContext, secondRepositories, seeded.OrganizationId, seeded.ManagerId);

        Task remove = members.RemoveAsync(seeded.ManagerId, CancellationToken.None);
        await Task.WhenAny(firstEntered.Task, remove).WaitAsync(StepLimit);
        Assert.False(remove.IsCompleted, remove.Exception?.ToString());
        Task delete = roles.DeleteAsync(seeded.Tester.Id, CancellationToken.None);
        await Task.WhenAny(delete, Task.Delay(BlockedWindow));
        bool deleteWaitedForLock = !delete.IsCompleted;
        releaseFirst.SetResult();

        await remove;
        ForbiddenException refused = await Assert.ThrowsAsync<ForbiddenException>(() => delete);

        Assert.True(deleteWaitedForLock);
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, refused.Message);
        await using AppDbContext reader = fixture.CreateDbContext();
        Assert.True(await reader.Roles.AnyAsync(role => role.Id == seeded.Tester.Id));
        Assert.Equal(0, await reader.AuditLogs.CountAsync(log => log.OrganizationId == seeded.OrganizationId
            && log.Kind == AuditLogKind.Event
            && log.Action == nameof(AuditEventType.RoleDeleted)));
    }
}
