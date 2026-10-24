using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Organizations;

public class MemberServiceWriteTests
{
    private static readonly RoleGrantRole OwnerRole =
        new(Guid.NewGuid(), "Owner", SystemRoleKind.Owner, Permissions.All.ToHashSet());

    private static readonly RoleGrantRole AdminRole = new(
        Guid.NewGuid(),
        "Admin",
        SystemRoleKind.Admin,
        new HashSet<string> { Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin });

    private static readonly RoleGrantRole MemberRole =
        new(Guid.NewGuid(), "Member", SystemRoleKind.Member, new HashSet<string>());

    private static readonly RoleGrantRole PeopleRole =
        new(Guid.NewGuid(), "Nhân sự", null, new HashSet<string> { Permissions.OrganizationMembersManage });

    private static readonly RoleGrantRole TesterRole =
        new(Guid.NewGuid(), "Tester", null, new HashSet<string> { Permissions.FilesManage });

    private static readonly RoleGrantRole AllPermissionsRole =
        new(Guid.NewGuid(), "Toàn quyền", null, Permissions.All.ToHashSet());

    // The organization's roles in the reader's display order (Owner, Admin, Member, then custom by name).
    private static readonly RoleGrantRole[] OrganizationRoles =
        [OwnerRole, AdminRole, MemberRole, PeopleRole, TesterRole, AllPermissionsRole];

    public enum Operation
    {
        ChangeRoles,
        Remove,
        Leave
    }

    /// <summary>An in-memory organization behind substitutes, so tests assert both calls and resulting state.</summary>
    private sealed record Fixture
    {
        private readonly List<OrganizationMember> members = [];
        private readonly List<OrganizationMemberRole> memberRoles = [];
        private readonly Dictionary<Guid, Account> accounts = [];
        private readonly List<bool> invalidatedInsideTransaction = [];
        private bool inTransaction;

        public Fixture()
        {
            Organization = Organization.Create(new OrganizationParams("Bảo Minh", "bao-minh"));

            UnitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => RunInTransactionAsync(
                    callInfo.Arg<Func<CancellationToken, Task>>()!, callInfo.Arg<CancellationToken>()));
            UnitOfWork.Repository<Organization, Guid>().Returns(Organizations);
            UnitOfWork.Repository<OrganizationMember, Guid>().Returns(Members);
            UnitOfWork.Repository<OrganizationMemberRole, Guid>().Returns(MemberRoles);
            UnitOfWork.Repository<Account, Guid>().Returns(Accounts);

