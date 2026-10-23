using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;
using FeedbackHub.Infrastructure.Services.Auth;

namespace FeedbackHub.Infrastructure.Tests.Services.Auth;

public class OrganizationContextAuthorizationHandlerTests
{
    private const string PermissionCode = Permissions.ApiKeysManage;
    private const string OrganizationName = "Suspended Co";

    private sealed record Fixture(
        OrganizationContextAuthorizationHandler Handler,
        ITenantAccessService TenantAccessService,
        IPermissionResolver PermissionResolver,
        IUnitOfWork UnitOfWork,
        IRepository<Organization, Guid> OrganizationRepository,
        Guid OrganizationId,
        Guid AccountId);

    private static Fixture CreateFixture(
        bool hasOrganizationClaim = true,
        bool hasAccountId = true,
        TenantAccessStatus status = TenantAccessStatus.Active,
        IReadOnlySet<string>? permissions = null)
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        HttpContextAccessor accessor = new() { HttpContext = new DefaultHttpContext() };

        ICurrentTenantProvider currentTenantProvider = Substitute.For<ICurrentTenantProvider>();
        currentTenantProvider.OrganizationId.Returns(hasOrganizationClaim ? organizationId : null);

        // An API key caller carries no account id (no NameIdentifier claim).
        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(hasAccountId ? accountId.ToString() : null);

        ITenantAccessService tenantAccessService = Substitute.For<ITenantAccessService>();
        tenantAccessService.GetAccessAsync(accountId, organizationId, Arg.Any<CancellationToken>()).Returns(status);

        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();
        permissionResolver.GetEffectivePermissionsAsync(organizationId, accountId, Arg.Any<CancellationToken>())
            .Returns(permissions ?? new HashSet<string>());

