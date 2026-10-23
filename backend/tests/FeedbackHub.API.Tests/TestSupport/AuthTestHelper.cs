using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Services.ApiKeys;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Services.Auth;

namespace FeedbackHub.API.Tests.TestSupport;

public static class AuthTestHelper
{
    public const string GateOrganizationName = "Gate Co";

    // Set by ApiFactoryFixture after the host boots, from the host's own IOptions<JwtSettings>, so minted tokens
    // match whatever SecretKey is configured — a hardcoded literal would drift from local appsettings.json into 401s.
    private static JwtSettings? configuredJwtSettings;

    private static JwtSettings JwtSettings => configuredJwtSettings
        ?? throw new InvalidOperationException(
            $"{nameof(AuthTestHelper)} is not configured. Every test class that mints tokens must be in "
            + $"[Collection(nameof(ApiCollection))] so {nameof(ApiFactoryFixture)} can call "
            + $"{nameof(ConfigureJwtSettings)} with the host's resolved IOptions<JwtSettings>.");

    public static void ConfigureJwtSettings(JwtSettings jwtSettings) => configuredJwtSettings = jwtSettings;

    public static async Task<Account> SeedConfirmedAccountAsync(
        AppDbContext context,
        string username = "test-user",
        string email = "test-user@example.com",
        string? passwordHash = "seeded-hash")
    {
        Account account = Account.Create(new AccountParams("Test User", username, email));
        if (passwordHash is not null)
        {
            account.SetPasswordHash(passwordHash);
        }
        account.ConfirmEmail();

        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        return account;
    }

    // Names no session, like a token minted before sessions were named: the refresh cookie is the only session marker.
    public static string MintAccessToken(Account account, Guid? organizationId = null) =>
        MintAccessToken(account, JwtSettings.SecretKey, DateTime.UtcNow.AddMinutes(15), organizationId);

    // An access token as sign-in issues it, naming the session (refresh-token family) it belongs to.
    public static string MintAccessTokenForSession(Account account, Guid sessionId)
    {
        JwtTokenService jwtTokenService = new(Options.Create(JwtSettings));
        return jwtTokenService.GenerateAccessToken(account, null, sessionId);
    }

    public static async Task<Organization> SeedOrganizationAsync(
        AppDbContext context,
        string name = "Acme Inc",
        string? slug = null)
    {
        Organization organization = Organization.Create(new OrganizationParams(name, slug ?? $"acme-{Guid.NewGuid():N}"));

        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        return organization;
    }

