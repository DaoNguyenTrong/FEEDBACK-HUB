using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Application.Services.Organizations;

public sealed class MemberService(
    ICurrentUserService currentUserService,
    ICurrentTenantProvider currentTenantProvider,
    IOrganizationMemberQueries memberQueries,
    IRoleGrantReader roleGrantReader) : IMemberService
{
    public async Task<PagedResult<OrganizationMemberDto>> ListAsync(
        PageRequest request, CancellationToken cancellationToken)
    {
        Guid organizationId = currentTenantProvider.OrganizationId
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
        Guid callerId = GetCurrentAccountId();

        PagedResult<MemberRow> page = await memberQueries.ListActiveAsync(organizationId, request, cancellationToken);

        // Read from the database, never the JWT or the permission cache: it decides who sees other emails.
        RoleGrantCaller caller = await roleGrantReader.LoadCallerAsync(organizationId, callerId, cancellationToken);
        IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>> rolesByMember =
            await roleGrantReader.LoadRolesOfMembersAsync(
                organizationId, page.Items.Select(row => row.MemberId).ToList(), cancellationToken);

        bool canManage = caller.Permissions.Contains(Permissions.OrganizationMembersManage);

        List<OrganizationMemberDto> items = page.Items
            .Select(row => ToDto(
                row,
                rolesByMember.TryGetValue(row.MemberId, out IReadOnlyList<RoleGrantRole>? roles) ? roles : [],
                caller,
                canManage))
            .ToList();

        return new PagedResult<OrganizationMemberDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    private static OrganizationMemberDto ToDto(
        MemberRow row, IReadOnlyList<RoleGrantRole> roles, RoleGrantCaller caller, bool canManage)
    {
        bool isSelf = row.AccountId == caller.AccountId;
        List<RoleRefDto> refs = roles.Select(ToRef).ToList();
        string? email = canManage || isSelf ? row.Email : null;

        bool canChangeRoles = false;
        bool canRemove = false;
        RoleGrantDenial? denial = null;

        if (canManage && isSelf)
        {
            canChangeRoles = true;
        }
        else if (canManage)
        {
            denial = RoleGrantPolicy.CheckTakeAwayAll(caller, roles);
            canChangeRoles = denial is null;
            canRemove = denial is null;
        }

        return new OrganizationMemberDto(
            row.AccountId,
            row.AccountName,
            email,
            refs.Select(role => role.Id).ToList(),
            refs.Select(role => role.Name).ToList(),
            refs,
            isSelf,
            canChangeRoles,
            canRemove,
            denial?.Reason,
            denial?.Role is { } lockRole ? ToRef(lockRole) : null);
    }

    private static RoleRefDto ToRef(RoleGrantRole role) => new(role.Id, role.Name, role.SystemKind);

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
