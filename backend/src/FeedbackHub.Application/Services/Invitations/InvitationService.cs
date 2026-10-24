using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Invitations;

public sealed class InvitationService(
    ICurrentUserService currentUserService,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork,
    IOrganizationLock organizationLock,
    IRoleGrantReader roleGrantReader,
    IInvitationQueries invitationQueries,
    IAuditTrail auditTrail,
    IEmailSender emailSender,
    IDateTimeProvider dateTimeProvider,
    IOptions<EmailSettings> emailOptions,
    ILogger<InvitationService> logger) : IInvitationService
{
    private readonly EmailSettings emailSettings = emailOptions.Value;

    public async Task<InvitationDto> SendAsync(SendInvitationRequest request, CancellationToken cancellationToken)
    {
        string email = Account.NormalizeEmail(request.Email);
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();
        InvitationDto? sent = null;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            RoleGrantCaller caller = await LockAndLoadManagerAsync(organizationId, callerId, ct);
            IReadOnlyList<RoleGrantRole> roles = await roleGrantReader.LoadRolesAsync(organizationId, [request.RoleId], ct);
            RoleGrantRole role = roles.Count == 1 ? roles[0] : throw new NotFoundException(nameof(Role), request.RoleId);
            OrganizationInvitation? previous = await unitOfWork.Repository<OrganizationInvitation, Guid>()
                .FirstOrDefaultAsync(
                    invitation => invitation.OrganizationId == organizationId && invitation.Email == email, ct);

            sent = await ReplaceAndSendAsync(organizationId, caller, email, role, previous, ct);
        }, cancellationToken);

        return sent ?? throw new InvalidOperationException("The invitation transaction completed without a result.");
    }

    public async Task<PagedResult<InvitationDto>> ListAsync(PageRequest request, CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();

        // The database, not the permission cache, decides who sees invited addresses (BR-75); flags need no lock.
        RoleGrantCaller caller = await roleGrantReader.LoadCallerAsync(organizationId, callerId, cancellationToken);
        if (!caller.Permissions.Contains(Permissions.OrganizationMembersManage))
        {
            throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
        }

        PagedResult<InvitationRow> page = await invitationQueries.ListAsync(organizationId, request, cancellationToken);
        IReadOnlyList<RoleGrantRole> roles = await roleGrantReader.LoadRolesAsync(
            organizationId, page.Items.Select(row => row.RoleId).Distinct().ToList(), cancellationToken);
        Dictionary<Guid, RoleGrantRole> rolesById = roles.ToDictionary(role => role.Id);
        DateTime utcNow = dateTimeProvider.UtcNow;

        // A role deleted since the page was read took its invitations with it (ON DELETE CASCADE).
        List<InvitationDto> items = page.Items
            .Where(row => rolesById.ContainsKey(row.RoleId))
            .Select(row => ToDto(row, rolesById[row.RoleId], caller, utcNow))
            .ToList();

        return new PagedResult<InvitationDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    public async Task<InvitationDto> ResendAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();
        InvitationDto? sent = null;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            (RoleGrantCaller caller, OrganizationInvitation invitation, RoleGrantRole role) =
                await LockAndLoadInvitationAsync(organizationId, callerId, invitationId, ct);

            sent = await ReplaceAndSendAsync(organizationId, caller, invitation.Email, role, invitation, ct);
        }, cancellationToken);

        return sent ?? throw new InvalidOperationException("The invitation transaction completed without a result.");
    }

    public async Task RevokeOrDeleteAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            (RoleGrantCaller caller, OrganizationInvitation invitation, RoleGrantRole role) =
                await LockAndLoadInvitationAsync(organizationId, callerId, invitationId, ct);

            // Recorded before the delete so the entry is built from the invitation while it still exists (D5).
            AuditEventType type = invitation.IsExpiredAt(dateTimeProvider.UtcNow)
                ? AuditEventType.InvitationDeleted
                : AuditEventType.InvitationRevoked;
            await auditTrail.RecordAsync(InvitationEvent(invitation, type, caller, role), ct);
            unitOfWork.Repository<OrganizationInvitation, Guid>().Delete(invitation);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    internal static InvitationDto ToDto(
        OrganizationInvitation invitation, RoleGrantRole role, RoleGrantCaller caller, DateTime utcNow) =>
        ToDto(
            new InvitationRow(invitation.Id, invitation.Email, invitation.RoleId, invitation.ExpiresAt),
            role,
            caller,
            utcNow);

    // The one place an InvitationDto is built, so its flags follow RoleGrantPolicy like the writes (TECH00 §5.3).
    internal static InvitationDto ToDto(
        InvitationRow invitation, RoleGrantRole role, RoleGrantCaller caller, DateTime utcNow)
    {
        RoleGrantDenial? denial = RoleGrantPolicy.CheckTakeAwayAll(caller, [role]);

        return new InvitationDto(
            invitation.Id,
            invitation.Email,
            new RoleRefDto(role.Id, role.Name, role.SystemKind),
            invitation.ExpiresAt,
            OrganizationInvitation.HasExpired(invitation.ExpiresAt, utcNow)
                ? InvitationStatus.Expired
                : InvitationStatus.Pending,
            denial is null,
            denial is null,
            denial?.Reason);
    }

    // Resend and revoke, in the D2 order: lock, gate re-check and members.manage, the invitation in the organization
    // (404 by id only, F17), then BR-58 on its role (403).
    private async Task<(RoleGrantCaller Caller, OrganizationInvitation Invitation, RoleGrantRole Role)>
        LockAndLoadInvitationAsync(Guid organizationId, Guid callerId, Guid invitationId, CancellationToken ct)
    {
        RoleGrantCaller caller = await LockAndLoadManagerAsync(organizationId, callerId, ct);

        OrganizationInvitation invitation = await unitOfWork.Repository<OrganizationInvitation, Guid>()
                .FirstOrDefaultAsync(
                    candidate => candidate.Id == invitationId && candidate.OrganizationId == organizationId, ct)
            ?? throw new NotFoundException(nameof(OrganizationInvitation), invitationId);
        RoleGrantRole role = await LoadRoleOfAsync(organizationId, invitation, ct);

        if (RoleGrantPolicy.CheckTakeAwayAll(caller, [role]) is { } denial)
        {
            throw new ForbiddenException(RoleGrantPolicy.ErrorCode(denial));
        }

        return (caller, invitation, role);
    }

    // After the lock: BR-58 for the role and any earlier invitation's role (403), the address not an active member
    // (409), then the earlier invitation replaced, recorded, saved and mailed (TECH00 §6.2).
    private async Task<InvitationDto> ReplaceAndSendAsync(
        Guid organizationId,
        RoleGrantCaller caller,
        string email,
        RoleGrantRole role,
        OrganizationInvitation? previous,
        CancellationToken ct)
    {
        // Replacing takes the earlier role away (BR-63 point 3); one check lets an Owner refusal win (§5.3, AC-341).
        List<RoleGrantRole> checkedRoles = [role];
        if (previous is not null && previous.RoleId != role.Id)
        {
            checkedRoles.Add(await LoadRoleOfAsync(organizationId, previous, ct));
        }

        if (RoleGrantPolicy.CheckGrantAll(caller, checkedRoles) is { } denial)
        {
            throw new ForbiddenException(RoleGrantPolicy.ErrorCode(denial));
        }

        if (await IsActiveMemberAsync(organizationId, email, ct))
        {
            throw new ConflictException(ApplicationMessages.InvitationEmailAlreadyMember);
        }

        IRepository<OrganizationInvitation, Guid> invitations = unitOfWork.Repository<OrganizationInvitation, Guid>();
        if (previous is not null)
        {
            invitations.Delete(previous);
        }

        // Never logged, recorded or put in an exception: only its hash is stored (TECH00 §6.2, §7.3).
        string rawToken = RandomToken.Create();
        DateTime utcNow = dateTimeProvider.UtcNow;
        OrganizationInvitation invitation = OrganizationInvitation.Create(new OrganizationInvitationParams(
            organizationId, email, role.Id, TokenHash.Compute(rawToken), utcNow));
        await invitations.AddAsync(invitation, ct);

        await auditTrail.RecordAsync(InvitationEvent(invitation, AuditEventType.InvitationSent, caller, role), ct);
        await unitOfWork.SaveChangesAsync(ct);

        await MailAsync(organizationId, invitation, role, rawToken, ct);

        return ToDto(invitation, role, caller, utcNow);
    }

    private static AuditEvent InvitationEvent(
        OrganizationInvitation invitation, AuditEventType type, RoleGrantCaller caller, RoleGrantRole role) =>
        AuditEvent.For(
            invitation,
            type,
            ActorKind.Account,
            caller.AccountId,
            new Dictionary<string, object>
            {
                ["email"] = invitation.Email,
                ["role"] = new AuditRoleRef(role.Id, role.Name, role.SystemKind)
            });

    // The foreign key cascades a deleted role to its invitations, so under the lock the role is always there.
    private async Task<RoleGrantRole> LoadRoleOfAsync(
        Guid organizationId, OrganizationInvitation invitation, CancellationToken ct)
    {
        IReadOnlyList<RoleGrantRole> roles = await roleGrantReader.LoadRolesAsync(organizationId, [invitation.RoleId], ct);
        return roles.Count == 1
            ? roles[0]
            : throw new InvalidOperationException($"The role of invitation {invitation.Id} could not be loaded.");
    }

    private async Task<bool> IsActiveMemberAsync(Guid organizationId, string email, CancellationToken ct)
    {
        Account? account = await unitOfWork.Repository<Account, Guid>()
            .FirstOrDefaultAsync(candidate => candidate.Email == email, ct);
        if (account is null)
        {
            return false;
        }

        OrganizationMember? member = await unitOfWork.Repository<OrganizationMember, Guid>().FirstOrDefaultAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.AccountId == account.Id
                && candidate.IsActive,
            ct);
        return member is not null;
    }

    // Throws inside the transaction, so the new invitation rolls back and an earlier one stays (TECH00 §6.2, QĐ-10).
    private async Task MailAsync(
        Guid organizationId, OrganizationInvitation invitation, RoleGrantRole role, string rawToken, CancellationToken ct)
    {
        Organization organization = await unitOfWork.Repository<Organization, Guid>().GetByIdAsync(organizationId, ct)
            ?? throw new NotFoundException(nameof(Organization), organizationId);
        string roleName = role.SystemKind is { } kind ? SystemRoleDisplayNames.Get(kind, "vi") : role.Name;
        string url = $"{emailSettings.FrontendBaseUrl.TrimEnd('/')}/#/invite?token={rawToken}";
        (string subject, string body) = InvitationEmail.Render(organization.Name, roleName, invitation.ExpiresAt, url);

        try
        {
            await emailSender.SendAsync(invitation.Email, subject, body, ct);
        }
        catch (ServiceUnavailableException)
        {
            // No exception text either: only the invitation id may reach the log (TECH00 §6.2 point 3, §7.3).
            logger.LogError("Failed to send invitation {InvitationId}", invitation.Id);
            throw new ServiceUnavailableException(ApplicationMessages.InvitationEmailDeliveryFailed);
        }
    }

    private Task<RoleGrantCaller> LockAndLoadManagerAsync(
        Guid organizationId, Guid callerId, CancellationToken cancellationToken) =>
        RoleWriteGate.LockAndLoadCallerAsync(
            organizationLock,
            unitOfWork,
            roleGrantReader,
            organizationId,
            callerId,
            Permissions.OrganizationMembersManage,
            cancellationToken);

    private Guid RequireOrganizationId() =>
        currentTenantProvider.OrganizationId
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