            Organizations.GetByIdAsync(Organization.Id, Arg.Any<CancellationToken>()).Returns(_ => Organization);
            Accounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => accounts.GetValueOrDefault(callInfo.Arg<Guid>()));
            RepositoryPredicateStub.StubFirstOrDefault(Members, members);
            RepositoryPredicateStub.StubListAsync(Members, members);
            RepositoryPredicateStub.StubListAsync(MemberRoles, memberRoles);
            MemberRoles.When(repository => repository.Delete(Arg.Any<OrganizationMemberRole>()))
                .Do(callInfo => memberRoles.Remove(callInfo.Arg<OrganizationMemberRole>()!));
            MemberRoles.When(repository => repository.AddAsync(Arg.Any<OrganizationMemberRole>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => memberRoles.Add(callInfo.Arg<OrganizationMemberRole>()!));

            Reader.LoadCallerAsync(Organization.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => CallerOverride ?? CallerFromData(callInfo.ArgAt<Guid>(1)));
            Reader.LoadRolesAsync(Organization.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    IReadOnlyCollection<Guid> ids = callInfo.ArgAt<IReadOnlyCollection<Guid>>(1);
                    return (IReadOnlyList<RoleGrantRole>)OrganizationRoles.Where(role => ids.Contains(role.Id)).ToList();
                });
            Reader.LoadRolesOfMembersAsync(
                    Organization.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    IReadOnlyCollection<Guid> ids = callInfo.ArgAt<IReadOnlyCollection<Guid>>(1);
                    return (IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>>)members
                        .Where(member => member.OrganizationId == Organization.Id && ids.Contains(member.Id))
                        .ToDictionary(member => member.Id, RolesOf);
                });

            TenantAccess.When(service => service.InvalidateMemberAsync(
                    Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => invalidatedInsideTransaction.Add(inTransaction));
            PermissionResolver.When(resolver => resolver.InvalidateMemberAsync(
                    Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => invalidatedInsideTransaction.Add(inTransaction));
            AuditTrail.When(trail => trail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => Recorded.Add(callInfo.Arg<AuditEvent>()!));

            // Anything read before the lock could be stale by the time the lock is held (TECH00 §5.4).
            OrganizationLock.When(organizationLock => organizationLock.AcquireAsync(
                    Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => ReadBeforeLock = ReadBeforeLock
                    || Reader.ReceivedCalls().Any()
                    || Members.ReceivedCalls().Any()
                    || MemberRoles.ReceivedCalls().Any()
                    || Organizations.ReceivedCalls().Any()
                    || Accounts.ReceivedCalls().Any());

            Tenant.OrganizationId.Returns(_ => Organization.Id);
            CurrentUser.UserId.Returns(_ => Caller?.Id.ToString());

            Service = new MemberService(
                CurrentUser,
                Tenant,
                Substitute.For<IOrganizationMemberQueries>(),
                Reader,
                UnitOfWork,
                OrganizationLock,
                AuditTrail,
                TenantAccess,
                PermissionResolver);
        }

        public Organization Organization { get; }

        public MemberService Service { get; }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IRepository<Organization, Guid> Organizations { get; } = Substitute.For<IRepository<Organization, Guid>>();

        public IRepository<OrganizationMember, Guid> Members { get; } =
            Substitute.For<IRepository<OrganizationMember, Guid>>();

        public IRepository<OrganizationMemberRole, Guid> MemberRoles { get; } =
            Substitute.For<IRepository<OrganizationMemberRole, Guid>>();

        public IRepository<Account, Guid> Accounts { get; } = Substitute.For<IRepository<Account, Guid>>();

        public IRoleGrantReader Reader { get; } = Substitute.For<IRoleGrantReader>();

        public IOrganizationLock OrganizationLock { get; } = Substitute.For<IOrganizationLock>();

        public IAuditTrail AuditTrail { get; } = Substitute.For<IAuditTrail>();

        public ITenantAccessService TenantAccess { get; } = Substitute.For<ITenantAccessService>();

        public IPermissionResolver PermissionResolver { get; } = Substitute.For<IPermissionResolver>();

        public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();

        public ICurrentTenantProvider Tenant { get; } = Substitute.For<ICurrentTenantProvider>();

        public List<AuditEvent> Recorded { get; } = [];

        public bool ReadBeforeLock { get; private set; }

        public Account? Caller { get; set; }

        public RoleGrantCaller? CallerOverride { get; set; }

        public Account AddMember(string name, params RoleGrantRole[] roles)
        {
            string handle = $"u{Guid.NewGuid():N}"[..12];
            Account account = Account.Create(new AccountParams(name, handle, $"{handle}@example.com"));
            accounts[account.Id] = account;
            OrganizationMember member =
                OrganizationMember.Create(new OrganizationMemberParams(Organization.Id, account.Id));
            members.Add(member);
            foreach (RoleGrantRole role in roles)
            {
                memberRoles.Add(OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, role.Id)));
            }

            return account;
        }

        public Account AddCaller(string name, params RoleGrantRole[] roles)
        {
            Caller = AddMember(name, roles);
            return Caller;
        }

        public void Deactivate(Account account) => MemberOf(account).Deactivate();

        public OrganizationMember MemberOf(Account account) =>
            members.Single(member => member.AccountId == account.Id);

        public bool IsActive(Account account) => MemberOf(account).IsActive;

        public IReadOnlyList<Guid> RoleIdsOf(Account account)
        {
            Guid memberId = MemberOf(account).Id;
            return memberRoles.Where(link => link.OrganizationMemberId == memberId).Select(link => link.RoleId).ToList();
        }

        public Task RunAsync(Operation operation, Guid accountId, IReadOnlyList<Guid>? roleIds = null) =>
            operation switch
            {
                Operation.ChangeRoles => Service.ChangeRolesAsync(
                    accountId, new UpdateMemberRolesRequest(roleIds ?? [MemberRole.Id]), CancellationToken.None),
                Operation.Remove => Service.RemoveAsync(accountId, CancellationToken.None),
                _ => Service.LeaveAsync(CancellationToken.None)
            };

        public async Task AssertNothingWrittenAsync()
        {
            await UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            MemberRoles.DidNotReceive().Delete(Arg.Any<OrganizationMemberRole>());
            await MemberRoles.DidNotReceive().AddAsync(Arg.Any<OrganizationMemberRole>(), Arg.Any<CancellationToken>());
            Members.DidNotReceive().Update(Arg.Any<OrganizationMember>());
            await TenantAccess.DidNotReceive()
                .InvalidateMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await PermissionResolver.DidNotReceive()
                .InvalidateMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        // Invalidating before the commit lets a request in between cache the old state again.
        public async Task AssertInvalidatedAfterCommitAsync(Account account)
        {
            await TenantAccess.Received(1).InvalidateMemberAsync(Organization.Id, account.Id, Arg.Any<CancellationToken>());
            await PermissionResolver.Received(1)
                .InvalidateMemberAsync(Organization.Id, account.Id, Arg.Any<CancellationToken>());
            Assert.Equal([false, false], invalidatedInsideTransaction);
        }

        private async Task RunInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken token)
        {
            inTransaction = true;
            try
            {
                await operation(token);
            }
            finally
            {
                inTransaction = false;
            }
        }

        private IReadOnlyList<RoleGrantRole> RolesOf(OrganizationMember member)
        {
            HashSet<Guid> held = memberRoles
                .Where(link => link.OrganizationMemberId == member.Id)
                .Select(link => link.RoleId)
                .ToHashSet();
            return OrganizationRoles.Where(role => held.Contains(role.Id)).ToList();
        }

        private RoleGrantCaller CallerFromData(Guid accountId)
        {
            OrganizationMember? member = members.FirstOrDefault(m => m.AccountId == accountId && m.IsActive);
            if (member is null)
            {
                return new RoleGrantCaller(accountId, false, new HashSet<string>());
            }

            IReadOnlyList<RoleGrantRole> roles = RolesOf(member);
            return new RoleGrantCaller(
                accountId,
                roles.Any(role => role.SystemKind == SystemRoleKind.Owner),
                roles.SelectMany(role => role.Permissions).ToHashSet());
        }
    }

    private static Fixture CreateFixture() => new();

    private static AuditRoleRef Ref(RoleGrantRole role) => new(role.Id, role.Name, role.SystemKind);

    private static Task ChangeAsync(Fixture f, Account target, params RoleGrantRole[] roles) =>
        f.Service.ChangeRolesAsync(
            target.Id, new UpdateMemberRolesRequest(roles.Select(role => role.Id).ToList()), CancellationToken.None);

    // Gate re-check after the lock (C3, F11): every write

    [Theory]
    [InlineData(Operation.ChangeRoles)]
    [InlineData(Operation.Remove)]
    [InlineData(Operation.Leave)]
    public async Task Write_Any_AcquiresLockBeforeAnyRead(Operation operation)
    {
        Fixture f = CreateFixture();
        f.AddCaller("An Chủ", OwnerRole);
        Account target = f.AddMember("Bình", MemberRole, TesterRole);
        f.AddMember("Cơ", OwnerRole);

        await f.RunAsync(operation, target.Id, [AdminRole.Id]);

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        Assert.False(f.ReadBeforeLock);
    }

    // Removed between the gate and the lock: refused the way the gate now would, nothing written.
    [Theory]
    [InlineData(Operation.ChangeRoles)]
    [InlineData(Operation.Remove)]
    [InlineData(Operation.Leave)]
    public async Task Write_CallerNoLongerActiveMemberAfterGate_Throws403AccessDenied(Operation operation)
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("An Chủ", OwnerRole);
        Account target = f.AddMember("Bình", MemberRole);
        f.Deactivate(caller);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => f.RunAsync(operation, target.Id, [AdminRole.Id]));

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        await f.AssertNothingWrittenAsync();
        Assert.True(f.IsActive(target));
    }

    [Theory]
    [InlineData(Operation.ChangeRoles)]
    [InlineData(Operation.Remove)]
    [InlineData(Operation.Leave)]
    public async Task Write_OrganizationSuspendedAfterGate_Throws403SuspendedWithName(Operation operation)
    {
        Fixture f = CreateFixture();
        f.AddCaller("An Chủ", OwnerRole);
        Account target = f.AddMember("Bình", MemberRole);
        f.AddMember("Cơ", OwnerRole);
        f.Organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));

        ForbiddenException exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => f.RunAsync(operation, target.Id, [AdminRole.Id]));

        Assert.Equal(ApplicationMessages.OrganizationSuspended, exception.Message);
        Assert.Equal("Bảo Minh", exception.Extensions[ProblemExtensionKeys.OrganizationName]);
        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        await f.AssertNothingWrittenAsync();
    }

    // members.manage lost after the gate (stale cache, or removed while waiting for the lock); BR-58 alone would pass.
    [Theory]
    [InlineData(Operation.ChangeRoles)]
    [InlineData(Operation.Remove)]
    public async Task Write_CallerLostMembersManageAfterGate_Throws403AccessDenied(Operation operation)
    {
        Fixture f = CreateFixture();
        f.AddCaller("Tâm", TesterRole);
        Account target = f.AddMember("Bình", MemberRole);
        f.AddMember("Cơ", OwnerRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.RunAsync(operation, target.Id, [MemberRole.Id, TesterRole.Id]));

        await f.AssertNothingWrittenAsync();
        Assert.True(f.IsActive(target));
        Assert.Equal([MemberRole.Id], f.RoleIdsOf(target));
    }

    // ChangeRolesAsync: input

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangeRolesAsync_EmptyRoleIds_Throws400BeforeLock(bool nullList)
    {
        Fixture f = CreateFixture();
        f.AddCaller("An Chủ", OwnerRole);
        Account target = f.AddMember("Bình", MemberRole);

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.OrganizationMemberRequiresAtLeastOneRole,
            () => f.Service.ChangeRolesAsync(
                target.Id, new UpdateMemberRolesRequest(nullList ? null! : []), CancellationToken.None));

        await f.UnitOfWork.DidNotReceive()
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await f.OrganizationLock.DidNotReceive().AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // BR-45: an id from another organization answers like one that does not exist.
    [Fact]
    public async Task ChangeRolesAsync_RoleOfOtherOrganization_Throws404AndWritesNothing()
    {
        Fixture f = CreateFixture();
        f.AddCaller("An Chủ", OwnerRole);
        Account target = f.AddMember("Bình", MemberRole);
        Guid foreignRoleId = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<Role>(
            foreignRoleId,
            () => f.Service.ChangeRolesAsync(
                target.Id, new UpdateMemberRolesRequest([AdminRole.Id, foreignRoleId]), CancellationToken.None));

        await f.AssertNothingWrittenAsync();
        Assert.Equal([MemberRole.Id], f.RoleIdsOf(target));
    }

    [Fact]
    public async Task ChangeRolesAsync_DuplicateIds_TreatedAsOne()
    {
        Fixture f = CreateFixture();
        f.AddCaller("An Chủ", OwnerRole);
        Account target = f.AddMember("Bình", MemberRole);

        await ChangeAsync(f, target, AdminRole, AdminRole, MemberRole);

        Assert.Equal(
            new[] { MemberRole.Id, AdminRole.Id }.Order(), f.RoleIdsOf(target).Order());
        await f.MemberRoles.Received(1).AddAsync(Arg.Any<OrganizationMemberRole>(), Arg.Any<CancellationToken>());
        f.MemberRoles.DidNotReceive().Delete(Arg.Any<OrganizationMemberRole>());
        AuditEvent changed = Assert.Single(f.Recorded);
        Assert.Equal([Ref(AdminRole), Ref(MemberRole)], (IReadOnlyList<AuditRoleRef>)changed.Parameters["rolesAfter"]);
    }

    [Fact]
    public async Task ChangeRolesAsync_MissingOrInactiveMember_Throws404()
    {
        Fixture f = CreateFixture();
        f.AddCaller("An Chủ", OwnerRole);
        Account former = f.AddMember("Bình", MemberRole);
        f.Deactivate(former);
        Guid stranger = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<OrganizationMember>(
            former.Id, () => ChangeAsync(f, former, AdminRole));
        await ApplicationAssert.AssertNotFoundAsync<OrganizationMember>(
            stranger,
            () => f.Service.ChangeRolesAsync(
                stranger, new UpdateMemberRolesRequest([AdminRole.Id]), CancellationToken.None));

        await f.AssertNothingWrittenAsync();
    }

    // ChangeRolesAsync: BR-58

    // AC-336 (add Member, keep Admin), AC-197 (Admin → Member)
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangeRolesAsync_ManagerLacksAdminPermissionsOfHolder_ThrowsInsufficient(bool keepAdmin)
    {
        Fixture f = CreateFixture();
        f.AddCaller("Nhân", PeopleRole);
        Account admin = f.AddMember("Quân", AdminRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => keepAdmin ? ChangeAsync(f, admin, AdminRole, MemberRole) : ChangeAsync(f, admin, MemberRole));

        await f.AssertNothingWrittenAsync();
        Assert.Equal([AdminRole.Id], f.RoleIdsOf(admin));
    }

    // AC-154
    [Fact]
    public async Task ChangeRolesAsync_SelfToAdminWithoutFeedbackHubAdmin_Throws403()
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Nhân", PeopleRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => ChangeAsync(f, caller, AdminRole));

        await f.AssertNothingWrittenAsync();
        Assert.Equal([PeopleRole.Id], f.RoleIdsOf(caller));
    }

    // AC-155
    [Fact]
    public async Task ChangeRolesAsync_AdminMakesMemberOwner_ThrowsOwnerOnly()
    {
        Fixture f = CreateFixture();
        f.AddCaller("An", AdminRole);
        Account member = f.AddMember("Minh", MemberRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OwnerOnlyOperation, () => ChangeAsync(f, member, OwnerRole, MemberRole));

        await f.AssertNothingWrittenAsync();
    }

    // AC-340: every permission in the catalogue is still not Owner.
    [Fact]
    public async Task ChangeRolesAsync_AllPermissionsNonOwnerAddsRoleToOwner_ThrowsOwnerOnly()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Phúc", AllPermissionsRole);
        Account owner = f.AddMember("Oanh", OwnerRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OwnerOnlyOperation, () => ChangeAsync(f, owner, OwnerRole, TesterRole));

        await f.AssertNothingWrittenAsync();
        Assert.Equal([OwnerRole.Id], f.RoleIdsOf(owner));
    }

    // AC-193; the sole Owner also trips the last-Owner rule, and 403 wins over 409 (D2).
    [Fact]
    public async Task ChangeRolesAsync_ManagerRemovesOwnerRole_ThrowsOwnerOnly()
    {
        Fixture f = CreateFixture();
        f.AddCaller("An", PeopleRole);
        Account owner = f.AddMember("Oanh", OwnerRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OwnerOnlyOperation, () => ChangeAsync(f, owner, AdminRole));

        await f.AssertNothingWrittenAsync();
        Assert.Equal([OwnerRole.Id], f.RoleIdsOf(owner));
    }

    // TECH00 §5.3: the Owner reason wins even when taking away the current roles fails on permissions first.
    [Fact]
    public async Task ChangeRolesAsync_ManagerAddsOwnerToAdminHolder_ThrowsOwnerOnly()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Nhân", PeopleRole);
        Account admin = f.AddMember("Quân", AdminRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OwnerOnlyOperation, () => ChangeAsync(f, admin, AdminRole, OwnerRole));

        await f.AssertNothingWrittenAsync();
    }

    // AC-157
    [Fact]
    public async Task ChangeRolesAsync_OwnerMakesMemberOwner_Succeeds()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Oanh", OwnerRole);
        Account member = f.AddMember("Minh", MemberRole);

        await ChangeAsync(f, member, OwnerRole, MemberRole);

        Assert.Equal(new[] { OwnerRole.Id, MemberRole.Id }.Order(), f.RoleIdsOf(member).Order());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.AssertInvalidatedAfterCommitAsync(member);
    }

    // A stale permission cache may grant what the database refuses (TECH00 §5.3).
    [Fact]
    public async Task ChangeRolesAsync_StaleCacheGrantsButDatabaseDenies_Throws403()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Cũ", PeopleRole);
        Account member = f.AddMember("Minh", MemberRole);
        f.PermissionResolver.GetEffectivePermissionsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Permissions.All.ToHashSet());

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => ChangeAsync(f, member, AdminRole));

        await f.PermissionResolver.DidNotReceive()
            .GetEffectivePermissionsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await f.AssertNothingWrittenAsync();
    }

    // ChangeRolesAsync: last Owner

    // BR-60: an inactive member holding Owner does not count.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangeRolesAsync_LastActiveOwnerDropsOwner_Throws409(bool inactiveOtherOwner)
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Oanh", OwnerRole);
        if (inactiveOtherOwner)
        {
            f.Deactivate(f.AddMember("Cựu", OwnerRole));
        }

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationCannotRemoveLastOwner, () => ChangeAsync(f, caller, AdminRole));

        await f.AssertNothingWrittenAsync();
        Assert.Equal([OwnerRole.Id], f.RoleIdsOf(caller));
    }

    [Fact]
    public async Task ChangeRolesAsync_OwnerDropsOwnerWhileAnotherActiveOwner_Succeeds()
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Oanh", OwnerRole);
        f.AddMember("Ông", OwnerRole);

        await ChangeAsync(f, caller, AdminRole);

        Assert.Equal([AdminRole.Id], f.RoleIdsOf(caller));
        await f.AssertInvalidatedAfterCommitAsync(caller);
    }

    // ChangeRolesAsync: unchanged set

    [Fact]
    public async Task ChangeRolesAsync_SameSet_NoWriteNoRecordNoInvalidate()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Oanh", OwnerRole);
        Account member = f.AddMember("Minh", MemberRole);

        await ChangeAsync(f, member, MemberRole);

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        await f.AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task ChangeRolesAsync_SameSetWithDuplicateIdsInDifferentOrder_NoWriteNoRecordNoInvalidate()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Oanh", OwnerRole);
        Account member = f.AddMember("Minh", AdminRole, MemberRole);

        await ChangeAsync(f, member, MemberRole, AdminRole, MemberRole);

        await f.AssertNothingWrittenAsync();
    }

    // BR-58 runs before the comparison: an unchanged set is no way to probe what one may not touch.
    [Fact]
    public async Task ChangeRolesAsync_SameSetButCallerLacksPermission_StillThrows403()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Nhân", PeopleRole);
        Account admin = f.AddMember("Quân", AdminRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => ChangeAsync(f, admin, AdminRole));

        await f.AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task ChangeRolesAsync_SameSetForMissingMember_StillThrows404()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Oanh", OwnerRole);
        Account former = f.AddMember("Minh", MemberRole);
        f.Deactivate(former);

        await ApplicationAssert.AssertNotFoundAsync<OrganizationMember>(
            former.Id, () => ChangeAsync(f, former, MemberRole));

        await f.AssertNothingWrittenAsync();
    }

    // ChangeRolesAsync: audit

    // F14: the reader's display order is kept as is, never the request's order (ICU order: Infrastructure tests).
    [Fact]
    public async Task ChangeRolesAsync_RolesInAuditEntry_KeepReaderOrder()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Oanh", OwnerRole);
        Account member = f.AddMember("Minh", TesterRole, MemberRole);

        await ChangeAsync(f, member, TesterRole, PeopleRole, AdminRole);

        AuditEvent changed = Assert.Single(f.Recorded);
        Assert.Equal([Ref(MemberRole), Ref(TesterRole)], (IReadOnlyList<AuditRoleRef>)changed.Parameters["rolesBefore"]);
        Assert.Equal(
            [Ref(AdminRole), Ref(PeopleRole), Ref(TesterRole)],
            (IReadOnlyList<AuditRoleRef>)changed.Parameters["rolesAfter"]);
    }

    // AC-367
    [Fact]
    public async Task ChangeRolesAsync_Success_RecordsBeforeAfterThenInvalidates()
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Nam", OwnerRole);
        Account member = f.AddMember("Minh", MemberRole);

        await ChangeAsync(f, member, AdminRole);

        Assert.Equal([AdminRole.Id], f.RoleIdsOf(member));
        AuditEvent changed = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.MemberRolesChanged, changed.Type);
        Assert.Equal(f.Organization.Id, changed.OrganizationId);
        Assert.Equal(f.MemberOf(member).Id, changed.SubjectId);
        Assert.Equal(ActorKind.Account, changed.ActorKind);
        Assert.Equal(caller.Id, changed.ActorAccountId);
        Assert.Equal("Minh", changed.Parameters["accountName"]);
        Assert.Equal(member.Email, changed.Parameters["email"]);
        Assert.Equal([Ref(MemberRole)], (IReadOnlyList<AuditRoleRef>)changed.Parameters["rolesBefore"]);
        Assert.Equal([Ref(AdminRole)], (IReadOnlyList<AuditRoleRef>)changed.Parameters["rolesAfter"]);
        AuditEventCatalog.Validate(changed);
        Received.InOrder(() =>
        {
            f.OrganizationLock.AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.Reader.LoadCallerAsync(f.Organization.Id, caller.Id, Arg.Any<CancellationToken>());
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await f.AssertInvalidatedAfterCommitAsync(member);
    }

    // RemoveAsync

    // AC-158
    [Fact]
    public async Task RemoveAsync_AdminRemovesOwner_ThrowsOwnerOnly()
    {
        Fixture f = CreateFixture();
        f.AddCaller("An", AdminRole);
        f.AddMember("Oanh", OwnerRole);
        Account secondOwner = f.AddMember("Ông", OwnerRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OwnerOnlyOperation, () => f.Service.RemoveAsync(secondOwner.Id, CancellationToken.None));

        await f.AssertNothingWrittenAsync();
        Assert.True(f.IsActive(secondOwner));
        Assert.Equal([OwnerRole.Id], f.RoleIdsOf(secondOwner));
    }

    [Fact]
    public async Task RemoveAsync_ManagerLacksPermissionsOfRole_ThrowsInsufficient()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Nhân", PeopleRole);
        Account admin = f.AddMember("Quân", AdminRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => f.Service.RemoveAsync(admin.Id, CancellationToken.None));

        await f.AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task RemoveAsync_MissingMember_Throws404()
    {
        Fixture f = CreateFixture();
        f.AddCaller("Oanh", OwnerRole);
        Guid stranger = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<OrganizationMember>(
            stranger, () => f.Service.RemoveAsync(stranger, CancellationToken.None));

        await f.AssertNothingWrittenAsync();
    }

    // AC-337, AC-370
    [Fact]
    public async Task RemoveAsync_Self_Throws409CannotRemoveSelfAndRecordsNothing()
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Nhân", PeopleRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.CannotRemoveSelf, () => f.Service.RemoveAsync(caller.Id, CancellationToken.None));

        await f.AssertNothingWrittenAsync();
        Assert.True(f.IsActive(caller));
        Assert.Equal([PeopleRole.Id], f.RoleIdsOf(caller));
    }

    // D2 order pin only: one's own roles are always within reach, so this state needs a stubbed reader.
    [Fact]
    public async Task RemoveAsync_SelfWhenBr58Denies_Throws403Not409()
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Nhân", AdminRole);
        f.CallerOverride =
            new RoleGrantCaller(caller.Id, false, new HashSet<string> { Permissions.OrganizationMembersManage });

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => f.Service.RemoveAsync(caller.Id, CancellationToken.None));

        await f.AssertNothingWrittenAsync();
    }

    // AC-69 (member part), BR-30, AC-367
    [Fact]
    public async Task RemoveAsync_Success_DeactivatesDeletesRolesRecordsRemoved()
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Nam", OwnerRole);
        Account removed = f.AddMember("Vân", TesterRole, MemberRole);

        await f.Service.RemoveAsync(removed.Id, CancellationToken.None);

        Assert.False(f.IsActive(removed));
        Assert.Empty(f.RoleIdsOf(removed));
        f.Members.Received(1).Update(f.MemberOf(removed));
        AuditEvent entry = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.MemberRemoved, entry.Type);
        Assert.Equal(f.MemberOf(removed).Id, entry.SubjectId);
        Assert.Equal(f.Organization.Id, entry.OrganizationId);
        Assert.Equal(ActorKind.Account, entry.ActorKind);
        Assert.Equal(caller.Id, entry.ActorAccountId);
        Assert.Equal("Vân", entry.Parameters["accountName"]);
        Assert.Equal(removed.Email, entry.Parameters["email"]);
        Assert.Equal([Ref(MemberRole), Ref(TesterRole)], (IReadOnlyList<AuditRoleRef>)entry.Parameters["roles"]);
        AuditEventCatalog.Validate(entry);
        Received.InOrder(() =>
        {
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await f.AssertInvalidatedAfterCommitAsync(removed);
    }

    // LeaveAsync

    // AC-277
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeaveAsync_LastActiveOwner_Throws409(bool inactiveOtherOwner)
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Oanh", OwnerRole);
        if (inactiveOtherOwner)
        {
            f.Deactivate(f.AddMember("Cựu", OwnerRole));
        }

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.LastOwnerCannotLeave, () => f.Service.LeaveAsync(CancellationToken.None));

        await f.AssertNothingWrittenAsync();
        Assert.True(f.IsActive(caller));
        Assert.Equal([OwnerRole.Id], f.RoleIdsOf(caller));
    }

    // AC-278, AC-279
    [Fact]
    public async Task LeaveAsync_OneOfTwoOwners_SucceedsAndRecordsLeft()
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Oanh", OwnerRole, TesterRole);
        Account otherOwner = f.AddMember("Ông", OwnerRole);

        await f.Service.LeaveAsync(CancellationToken.None);

        Assert.False(f.IsActive(caller));
        Assert.Empty(f.RoleIdsOf(caller));
        Assert.True(f.IsActive(otherOwner));
        Assert.Equal([OwnerRole.Id], f.RoleIdsOf(otherOwner));
        AuditEvent entry = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.MemberLeft, entry.Type);
        Assert.Equal(f.MemberOf(caller).Id, entry.SubjectId);
        Assert.Equal(caller.Id, entry.ActorAccountId);
        Assert.Equal(ActorKind.Account, entry.ActorKind);
        Assert.Equal("Oanh", entry.Parameters["accountName"]);
        Assert.Equal(caller.Email, entry.Parameters["email"]);
        AuditEventCatalog.Validate(entry);
        await f.AssertInvalidatedAfterCommitAsync(caller);
    }

    // BR-58 does not apply: whoever leaves gives up only their own permissions.
    [Fact]
    public async Task LeaveAsync_PlainMember_Succeeds()
    {
        Fixture f = CreateFixture();
        Account caller = f.AddCaller("Minh", MemberRole);
        f.AddMember("Oanh", OwnerRole);

        await f.Service.LeaveAsync(CancellationToken.None);

        Assert.False(f.IsActive(caller));
        Assert.Empty(f.RoleIdsOf(caller));
        await f.AssertInvalidatedAfterCommitAsync(caller);
    }
}
