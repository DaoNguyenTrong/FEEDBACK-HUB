using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Roles;

public class RoleServiceWriteTests
{
    public enum Operation
    {
        Create,
        Update,
        Delete
    }

    /// <summary>An in-memory organization behind substitutes, so tests assert both calls and resulting state.</summary>
    private sealed record Fixture
    {
        private readonly List<OrganizationMember> members = [];
        private readonly List<Role> roles = [];
        private readonly List<RolePermission> rolePermissions = [];
        private readonly Dictionary<Guid, int> holderCounts = [];
        private readonly List<bool> invalidatedInsideTransaction = [];
        private bool inTransaction;

        public Fixture()
        {
            Organization = Organization.Create(new OrganizationParams("Bảo Minh", "bao-minh"));
            CallerMember = OrganizationMember.Create(new OrganizationMemberParams(Organization.Id, CallerId));
            members.Add(CallerMember);

            foreach (SystemRoleKind kind in Enum.GetValues<SystemRoleKind>())
            {
                Role role = Role.Create(new RoleParams(Organization.Id, kind.ToString(), kind));
                roles.Add(role);
                SystemRoles[kind] = role;
            }

            AddCodes(SystemRoles[SystemRoleKind.Admin], Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin);

            UnitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => RunInTransactionAsync(
                    callInfo.Arg<Func<CancellationToken, Task>>()!, callInfo.Arg<CancellationToken>()));
            UnitOfWork.Repository<Organization, Guid>().Returns(Organizations);
            UnitOfWork.Repository<OrganizationMember, Guid>().Returns(Members);
            UnitOfWork.Repository<Role, Guid>().Returns(Roles);
            UnitOfWork.Repository<RolePermission, Guid>().Returns(RolePermissions);

