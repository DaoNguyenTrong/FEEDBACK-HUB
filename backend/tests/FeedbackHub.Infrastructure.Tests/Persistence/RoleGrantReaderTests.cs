using System.Reflection;
using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class RoleGrantReaderTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private RoleGrantReader reader = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        reader = new RoleGrantReader(context);
    }

    // Never committed: disposing the transaction rolls every seeded row back.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private sealed record SeededOrganization(Organization Organization, IReadOnlyDictionary<SystemRoleKind, Role> System);

    private async Task<SeededOrganization> SeedOrganizationAsync()
    {
        Organization organization = Organization.Create(new OrganizationParams("Acme", $"acme-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);

        Dictionary<SystemRoleKind, Role> roles = [];
        foreach (SystemRoleKind kind in Enum.GetValues<SystemRoleKind>())
        {
            Role role = Role.Create(new RoleParams(organization.Id, kind.ToString(), kind));
            context.Roles.Add(role);
            roles[kind] = role;
        }

        context.RolePermissions.Add(RolePermission.Create(
            new RolePermissionParams(roles[SystemRoleKind.Admin].Id, Permissions.OrganizationMembersManage)));
        context.RolePermissions.Add(RolePermission.Create(
            new RolePermissionParams(roles[SystemRoleKind.Admin].Id, Permissions.FeedbackHubAdmin)));

        await context.SaveChangesAsync();
        return new SeededOrganization(organization, roles);
    }

    private async Task<Role> SeedCustomRoleAsync(Guid organizationId, string name, params string[] permissionCodes)
    {
        Role role = Role.Create(new RoleParams(organizationId, name));
        context.Roles.Add(role);
        foreach (string code in permissionCodes)
        {
            context.RolePermissions.Add(RolePermission.Create(new RolePermissionParams(role.Id, code)));
        }

        await context.SaveChangesAsync();
        return role;
    }

    private async Task<OrganizationMember> SeedMemberAsync(
        Guid organizationId, IEnumerable<Role> roles, bool active = true, Guid? accountId = null)
    {
        if (accountId is null)
        {
            string handle = $"member-{Guid.NewGuid():N}";
            Account account = Account.Create(new AccountParams("Member", handle, $"{handle}@example.com"));
            context.Accounts.Add(account);
            accountId = account.Id;
        }

        OrganizationMember member = OrganizationMember.Create(
            new OrganizationMemberParams(organizationId, accountId.Value));
        if (!active)
        {
            member.Deactivate();
        }

        context.OrganizationMembers.Add(member);
        foreach (Role role in roles)
        {
            context.OrganizationMemberRoles.Add(
                OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, role.Id)));
        }

        await context.SaveChangesAsync();
        return member;
    }

    // LoadCallerAsync

    [Fact]
    public async Task LoadCallerAsync_Owner_IsOwnerWithAllPermissions()
    {
        SeededOrganization seeded = await SeedOrganizationAsync();
        OrganizationMember owner = await SeedMemberAsync(
            seeded.Organization.Id, [seeded.System[SystemRoleKind.Owner]]);

        RoleGrantCaller caller = await reader.LoadCallerAsync(
            seeded.Organization.Id, owner.AccountId, CancellationToken.None);

        Assert.Equal(owner.AccountId, caller.AccountId);
        Assert.True(caller.IsOwner);
        Assert.Equal(Permissions.All.ToHashSet(), caller.Permissions.ToHashSet());
    }

    [Fact]
    public async Task LoadCallerAsync_InactiveOwner_NotOwnerWithoutPermissions()
    {
        SeededOrganization seeded = await SeedOrganizationAsync();
        OrganizationMember removed = await SeedMemberAsync(
            seeded.Organization.Id, [seeded.System[SystemRoleKind.Owner]], active: false);

        RoleGrantCaller caller = await reader.LoadCallerAsync(
            seeded.Organization.Id, removed.AccountId, CancellationToken.None);

        Assert.False(caller.IsOwner);
        Assert.Empty(caller.Permissions);
    }

    [Fact]
    public async Task LoadCallerAsync_OwnerOfAnotherOrganization_NotOwnerWithoutPermissions()
    {
        SeededOrganization mine = await SeedOrganizationAsync();
        SeededOrganization other = await SeedOrganizationAsync();
        OrganizationMember ownerElsewhere = await SeedMemberAsync(
            other.Organization.Id, [other.System[SystemRoleKind.Owner]]);

        RoleGrantCaller caller = await reader.LoadCallerAsync(
            mine.Organization.Id, ownerElsewhere.AccountId, CancellationToken.None);

        Assert.False(caller.IsOwner);
        Assert.Empty(caller.Permissions);
    }

    [Fact]
    public async Task LoadCallerAsync_SeveralRoles_UnionOfRolePermissions()
    {
        SeededOrganization seeded = await SeedOrganizationAsync();
        Role auditor = await SeedCustomRoleAsync(
            seeded.Organization.Id, "Kiểm toán", Permissions.AuditLogsView, Permissions.OrganizationMembersManage);
        OrganizationMember member = await SeedMemberAsync(
            seeded.Organization.Id, [seeded.System[SystemRoleKind.Admin], auditor]);

        RoleGrantCaller caller = await reader.LoadCallerAsync(
            seeded.Organization.Id, member.AccountId, CancellationToken.None);

        Assert.False(caller.IsOwner);
        Assert.Equal(
            new HashSet<string>
            {
                Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin, Permissions.AuditLogsView
            },
            caller.Permissions.ToHashSet());
    }

    // LoadRolesAsync

    [Fact]
    public async Task LoadRolesAsync_RoleOfAnotherOrganization_IsIgnored()
    {
        SeededOrganization mine = await SeedOrganizationAsync();
        SeededOrganization other = await SeedOrganizationAsync();
        Role foreign = await SeedCustomRoleAsync(other.Organization.Id, "Ngoài", Permissions.FilesManage);
        Role member = mine.System[SystemRoleKind.Member];

        IReadOnlyList<RoleGrantRole> roles = await reader.LoadRolesAsync(
            mine.Organization.Id, [foreign.Id, member.Id, other.System[SystemRoleKind.Owner].Id],
            CancellationToken.None);

        RoleGrantRole loaded = Assert.Single(roles);
        Assert.Equal(member.Id, loaded.Id);
    }

    [Fact]
    public async Task LoadRolesAsync_SystemAndCustomRoles_CarryKindAndPermissions()
    {
        SeededOrganization seeded = await SeedOrganizationAsync();
        Role auditor = await SeedCustomRoleAsync(seeded.Organization.Id, "Kiểm toán", Permissions.AuditLogsView);

        IReadOnlyList<RoleGrantRole> roles = await reader.LoadRolesAsync(
            seeded.Organization.Id,
            [auditor.Id, seeded.System[SystemRoleKind.Owner].Id, seeded.System[SystemRoleKind.Admin].Id],
            CancellationToken.None);

        Assert.Equal(3, roles.Count);
        Assert.Equal(SystemRoleKind.Owner, roles[0].SystemKind);
        Assert.Equal(Permissions.All.ToHashSet(), roles[0].Permissions.ToHashSet());
        Assert.Equal(SystemRoleKind.Admin, roles[1].SystemKind);
        Assert.Equal(
            new HashSet<string> { Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin },
            roles[1].Permissions.ToHashSet());
        Assert.Null(roles[2].SystemKind);
        Assert.Equal("Kiểm toán", roles[2].Name);
        Assert.Equal(new HashSet<string> { Permissions.AuditLogsView }, roles[2].Permissions.ToHashSet());
    }

    // ICU root order, not code points: "an Bình" < "Âu Cơ" < "Bảo Minh" (AC-302 order, reused for roles).
    [Fact]
    public async Task LoadRolesAsync_MixedRoles_SystemKindsThenCustomByIcuName()
    {
        SeededOrganization seeded = await SeedOrganizationAsync();
        Role bao = await SeedCustomRoleAsync(seeded.Organization.Id, "Bảo Minh");
        Role an = await SeedCustomRoleAsync(seeded.Organization.Id, "an Bình");
        Role au = await SeedCustomRoleAsync(seeded.Organization.Id, "Âu Cơ");

        IReadOnlyList<RoleGrantRole> roles = await reader.LoadRolesAsync(
            seeded.Organization.Id,
            [bao.Id, seeded.System[SystemRoleKind.Member].Id, au.Id, seeded.System[SystemRoleKind.Admin].Id,
                an.Id, seeded.System[SystemRoleKind.Owner].Id],
            CancellationToken.None);

        Assert.Equal(["Owner", "Admin", "Member", "an Bình", "Âu Cơ", "Bảo Minh"], roles.Select(role => role.Name));
    }

    [Fact]
    public async Task LoadRolesAsync_NoIds_ReturnsEmpty()
    {
        SeededOrganization seeded = await SeedOrganizationAsync();

        IReadOnlyList<RoleGrantRole> roles = await reader.LoadRolesAsync(
            seeded.Organization.Id, [], CancellationToken.None);

        Assert.Empty(roles);
    }

    // LoadRolesOfMembersAsync

    [Fact]
    public async Task LoadRolesOfMembersAsync_SeveralMembers_RolesPerMemberRowInDisplayOrder()
    {
        SeededOrganization seeded = await SeedOrganizationAsync();
        Role bao = await SeedCustomRoleAsync(seeded.Organization.Id, "Bảo Minh", Permissions.FilesManage);
        Role an = await SeedCustomRoleAsync(seeded.Organization.Id, "an Bình");
        OrganizationMember first = await SeedMemberAsync(
            seeded.Organization.Id, [bao, seeded.System[SystemRoleKind.Member], an, seeded.System[SystemRoleKind.Owner]]);
        OrganizationMember second = await SeedMemberAsync(
            seeded.Organization.Id, [seeded.System[SystemRoleKind.Admin]]);

        IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>> rolesByMember =
            await reader.LoadRolesOfMembersAsync([first.Id, second.Id], CancellationToken.None);

        Assert.Equal(2, rolesByMember.Count);
        Assert.Equal(
            ["Owner", "Member", "an Bình", "Bảo Minh"], rolesByMember[first.Id].Select(role => role.Name));
        Assert.Equal(Permissions.All.ToHashSet(), rolesByMember[first.Id][0].Permissions.ToHashSet());
        Assert.Equal(new HashSet<string> { Permissions.FilesManage }, rolesByMember[first.Id][3].Permissions.ToHashSet());
        Assert.Equal(["Admin"], rolesByMember[second.Id].Select(role => role.Name));
    }

    [Fact]
    public async Task LoadRolesOfMembersAsync_MemberWithoutRoles_ReturnsEmptyListForThatMember()
    {
        SeededOrganization seeded = await SeedOrganizationAsync();
        OrganizationMember member = await SeedMemberAsync(seeded.Organization.Id, []);

        IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>> rolesByMember =
            await reader.LoadRolesOfMembersAsync([member.Id], CancellationToken.None);

        Assert.Empty(Assert.Contains(member.Id, rolesByMember));
    }

    [Fact]
    public async Task LoadRolesOfMembersAsync_NoIds_ReturnsEmpty()
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>> rolesByMember =
            await reader.LoadRolesOfMembersAsync([], CancellationToken.None);

        Assert.Empty(rolesByMember);
    }

    // Constructor

    // TECH00 §5.3: BR-58 inputs come from the database inside the locked transaction, never from the cache.
    [Fact]
    public void Constructor_Parameters_TakeOnlyAppDbContextAndNoPermissionResolver()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(RoleGrantReader).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        Type[] parameterTypes = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal([typeof(AppDbContext)], parameterTypes);
        Assert.DoesNotContain(typeof(IPermissionResolver), parameterTypes);
    }
}
