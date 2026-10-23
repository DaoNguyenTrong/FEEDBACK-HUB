using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Organizations;

public sealed class OrganizationService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IRoleService roleService) : IOrganizationService
{
    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        // BR-23: an account flagged for re-verification can still sign in, so being signed in proves nothing.
        Account account = await unitOfWork.Repository<Account, Guid>().GetByIdAsync(accountId, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        if (!account.EmailConfirmed)
        {
            throw new ForbiddenException(ApplicationMessages.EmailNotConfirmed);
        }

        Organization organization = Organization.Create(new OrganizationParams(request.Name, request.Slug));
        IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();

        // Never names the organization holding the slug, whatever its status; the unique index backs this check.
        Organization? slugHolder =
            await organizationRepository.FirstOrDefaultAsync(o => o.Slug == organization.Slug, cancellationToken);
        if (slugHolder is not null)
        {
            throw new ConflictException(ApplicationMessages.OrganizationSlugAlreadyExists);
        }

        await organizationRepository.AddAsync(organization, cancellationToken);

        IReadOnlyDictionary<SystemRoleKind, Role> systemRoles =
            await roleService.SeedSystemRolesAsync(organization.Id, cancellationToken);
        Role ownerRole = systemRoles[SystemRoleKind.Owner];

        OrganizationMember owner = OrganizationMember.Create(new OrganizationMemberParams(organization.Id, accountId));
        await unitOfWork.Repository<OrganizationMember, Guid>().AddAsync(owner, cancellationToken);

        await unitOfWork.Repository<OrganizationMemberRole, Guid>().AddAsync(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(owner.Id, ownerRole.Id)), cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrganizationDto(
            organization.Id, organization.Name, organization.Slug, organization.Status,
            [ownerRole.Name], [RoleRefDto.From(ownerRole)], Permissions.All);
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListMineAsync(CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        IReadOnlyList<OrganizationMember> memberships = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(m => m.AccountId == accountId && m.IsActive, cancellationToken);

        if (memberships.Count == 0)
        {
            return [];
        }

        Dictionary<Guid, List<Role>> rolesByMembershipId = await LoadRolesByMembershipIdAsync(
            memberships.Select(m => m.Id).ToList(), cancellationToken);

        ILookup<Guid, string> permissionCodesByRoleId = await LoadPermissionCodesByRoleIdAsync(
            rolesByMembershipId.Values.SelectMany(roles => roles), cancellationToken);

        List<Guid> organizationIds = memberships.Select(m => m.OrganizationId).Distinct().ToList();
        Dictionary<Guid, Organization> organizationById = (await unitOfWork.Repository<Organization, Guid>()
                .ListAsync(o => organizationIds.Contains(o.Id), cancellationToken))
            .ToDictionary(o => o.Id);

        List<OrganizationDto> result = [];

        foreach (OrganizationMember membership in memberships)
        {
            if (!organizationById.TryGetValue(membership.OrganizationId, out Organization? organization))
            {
                continue;
            }

            List<Role> roles = rolesByMembershipId.GetValueOrDefault(membership.Id, []);
            bool isOwner = roles.Any(role => role.SystemRoleKind == SystemRoleKind.Owner);
            HashSet<string> permissionCodes = isOwner
                ? [.. Permissions.All]
                : roles.SelectMany(role => permissionCodesByRoleId[role.Id]).ToHashSet();

            result.Add(new OrganizationDto(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.Status,
                roles.Select(role => role.Name).ToList(),
                roles.Select(RoleRefDto.From).ToList(),
                permissionCodes.ToList()));
        }

        return result;
    }

    private async Task<Dictionary<Guid, List<Role>>> LoadRolesByMembershipIdAsync(
        IReadOnlyList<Guid> membershipIds, CancellationToken cancellationToken)
    {
        Dictionary<Guid, List<Role>> result = membershipIds.ToDictionary(id => id, _ => new List<Role>());

        if (membershipIds.Count == 0)
        {
            return result;
        }

        IReadOnlyList<OrganizationMemberRole> memberRoles = await unitOfWork.Repository<OrganizationMemberRole, Guid>()
            .ListAsync(mr => membershipIds.Contains(mr.OrganizationMemberId), cancellationToken);

        List<Guid> roleIds = memberRoles.Select(mr => mr.RoleId).Distinct().ToList();

        IReadOnlyList<Role> roles = roleIds.Count == 0
            ? []
            : await unitOfWork.Repository<Role, Guid>().ListAsync(role => roleIds.Contains(role.Id), cancellationToken);

        Dictionary<Guid, Role> roleById = roles.ToDictionary(role => role.Id);

        foreach (OrganizationMemberRole memberRole in memberRoles)
        {
            if (roleById.TryGetValue(memberRole.RoleId, out Role? role))
            {
                result[memberRole.OrganizationMemberId].Add(role);
            }
        }

        return result;
    }

    private async Task<ILookup<Guid, string>> LoadPermissionCodesByRoleIdAsync(
        IEnumerable<Role> roles, CancellationToken cancellationToken)
    {
        List<Guid> nonOwnerRoleIds = roles
            .Where(role => role.SystemRoleKind != SystemRoleKind.Owner)
            .Select(role => role.Id)
            .Distinct()
            .ToList();

        if (nonOwnerRoleIds.Count == 0)
        {
            return Array.Empty<RolePermission>().ToLookup(rp => rp.RoleId, rp => rp.PermissionCode);
        }

        IReadOnlyList<RolePermission> rolePermissions = await unitOfWork.Repository<RolePermission, Guid>()
            .ListAsync(rp => nonOwnerRoleIds.Contains(rp.RoleId), cancellationToken);

        return rolePermissions.ToLookup(rp => rp.RoleId, rp => rp.PermissionCode);
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