        Organization organization = Organization.Create(new OrganizationParams(OrganizationName, "suspended-co"));
        IRepository<Organization, Guid> organizationRepository = Substitute.For<IRepository<Organization, Guid>>();
        organizationRepository.GetByIdAsync(organizationId, Arg.Any<CancellationToken>()).Returns(organization);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepository);

        OrganizationContextAuthorizationHandler handler = new(
            accessor, currentTenantProvider, currentUserService, tenantAccessService, permissionResolver, unitOfWork);

        return new Fixture(
            handler, tenantAccessService, permissionResolver, unitOfWork, organizationRepository, organizationId, accountId);
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(
        Fixture f, string? permissionCode, DenyMode denyMode, bool authenticated = true)
    {
        OrganizationContextRequirement requirement = new(permissionCode, denyMode);
        ClaimsPrincipal user = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "caller")], "Test"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        AuthorizationHandlerContext context = new([requirement], user, resource: null);

        await f.Handler.HandleAsync(context);

        return context;
    }

    private static CodedAuthorizationFailureReason SingleCodedReason(AuthorizationHandlerContext context)
    {
        Assert.True(context.HasFailed);
        AuthorizationFailureReason reason = Assert.Single(context.FailureReasons);
        return Assert.IsType<CodedAuthorizationFailureReason>(reason);
    }

    // HandleAsync

    [Fact]
    public async Task HandleAsync_NotSignedIn_LeavesRequirementUnhandledForChallenge()
    {
        Fixture f = CreateFixture();

        AuthorizationHandlerContext context = await AuthorizeAsync(f, PermissionCode, DenyMode.Forbidden, authenticated: false);

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
        await f.TenantAccessService.DidNotReceive().GetAccessAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // AC-298: the personal context (no org_id) and an API key (no account id) never reach an organization.
    [Theory]
    [InlineData(false, true, DenyMode.Forbidden, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationAccessDenied)]
    [InlineData(true, false, DenyMode.Forbidden, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationAccessDenied)]
    [InlineData(false, true, DenyMode.NotFound, StatusCodes.Status404NotFound, ApplicationMessages.ResourceNotFound)]
    [InlineData(true, false, DenyMode.NotFound, StatusCodes.Status404NotFound, ApplicationMessages.ResourceNotFound)]
    public async Task HandleAsync_PersonalContextOrApiKey_FailsByDenyMode(
        bool hasOrganizationClaim, bool hasAccountId, DenyMode denyMode, int expectedStatus, string expectedCode)
    {
        Fixture f = CreateFixture(hasOrganizationClaim, hasAccountId);

        AuthorizationHandlerContext context = await AuthorizeAsync(f, null, denyMode);

        CodedAuthorizationFailureReason reason = SingleCodedReason(context);
        Assert.Equal(expectedStatus, reason.StatusCode);
        Assert.Equal(expectedCode, reason.Code);
        Assert.Null(reason.OrganizationName);
        await f.TenantAccessService.DidNotReceive().GetAccessAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // AC-320: a non-member, even of a suspended organization, learns neither its name nor its state.
    [Theory]
    [InlineData(DenyMode.Forbidden, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationAccessDenied)]
    [InlineData(DenyMode.NotFound, StatusCodes.Status404NotFound, ApplicationMessages.ResourceNotFound)]
    public async Task HandleAsync_NotMember_FailsByDenyMode(DenyMode denyMode, int expectedStatus, string expectedCode)
    {
        Fixture f = CreateFixture(status: TenantAccessStatus.NotMember);

        AuthorizationHandlerContext context = await AuthorizeAsync(f, PermissionCode, denyMode);

        CodedAuthorizationFailureReason reason = SingleCodedReason(context);
        Assert.Equal(expectedStatus, reason.StatusCode);
        Assert.Equal(expectedCode, reason.Code);
        Assert.Null(reason.OrganizationName);
        f.UnitOfWork.DidNotReceive().Repository<Organization, Guid>();
        await f.PermissionResolver.DidNotReceive().GetEffectivePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // AC-301: a member of a suspended organization is told which one, whichever the deny mode.
    [Theory]
    [InlineData(DenyMode.Forbidden)]
    [InlineData(DenyMode.NotFound)]
    public async Task HandleAsync_SuspendedMember_FailsSuspendedWithNameInBothModes(DenyMode denyMode)
    {
        Fixture f = CreateFixture(status: TenantAccessStatus.Suspended);

        AuthorizationHandlerContext context = await AuthorizeAsync(f, null, denyMode);

        CodedAuthorizationFailureReason reason = SingleCodedReason(context);
        Assert.Equal(StatusCodes.Status403Forbidden, reason.StatusCode);
        Assert.Equal(ApplicationMessages.OrganizationSuspended, reason.Code);
        Assert.Equal(OrganizationName, reason.OrganizationName);
        await f.OrganizationRepository.Received(1).GetByIdAsync(f.OrganizationId, Arg.Any<CancellationToken>());
    }

    // AC-321: step 3 (suspended) comes before step 4 (missing permission).
    [Fact]
    public async Task HandleAsync_SuspendedMemberMissingPermission_FailsSuspendedNotAccessDenied()
    {
        Fixture f = CreateFixture(status: TenantAccessStatus.Suspended);

        AuthorizationHandlerContext context = await AuthorizeAsync(f, PermissionCode, DenyMode.Forbidden);

        CodedAuthorizationFailureReason reason = SingleCodedReason(context);
        Assert.Equal(ApplicationMessages.OrganizationSuspended, reason.Code);
        Assert.Equal(OrganizationName, reason.OrganizationName);
        await f.PermissionResolver.DidNotReceive().GetEffectivePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DenyMode.Forbidden, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationAccessDenied)]
    [InlineData(DenyMode.NotFound, StatusCodes.Status404NotFound, ApplicationMessages.ResourceNotFound)]
    public async Task HandleAsync_SuspendedOrganizationGone_FailsByDenyModeWithoutName(
        DenyMode denyMode, int expectedStatus, string expectedCode)
    {
        Fixture f = CreateFixture(status: TenantAccessStatus.Suspended);
        f.OrganizationRepository.GetByIdAsync(f.OrganizationId, Arg.Any<CancellationToken>()).Returns((Organization?)null);

        AuthorizationHandlerContext context = await AuthorizeAsync(f, null, denyMode);

        CodedAuthorizationFailureReason reason = SingleCodedReason(context);
        Assert.Equal(expectedStatus, reason.StatusCode);
        Assert.Equal(expectedCode, reason.Code);
        Assert.Null(reason.OrganizationName);
    }

    // AC-316: a missing permission is refused before any data is read.
    [Fact]
    public async Task HandleAsync_ActiveMemberMissingPermission_FailsAccessDeniedWithoutDataQuery()
    {
        Fixture f = CreateFixture(permissions: new HashSet<string> { Permissions.FilesManage });

        AuthorizationHandlerContext context = await AuthorizeAsync(f, PermissionCode, DenyMode.Forbidden);

        CodedAuthorizationFailureReason reason = SingleCodedReason(context);
        Assert.Equal(StatusCodes.Status403Forbidden, reason.StatusCode);
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, reason.Code);
        Assert.Null(reason.OrganizationName);
        f.UnitOfWork.DidNotReceive().Repository<Organization, Guid>();
    }

    [Fact]
    public async Task HandleAsync_ActiveMemberWithPermission_Succeeds()
    {
        Fixture f = CreateFixture(permissions: new HashSet<string> { PermissionCode });

        AuthorizationHandlerContext context = await AuthorizeAsync(f, PermissionCode, DenyMode.Forbidden);

        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
        await f.PermissionResolver.Received(1).GetEffectivePermissionsAsync(
            f.OrganizationId, f.AccountId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DenyMode.Forbidden)]
    [InlineData(DenyMode.NotFound)]
    public async Task HandleAsync_NoPermissionCode_ActiveMember_Succeeds(DenyMode denyMode)
    {
        Fixture f = CreateFixture();

        AuthorizationHandlerContext context = await AuthorizeAsync(f, null, denyMode);

        Assert.True(context.HasSucceeded);
        await f.TenantAccessService.Received(1).GetAccessAsync(
            f.AccountId, f.OrganizationId, Arg.Any<CancellationToken>());
        await f.PermissionResolver.DidNotReceive().GetEffectivePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