    /// <summary>A signed-in caller in a fresh <see cref="GateOrganizationName"/> organization, for gate tests.</summary>
    /// <param name="personalContext">Mint the token without <c>org_id</c>.</param>
    public static async Task<HttpClient> CreateOrganizationCallerClientAsync(
        ApiFactoryFixture fixture,
        SystemRoleKind role,
        bool member = true,
        bool organizationSuspended = false,
        bool personalContext = false)
    {
        using IServiceScope scope = fixture.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account caller = await SeedConfirmedAccountAsync(
            context, username: $"gate-{Guid.NewGuid():N}", email: $"gate-{Guid.NewGuid():N}@example.com");
        Organization organization = await SeedOrganizationAsync(context, name: GateOrganizationName);
        if (member)
        {
            await SeedOrganizationMemberAsync(context, organization.Id, caller.Id, role);
        }

        if (organizationSuspended)
        {
            organization.Deactivate(new DeactivationParams(caller.Id, false, null, DateTime.UtcNow));
            context.Organizations.Update(organization);
            await context.SaveChangesAsync();
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", MintAccessToken(caller, personalContext ? null : organization.Id));
        return client;
    }

    public static async Task<OrganizationMember> SeedOrganizationMemberAsync(
        AppDbContext context,
        Guid organizationId,
        Guid accountId,
        SystemRoleKind role = SystemRoleKind.Owner)
    {
        IReadOnlyDictionary<SystemRoleKind, Role> systemRoles = await SeedSystemRolesAsync(context, organizationId);

        OrganizationMember member = OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId));
        context.OrganizationMembers.Add(member);
        context.OrganizationMemberRoles.Add(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, systemRoles[role].Id)));

        await context.SaveChangesAsync();

        return member;
    }

    // Idempotent: seeds the 3 system roles for an org (mirrors RoleService.SeedSystemRolesAsync,
    // hand-rolled against the raw AppDbContext since pulling in the full DI-wired service isn't
    // worth it in a test helper) or returns the existing ones if already seeded.
    public static async Task<IReadOnlyDictionary<SystemRoleKind, Role>> SeedSystemRolesAsync(
        AppDbContext context, Guid organizationId)
    {
        List<Role> existing = await context.Roles
            .Where(role => role.OrganizationId == organizationId && role.SystemRoleKind != null)
            .ToListAsync();

        if (existing.Count == 3)
        {
            return existing.ToDictionary(role => role.SystemRoleKind!.Value);
        }

        Dictionary<SystemRoleKind, Role> roles = [];

        foreach (SystemRoleKind kind in Enum.GetValues<SystemRoleKind>())
        {
            Role role = Role.Create(new RoleParams(organizationId, kind.ToString(), kind));
            context.Roles.Add(role);
            roles[kind] = role;

            if (kind == SystemRoleKind.Admin)
            {
                context.RolePermissions.Add(
                    RolePermission.Create(new RolePermissionParams(role.Id, Permissions.OrganizationMembersManage)));
            }
        }

        await context.SaveChangesAsync();

        return roles;
    }

    public static async Task<Role> SeedCustomRoleAsync(
        AppDbContext context, Guid organizationId, string name, IReadOnlyList<string> permissionCodes)
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

    public static async Task AssignRoleAsync(AppDbContext context, Guid organizationMemberId, Guid roleId)
    {
        context.OrganizationMemberRoles.Add(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(organizationMemberId, roleId)));

        await context.SaveChangesAsync();
    }

    // Signed with the real JwtSettings.SecretKey but already-expired — exercises the JwtBearer
    // pipeline's lifetime validation, distinct from a token that's simply missing.
    public static string MintExpiredAccessToken(Account account) =>
        MintAccessToken(account, JwtSettings.SecretKey, DateTime.UtcNow.AddMinutes(-1));

    // Correct claims/expiry but signed with a different key — exercises signature validation.
    public static string MintTamperedAccessToken(Account account) =>
        MintAccessToken(account, "tampered-secret-key-also-32-chars-minimum", DateTime.UtcNow.AddMinutes(15));

    private static string MintAccessToken(Account account, string secretKey, DateTime expires, Guid? organizationId = null)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(secretKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Email, account.Email),
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];
        if (organizationId is { } orgId)
        {
            claims.Add(new Claim(IJwtTokenService.OrganizationIdClaimType, orgId.ToString()));
        }

        JwtSecurityToken token = new(
            issuer: JwtSettings.Issuer,
            audience: JwtSettings.Audiences[0],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static async Task<(ApiKey ApiKey, string RawKey)> SeedActiveApiKeyAsync(
        AppDbContext context, Guid organizationId, string name = "Test Key")
    {
        FeedbackHub.Application.Common.Interfaces.ICurrentTenantProvider currentTenantProvider =
            Substitute.For<FeedbackHub.Application.Common.Interfaces.ICurrentTenantProvider>();
        currentTenantProvider.OrganizationId.Returns(organizationId);

        ApiKeyService apiKeyServiceHelper = new(new PassthroughUnitOfWork(context), currentTenantProvider);
        CreateApiKeyResult result =
            await apiKeyServiceHelper.CreateAsync(new CreateApiKeyRequest(name), CancellationToken.None);

        ApiKey apiKey = await context.ApiKeys.FirstAsync(k => k.Id == result.Key.Id);
        return (apiKey, result.RawKey);
    }

    private sealed class PassthroughUnitOfWork(AppDbContext context) : FeedbackHub.Application.Common.Interfaces.IUnitOfWork
    {
        public FeedbackHub.Domain.Interfaces.IRepository<T, TId> Repository<T, TId>()
            where T : BaseEntity<TId> where TId : notnull =>
            new FeedbackHub.Infrastructure.Persistence.Repositories.GenericRepository<T, TId>(context);

        public FeedbackHub.Domain.Interfaces.IRepository<T> Repository<T>() where T : BaseEntity =>
            new FeedbackHub.Infrastructure.Persistence.Repositories.GenericRepository<T>(context);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            context.SaveChangesAsync(cancellationToken);

        // Only seeds an API key, which saves once — no transaction to manage.
        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }
}