            Organizations.GetByIdAsync(Organization.Id, Arg.Any<CancellationToken>()).Returns(_ => Organization);
            RepositoryPredicateStub.StubFirstOrDefault(Members, members);
            RepositoryPredicateStub.StubFirstOrDefault(Roles, roles);
            RepositoryPredicateStub.StubListAsync(RolePermissions, rolePermissions);
            Roles.When(repository => repository.AddAsync(Arg.Any<Role>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => roles.Add(callInfo.Arg<Role>()!));
            Roles.When(repository => repository.Delete(Arg.Any<Role>()))
                .Do(callInfo => roles.Remove(callInfo.Arg<Role>()!));
            RolePermissions.When(repository => repository.AddAsync(Arg.Any<RolePermission>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => rolePermissions.Add(callInfo.Arg<RolePermission>()!));
            RolePermissions.When(repository => repository.Delete(Arg.Any<RolePermission>()))
                .Do(callInfo => rolePermissions.Remove(callInfo.Arg<RolePermission>()!));

            Reader.LoadCallerAsync(Organization.Id, CallerId, Arg.Any<CancellationToken>())
                .Returns(_ => Caller);
            Reader.LoadRolesAsync(Organization.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    IReadOnlyCollection<Guid> ids = callInfo.ArgAt<IReadOnlyCollection<Guid>>(1);
                    return (IReadOnlyList<RoleGrantRole>)roles
                        .Where(role => role.OrganizationId == Organization.Id && ids.Contains(role.Id))
                        .Select(role => new RoleGrantRole(
                            role.Id, role.Name, role.SystemRoleKind, EffectiveCodes(role).ToHashSet()))
                        .ToList();
                });
            Queries.GetAsync(Organization.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => roles
                    .Where(role => role.OrganizationId == Organization.Id && role.Id == callInfo.ArgAt<Guid>(1))
                    .Select(role => new RoleRow(
                        role.Id, role.Name, role.SystemRoleKind, EffectiveCodes(role),
                        holderCounts.GetValueOrDefault(role.Id)))
                    .SingleOrDefault());
            Queries.IsNameTakenAsync(
                    Organization.Id, Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => roles.Any(role =>
                    role.OrganizationId == Organization.Id
                    && role.Id != callInfo.ArgAt<Guid?>(2)
                    && role.Name.ToLowerInvariant() == callInfo.ArgAt<string>(1).ToLowerInvariant()));

            PermissionResolver.When(resolver => resolver.InvalidateOrganizationAsync(
                    Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => invalidatedInsideTransaction.Add(inTransaction));
            AuditTrail.When(trail => trail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => Recorded.Add(callInfo.Arg<AuditEvent>()!));

            // Anything read before the lock could be stale by the time the lock is held (TECH00 §5.4).
            OrganizationLock.When(organizationLock => organizationLock.AcquireAsync(
                    Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => ReadBeforeLock = ReadBeforeLock
                    || Reader.ReceivedCalls().Any()
                    || Queries.ReceivedCalls().Any()
                    || Members.ReceivedCalls().Any()
                    || Roles.ReceivedCalls().Any()
                    || RolePermissions.ReceivedCalls().Any()
                    || Organizations.ReceivedCalls().Any());

            Tenant.OrganizationId.Returns(_ => Organization.Id);
            CurrentUser.UserId.Returns(CallerId.ToString());

            Service = new RoleService(
                UnitOfWork, CurrentUser, Tenant, Queries, Reader, OrganizationLock, AuditTrail, PermissionResolver);
        }

        public Guid CallerId { get; } = Guid.NewGuid();

        public Organization Organization { get; }

        public OrganizationMember CallerMember { get; }

        public Dictionary<SystemRoleKind, Role> SystemRoles { get; } = [];

        public RoleService Service { get; }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IRepository<Organization, Guid> Organizations { get; } = Substitute.For<IRepository<Organization, Guid>>();

        public IRepository<OrganizationMember, Guid> Members { get; } =
            Substitute.For<IRepository<OrganizationMember, Guid>>();

        public IRepository<Role, Guid> Roles { get; } = Substitute.For<IRepository<Role, Guid>>();

        public IRepository<RolePermission, Guid> RolePermissions { get; } =
            Substitute.For<IRepository<RolePermission, Guid>>();

        public IRoleGrantReader Reader { get; } = Substitute.For<IRoleGrantReader>();

        public IRoleQueries Queries { get; } = Substitute.For<IRoleQueries>();

        public IOrganizationLock OrganizationLock { get; } = Substitute.For<IOrganizationLock>();

        public IAuditTrail AuditTrail { get; } = Substitute.For<IAuditTrail>();

        public IPermissionResolver PermissionResolver { get; } = Substitute.For<IPermissionResolver>();

        public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();

        public ICurrentTenantProvider Tenant { get; } = Substitute.For<ICurrentTenantProvider>();

        public List<AuditEvent> Recorded { get; } = [];

        public bool ReadBeforeLock { get; private set; }

        public RoleGrantCaller Caller { get; private set; } = null!;

        public void SetCaller(bool isOwner, params string[] permissions) =>
            Caller = new RoleGrantCaller(CallerId, isOwner, isOwner ? Permissions.All.ToHashSet() : permissions.ToHashSet());

        public void SetOwnerCaller() => SetCaller(isOwner: true);

        public Role AddCustomRole(string name, params string[] codes) => AddRole(Organization.Id, name, codes);

        public Role AddForeignRole(string name) => AddRole(Guid.NewGuid(), name, []);

        public void SetHolders(Role role, int count) => holderCounts[role.Id] = count;

        public void DeactivateCaller() => CallerMember.Deactivate();

        public IReadOnlyList<string> StoredCodes(Role role) =>
            rolePermissions.Where(link => link.RoleId == role.Id).Select(link => link.PermissionCode).ToList();

        public bool Exists(Guid roleId) => roles.Any(role => role.Id == roleId);

        public Role? FindByName(string name) => roles.SingleOrDefault(role => role.Name == name);

        public Task<RoleDto> CreateAsync(string name, params string[] codes) =>
            Service.CreateAsync(new CreateRoleRequest(name, codes), CancellationToken.None);

        public Task<RoleDto> UpdateAsync(Role role, string name, params string[] codes) =>
            Service.UpdateAsync(role.Id, new UpdateRoleRequest(name, codes), CancellationToken.None);

        public Task DeleteAsync(Role role) => Service.DeleteAsync(role.Id, CancellationToken.None);

        public Task RunAsync(Operation operation, Role target) =>
            operation switch
            {
                Operation.Create => CreateAsync("Hỗ trợ", Permissions.FilesManage),
                Operation.Update => UpdateAsync(target, "Kiểm thử", Permissions.FilesManage),
                _ => DeleteAsync(target)
            };

        public async Task AssertNothingWrittenAsync()
        {
            await UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            await Roles.DidNotReceive().AddAsync(Arg.Any<Role>(), Arg.Any<CancellationToken>());
            Roles.DidNotReceive().Update(Arg.Any<Role>());
            Roles.DidNotReceive().Delete(Arg.Any<Role>());
            await RolePermissions.DidNotReceive().AddAsync(Arg.Any<RolePermission>(), Arg.Any<CancellationToken>());
            RolePermissions.DidNotReceive().Delete(Arg.Any<RolePermission>());
            await PermissionResolver.DidNotReceive()
                .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        // Invalidating before the commit lets a request in between cache the old permissions again.
        public async Task AssertInvalidatedAfterCommitAsync()
        {
            await PermissionResolver.Received(1).InvalidateOrganizationAsync(Organization.Id, Arg.Any<CancellationToken>());
            Assert.Equal([false], invalidatedInsideTransaction);
        }

        private Role AddRole(Guid organizationId, string name, IReadOnlyList<string> codes)
        {
            Role role = Role.Create(new RoleParams(organizationId, name));
            roles.Add(role);
            AddCodes(role, codes.ToArray());
            return role;
        }

        private void AddCodes(Role role, params string[] codes)
        {
            foreach (string code in codes)
            {
                rolePermissions.Add(RolePermission.Create(new RolePermissionParams(role.Id, code)));
            }
        }

        private IReadOnlyList<string> EffectiveCodes(Role role) =>
            SystemRoleDefaults.EffectivePermissionCodes(role.SystemRoleKind, StoredCodes(role));

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
    }

    private static Fixture CreateFixture()
    {
        Fixture fixture = new();
        fixture.SetOwnerCaller();
        return fixture;
    }

    // Lock and gate re-check (C3, F11): every write

    [Theory]
    [InlineData(Operation.Create)]
    [InlineData(Operation.Update)]
    [InlineData(Operation.Delete)]
    public async Task Write_Any_AcquiresLockBeforeAnyRead(Operation operation)
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);

        await f.RunAsync(operation, tester);

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        Assert.False(f.ReadBeforeLock);
    }

