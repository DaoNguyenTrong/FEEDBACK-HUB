using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
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

            sent = await ReplaceAndSendAsync(organizationId, caller, email, role, ct);
        }, cancellationToken);

        return sent ?? throw new InvalidOperationException("The invitation transaction completed without a result.");
    }

    /// <summary>The one place a <see cref="InvitationDto"/> is built, so its flags follow <see cref="RoleGrantPolicy"/>.</summary>
    internal static InvitationDto ToDto(
        OrganizationInvitation invitation, RoleGrantRole role, RoleGrantCaller caller, DateTime utcNow)
    {
        RoleGrantDenial? denial = RoleGrantPolicy.CheckTakeAwayAll(caller, [role]);

        return new InvitationDto(
            invitation.Id,
            invitation.Email,
            new RoleRefDto(role.Id, role.Name, role.SystemKind),
            invitation.ExpiresAt,
            invitation.IsExpiredAt(utcNow) ? InvitationStatus.Expired : InvitationStatus.Pending,
            denial is null,
            denial is null,
            denial?.Reason);
    }

    /// <summary>
    /// After the lock and the role lookup: BR-58 for the new role and any earlier invitation's role (403), the address
    /// not an active member (409), then the earlier invitation replaced, recorded, saved and mailed (TECH00 §6.2).
    /// </summary>
    private async Task<InvitationDto> ReplaceAndSendAsync(
        Guid organizationId, RoleGrantCaller caller, string email, RoleGrantRole role, CancellationToken ct)
    {
        IRepository<OrganizationInvitation, Guid> invitations = unitOfWork.Repository<OrganizationInvitation, Guid>();
        OrganizationInvitation? previous = await invitations.FirstOrDefaultAsync(
            invitation => invitation.OrganizationId == organizationId && invitation.Email == email, ct);

        // Replacing takes the earlier role away (BR-63 point 3); one check lets an Owner refusal win (§5.3, AC-341).
        List<RoleGrantRole> checkedRoles = [role];
        if (previous is not null && previous.RoleId != role.Id)
        {
            checkedRoles.Add(await LoadPreviousRoleAsync(organizationId, previous, ct));
        }

        if (RoleGrantPolicy.CheckGrantAll(caller, checkedRoles) is { } denial)
        {
            throw new ForbiddenException(RoleGrantPolicy.ErrorCode(denial));
        }

        if (await IsActiveMemberAsync(organizationId, email, ct))
        {
            throw new ConflictException(ApplicationMessages.InvitationEmailAlreadyMember);
        }

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

        await auditTrail.RecordAsync(
            AuditEvent.For(
                invitation,
                AuditEventType.InvitationSent,
                ActorKind.Account,
                caller.AccountId,
                new Dictionary<string, object>
                {
                    ["email"] = email,
                    ["role"] = new AuditRoleRef(role.Id, role.Name, role.SystemKind)
                }),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        await MailAsync(organizationId, invitation, role, rawToken, ct);

        return ToDto(invitation, role, caller, utcNow);
    }

    // The foreign key cascades a deleted role to its invitations, so under the lock the role is always there.
    private async Task<RoleGrantRole> LoadPreviousRoleAsync(
        Guid organizationId, OrganizationInvitation previous, CancellationToken ct)
    {
        IReadOnlyList<RoleGrantRole> roles = await roleGrantReader.LoadRolesAsync(organizationId, [previous.RoleId], ct);
        return roles.Count == 1
            ? roles[0]
            : throw new InvalidOperationException($"The role of invitation {previous.Id} could not be loaded.");
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

    /// <exception cref="ServiceUnavailableException">
    /// <c>InvitationEmailDeliveryFailed</c>, thrown inside the transaction so the new invitation is rolled back and an
    /// earlier one stays as it was (TECH00 §6.2, QĐ-10).
    /// </exception>
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