    // Removed between the gate and the lock: refused the way the gate now would, nothing written.
    [Theory]
    [InlineData(Operation.Create)]
    [InlineData(Operation.Update)]
    [InlineData(Operation.Delete)]
    public async Task Write_CallerNoLongerActiveMemberAfterGate_Throws403AccessDenied(Operation operation)
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);
        f.DeactivateCaller();

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => f.RunAsync(operation, tester));

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        await f.AssertNothingWrittenAsync();
        Assert.True(f.Exists(tester.Id));
    }

    [Theory]
    [InlineData(Operation.Create)]
    [InlineData(Operation.Update)]
    [InlineData(Operation.Delete)]
    public async Task Write_OrganizationSuspendedAfterGate_Throws403SuspendedWithName(Operation operation)
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);
        f.Organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));

        ForbiddenException exception =
            await Assert.ThrowsAsync<ForbiddenException>(() => f.RunAsync(operation, tester));

        Assert.Equal(ApplicationMessages.OrganizationSuspended, exception.Message);
        Assert.Equal("Bảo Minh", exception.Extensions[ProblemExtensionKeys.OrganizationName]);
        await f.AssertNothingWrittenAsync();
    }

    // BR-58 inputs come from the database after the lock, never from the permission cache (TECH00 §5.3).
    [Theory]
    [InlineData(Operation.Create)]
    [InlineData(Operation.Update)]
    [InlineData(Operation.Delete)]
    public async Task Write_Any_ReadsCallerFromReaderNotResolver(Operation operation)
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);

        await f.RunAsync(operation, tester);

        await f.Reader.Received(1).LoadCallerAsync(f.Organization.Id, f.CallerId, Arg.Any<CancellationToken>());
        await f.PermissionResolver.DidNotReceive().GetEffectivePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(Operation.Create)]
    [InlineData(Operation.Update)]
    public async Task Write_UnknownPermissionCode_Throws400BeforeLock(Operation operation)
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester");

        Task Act() => operation == Operation.Create
            ? f.CreateAsync("Hỗ trợ", Permissions.FilesManage, "feedback.delete")
            : f.UpdateAsync(tester, "Tester", "feedback.delete");

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(ApplicationMessages.UnknownPermissionCode, Act);

        await f.OrganizationLock.DidNotReceive().AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await f.AssertNothingWrittenAsync();
    }

    // CreateAsync

    // AC-156
    [Fact]
    public async Task CreateAsync_PermissionCallerLacks_Throws403AndCreatesNothing()
    {
        Fixture f = CreateFixture();
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => f.CreateAsync("Trưởng IT", Permissions.FeedbackHubAdmin));

        await f.AssertNothingWrittenAsync();
        Assert.Null(f.FindByName("Trưởng IT"));
    }

    // Task 1 carry: feedbackhub.admin goes onto a custom role only from a caller who holds it (BR-58 point 2).
    [Fact]
    public async Task CreateAsync_FeedbackHubAdminHeldByCaller_Succeeds()
    {
        Fixture f = CreateFixture();
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage, Permissions.FeedbackHubAdmin);

        RoleDto created = await f.CreateAsync("Trưởng IT", Permissions.FeedbackHubAdmin);

        Assert.Equal([Permissions.FeedbackHubAdmin], created.PermissionCodes);
        Assert.Equal([Permissions.FeedbackHubAdmin], f.StoredCodes(f.FindByName("Trưởng IT")!));
    }

    // BR-58 is checked before the name, so a caller out of reach learns nothing about existing names (D2).
    [Fact]
    public async Task CreateAsync_PermissionCallerLacksAndNameTaken_Throws403Not409()
    {
        Fixture f = CreateFixture();
        f.AddCustomRole("Tester");
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => f.CreateAsync("tester", Permissions.FilesManage));
    }

    // AC-291: either language, any case, padding and NFD alike.
    [Theory]
    [InlineData("Owner")]
    [InlineData("chủ sở hữu")]
    [InlineData("Chủ sở hữu")]
    [InlineData("  ADMIN ")]
    [InlineData("member")]
    public async Task CreateAsync_ReservedName_Throws409(string name)
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.RoleNameReservedForSystemRole, () => f.CreateAsync(name));

        await f.AssertNothingWrittenAsync();
    }

    // AC-290
    [Fact]
    public async Task CreateAsync_NameDiffersOnlyByCase_Throws409()
    {
        Fixture f = CreateFixture();
        f.AddCustomRole("Tester");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationRoleNameAlreadyExists, () => f.CreateAsync("  tester "));

        await f.Queries.Received(1).IsNameTakenAsync(f.Organization.Id, "tester", null, Arg.Any<CancellationToken>());
        await f.AssertNothingWrittenAsync();
    }

    // The name is compared trimmed and NFC, as Role.Update stores it.
    [Fact]
    public async Task CreateAsync_NfdName_ChecksAndStoresNfc()
    {
        Fixture f = CreateFixture();

        RoleDto created = await f.CreateAsync("Kiểm thử");

        Assert.Equal("Kiểm thử", created.Name);
        await f.Queries.Received(1).IsNameTakenAsync(f.Organization.Id, "Kiểm thử", null, Arg.Any<CancellationToken>());
    }

    // AC-289
    [Fact]
    public async Task CreateAsync_100CharName_Succeeds()
    {
        Fixture f = CreateFixture();
        string name = new('ư', 100);

        RoleDto created = await f.CreateAsync(name);

        Assert.Equal(name, created.Name);
        Assert.NotNull(f.FindByName(name));
    }

    [Fact]
    public async Task CreateAsync_Success_RecordsRoleCreatedAndReturnsDto()
    {
        Fixture f = CreateFixture();

        RoleDto created = await f.CreateAsync(
            "Hỗ trợ", Permissions.FilesManage, Permissions.OrganizationMembersManage, Permissions.FilesManage);

        Role stored = Assert.IsType<Role>(f.FindByName("Hỗ trợ"));
        Assert.Equal(f.Organization.Id, stored.OrganizationId);
        Assert.Null(stored.SystemRoleKind);
        Assert.Equal(
            new[] { Permissions.OrganizationMembersManage, Permissions.FilesManage }.Order(), f.StoredCodes(stored).Order());

        Assert.Equal(stored.Id, created.Id);
        Assert.Equal(0, created.HolderCount);
        Assert.False(created.IsSystem);
        Assert.True(created.CanEdit);
        Assert.True(created.CanDelete);

        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.RoleCreated, recorded.Type);
        Assert.Equal(f.Organization.Id, recorded.OrganizationId);
        Assert.Equal(stored.Id, recorded.SubjectId);
        Assert.Equal(ActorKind.Account, recorded.ActorKind);
        Assert.Equal(f.CallerId, recorded.ActorAccountId);
        Assert.Equal(new AuditRoleRef(stored.Id, "Hỗ trợ", null), recorded.Parameters["role"]);
        Assert.Equal(
            [Permissions.OrganizationMembersManage, Permissions.FilesManage],
            (IReadOnlyList<string>)recorded.Parameters["permissionCodes"]);

        Received.InOrder(() =>
        {
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await f.PermissionResolver.DidNotReceive()
            .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // UpdateAsync

    [Fact]
    public async Task UpdateAsync_RoleOfOtherOrganizationOrUnknown_Throws404()
    {
        Fixture f = CreateFixture();
        Role foreign = f.AddForeignRole("Tester");
        Guid unknown = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<Role>(foreign.Id, () => f.UpdateAsync(foreign, "Kiểm thử"));
        await ApplicationAssert.AssertNotFoundAsync<Role>(
            unknown, () => f.Service.UpdateAsync(unknown, new UpdateRoleRequest("Kiểm thử", []), CancellationToken.None));

        await f.AssertNothingWrittenAsync();
    }

    // AC-55, AC-159: the system-role reason wins over BR-58, even for a caller missing the role's permissions.
    [Theory]
    [InlineData(SystemRoleKind.Owner)]
    [InlineData(SystemRoleKind.Admin)]
    [InlineData(SystemRoleKind.Member)]
    public async Task UpdateAsync_SystemRole_Throws403ImmutableBeforeBr58(SystemRoleKind kind)
    {
        Fixture f = CreateFixture();
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage, Permissions.OrganizationMembersManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationSystemRoleImmutable,
            () => f.UpdateAsync(f.SystemRoles[kind], "Admin", Permissions.OrganizationMembersManage));

        await f.AssertNothingWrittenAsync();
        Assert.Equal(
            [Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin],
            f.StoredCodes(f.SystemRoles[SystemRoleKind.Admin]));
    }

    [Fact]
    public async Task UpdateAsync_NoChangeOnSystemRole_StillThrows403Immutable()
    {
        Fixture f = CreateFixture();
        Role admin = f.SystemRoles[SystemRoleKind.Admin];

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationSystemRoleImmutable,
            () => f.UpdateAsync(admin, "Admin", Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin));

        await f.AssertNothingWrittenAsync();
    }

    // AC-295
    [Fact]
    public async Task UpdateAsync_RenameRoleWithPermissionCallerLacks_Throws403()
    {
        Fixture f = CreateFixture();
        Role lead = f.AddCustomRole("Trưởng IT", Permissions.FeedbackHubAdmin);
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => f.UpdateAsync(lead, "Trưởng nhóm IT", Permissions.FeedbackHubAdmin));

        await f.AssertNothingWrittenAsync();
    }

    // AC-296
    [Fact]
    public async Task UpdateAsync_AddPermissionCallerHasButRoleHasOneCallerLacks_Throws403()
    {
        Fixture f = CreateFixture();
        Role lead = f.AddCustomRole("Trưởng IT", Permissions.FeedbackHubAdmin);
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage, Permissions.FilesManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => f.UpdateAsync(lead, "Trưởng IT", Permissions.FeedbackHubAdmin, Permissions.FilesManage));

        await f.AssertNothingWrittenAsync();
        Assert.Equal([Permissions.FeedbackHubAdmin], f.StoredCodes(lead));
    }

    // AC-198
    [Fact]
    public async Task UpdateAsync_RemovePermissionCallerLacks_Throws403()
    {
        Fixture f = CreateFixture();
        Role lead = f.AddCustomRole("Trưởng IT", Permissions.FeedbackHubAdmin, Permissions.FilesManage);
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage, Permissions.FilesManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => f.UpdateAsync(lead, "Trưởng IT", Permissions.FilesManage));

        await f.AssertNothingWrittenAsync();
    }

    // BR-58 point 2: a role within reach still cannot gain a permission the caller lacks.
    [Fact]
    public async Task UpdateAsync_AddPermissionCallerLacks_Throws403()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage, Permissions.FilesManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => f.UpdateAsync(tester, "Tester", Permissions.FilesManage, Permissions.AuditLogsView));

        await f.AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task UpdateAsync_NoChangeButCallerLacksPermission_StillThrows403()
    {
        Fixture f = CreateFixture();
        Role lead = f.AddCustomRole("Trưởng IT", Permissions.FeedbackHubAdmin);
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions,
            () => f.UpdateAsync(lead, "Trưởng IT", Permissions.FeedbackHubAdmin));

        await f.AssertNothingWrittenAsync();
    }

    // AC-297
    [Fact]
    public async Task UpdateAsync_OwnerRenames_Succeeds()
    {
        Fixture f = CreateFixture();
        Role lead = f.AddCustomRole("Trưởng IT", Permissions.FeedbackHubAdmin);
        f.SetHolders(lead, 2);

        RoleDto updated = await f.UpdateAsync(lead, "Trưởng nhóm IT", Permissions.FeedbackHubAdmin);

        Assert.Equal("Trưởng nhóm IT", updated.Name);
        Assert.Equal(2, updated.HolderCount);
        Assert.Equal("Trưởng nhóm IT", lead.Name);
        f.Roles.Received(1).Update(lead);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // AC-326
    [Fact]
    public async Task UpdateAsync_RenameToOtherRoleUppercase_Throws409()
    {
        Fixture f = CreateFixture();
        f.AddCustomRole("Tester");
        Role review = f.AddCustomRole("Kiểm thử");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationRoleNameAlreadyExists, () => f.UpdateAsync(review, "TESTER"));

        await f.Queries.Received(1).IsNameTakenAsync(f.Organization.Id, "TESTER", review.Id, Arg.Any<CancellationToken>());
        await f.AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task UpdateAsync_RenameToReservedName_Throws409Reserved()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.RoleNameReservedForSystemRole, () => f.UpdateAsync(tester, "Chủ Sở Hữu"));

        await f.AssertNothingWrittenAsync();
    }

    // The role's own name never counts as taken, so its case can change.
    [Fact]
    public async Task UpdateAsync_ChangeOnlyCaseOfOwnName_Succeeds()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester");

        RoleDto updated = await f.UpdateAsync(tester, "TESTER");

        Assert.Equal("TESTER", updated.Name);
        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal("Tester", recorded.Parameters["nameBefore"]);
        Assert.Equal("TESTER", recorded.Parameters["nameAfter"]);
    }

    // AC-293 (role part): a rename touches neither the permissions nor the holders.
    [Fact]
    public async Task UpdateAsync_RenameKeepsHoldersAndPermissions()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);
        f.SetHolders(tester, 1);

        RoleDto updated = await f.UpdateAsync(tester, "Kiểm thử", Permissions.FilesManage);

        Assert.Equal([Permissions.FilesManage], updated.PermissionCodes);
        Assert.Equal(1, updated.HolderCount);
        Assert.Equal([Permissions.FilesManage], f.StoredCodes(tester));
        await f.RolePermissions.DidNotReceive().AddAsync(Arg.Any<RolePermission>(), Arg.Any<CancellationToken>());
        f.RolePermissions.DidNotReceive().Delete(Arg.Any<RolePermission>());
        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Empty((IReadOnlyList<string>)recorded.Parameters["permissionsAdded"]);
        Assert.Empty((IReadOnlyList<string>)recorded.Parameters["permissionsRemoved"]);
    }

    [Fact]
    public async Task UpdateAsync_NoChange_ReturnsCurrentDtoNoRecordNoInvalidate()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);
        f.SetHolders(tester, 3);

        RoleDto current = await f.UpdateAsync(tester, "Tester", Permissions.FilesManage);

        Assert.Equal(tester.Id, current.Id);
        Assert.Equal("Tester", current.Name);
        Assert.Equal(3, current.HolderCount);
        await f.AssertNothingWrittenAsync();
    }

    // The name is compared after trimming, the permissions as a set: order and duplicates do not count.
    [Fact]
    public async Task UpdateAsync_NameChangesOnlyByTrailingSpaceAndSamePermissionSet_NoRecordNoInvalidate()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage, Permissions.AuditLogsView);

        RoleDto current = await f.UpdateAsync(
            tester, "Tester  ", Permissions.AuditLogsView, Permissions.FilesManage, Permissions.AuditLogsView);

        Assert.Equal("Tester", current.Name);
        await f.AssertNothingWrittenAsync();
    }

    // The no-op answer still runs the name checks: a role stuck on a reserved name cannot pass them.
    [Fact]
    public async Task UpdateAsync_NoChangeWithReservedName_StillThrows409()
    {
        Fixture f = CreateFixture();
        Role legacy = f.AddCustomRole("owner");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.RoleNameReservedForSystemRole, () => f.UpdateAsync(legacy, "owner"));
    }

    [Fact]
    public async Task UpdateAsync_Success_RecordsAddedAndRemovedThenInvalidates()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage, Permissions.AuditLogsView);

        RoleDto updated = await f.UpdateAsync(
            tester, "Kiểm thử", Permissions.AuditLogsView, Permissions.SettingsManage, Permissions.ApiKeysManage);

        Assert.Equal(
            new[] { Permissions.ApiKeysManage, Permissions.AuditLogsView, Permissions.SettingsManage }.Order(),
            f.StoredCodes(tester).Order());
        Assert.Equal(
            new[] { Permissions.ApiKeysManage, Permissions.AuditLogsView, Permissions.SettingsManage }.Order(),
            updated.PermissionCodes.Order());

        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.RoleUpdated, recorded.Type);
        Assert.Equal(f.Organization.Id, recorded.OrganizationId);
        Assert.Equal(tester.Id, recorded.SubjectId);
        Assert.Equal(f.CallerId, recorded.ActorAccountId);
        Assert.Equal("Tester", recorded.Parameters["nameBefore"]);
        Assert.Equal("Kiểm thử", recorded.Parameters["nameAfter"]);
        Assert.Equal(
            [Permissions.ApiKeysManage, Permissions.SettingsManage],
            (IReadOnlyList<string>)recorded.Parameters["permissionsAdded"]);
        Assert.Equal([Permissions.FilesManage], (IReadOnlyList<string>)recorded.Parameters["permissionsRemoved"]);

        Received.InOrder(() =>
        {
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await f.AssertInvalidatedAfterCommitAsync();
    }

    // A permission-only change leaves the role row alone.
    [Fact]
    public async Task UpdateAsync_PermissionsOnly_DoesNotRewriteRoleRow()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);

        await f.UpdateAsync(tester, "Tester", Permissions.FilesManage, Permissions.AuditLogsView);

        f.Roles.DidNotReceive().Update(Arg.Any<Role>());
        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal("Tester", recorded.Parameters["nameAfter"]);
        await f.AssertInvalidatedAfterCommitAsync();
    }

    // DeleteAsync

    [Fact]
    public async Task DeleteAsync_RoleOfOtherOrganizationOrUnknown_Throws404()
    {
        Fixture f = CreateFixture();
        Role foreign = f.AddForeignRole("Tester");
        Guid unknown = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<Role>(foreign.Id, () => f.DeleteAsync(foreign));
        await ApplicationAssert.AssertNotFoundAsync<Role>(
            unknown, () => f.Service.DeleteAsync(unknown, CancellationToken.None));

        await f.AssertNothingWrittenAsync();
        Assert.True(f.Exists(foreign.Id));
    }

    [Theory]
    [InlineData(SystemRoleKind.Owner)]
    [InlineData(SystemRoleKind.Admin)]
    [InlineData(SystemRoleKind.Member)]
    public async Task DeleteAsync_SystemRole_Throws403Immutable(SystemRoleKind kind)
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationSystemRoleImmutable, () => f.DeleteAsync(f.SystemRoles[kind]));

        await f.AssertNothingWrittenAsync();
    }

    // AC-248, AC-251, AC-256: even an Owner cannot delete a role someone still holds.
    [Fact]
    public async Task DeleteAsync_HasActiveHolder_Throws409()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);
        f.SetHolders(tester, 1);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.RoleHasHolders, () => f.DeleteAsync(tester));

        await f.AssertNothingWrittenAsync();
        Assert.True(f.Exists(tester.Id));
    }

    // AC-252: holders are counted by the query over active members only, so zero here means none active.
    [Fact]
    public async Task DeleteAsync_NoActiveHolder_Deletes()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);
        f.SetHolders(tester, 0);

        await f.DeleteAsync(tester);

        Assert.False(f.Exists(tester.Id));
        await f.Queries.Received(1).GetAsync(f.Organization.Id, tester.Id, Arg.Any<CancellationToken>());
    }

    // AC-257, AC-249: BR-58 answers before the holder check (D2).
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task DeleteAsync_CallerLacksPermission_Throws403Before409(int holders)
    {
        Fixture f = CreateFixture();
        Role lead = f.AddCustomRole("Trưởng IT", Permissions.FeedbackHubAdmin);
        f.SetHolders(lead, holders);
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => f.DeleteAsync(lead));

        await f.AssertNothingWrittenAsync();
        Assert.True(f.Exists(lead.Id));
    }

    // AC-247
    [Fact]
    public async Task DeleteAsync_Success_RecordsRoleDeletedBeforeDeleteThenInvalidates()
    {
        Fixture f = CreateFixture();
        Role tester = f.AddCustomRole("Tester", Permissions.FilesManage);
        f.SetCaller(isOwner: false, Permissions.OrganizationRolesManage, Permissions.FilesManage);

        await f.DeleteAsync(tester);

        Assert.False(f.Exists(tester.Id));
        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.RoleDeleted, recorded.Type);
        Assert.Equal(f.Organization.Id, recorded.OrganizationId);
        Assert.Equal(tester.Id, recorded.SubjectId);
        Assert.Equal(f.CallerId, recorded.ActorAccountId);
        Assert.Equal(new AuditRoleRef(tester.Id, "Tester", null), recorded.Parameters["role"]);

        Received.InOrder(() =>
        {
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.Roles.Delete(tester);
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await f.AssertInvalidatedAfterCommitAsync();
    }
}
