using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Auth;
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
    ITenantAccessService tenantAccessService,
    IPermissionResolver permissionResolver,
    ITokenIssuer tokenIssuer,
    IPasswordHasher passwordHasher,
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

            if (RoleGrantPolicy.CheckTakeAwayAll(caller, [role]) is { } denial)
            {
                throw new ForbiddenException(RoleGrantPolicy.ErrorCode(denial));
            }

            // Recorded before the delete so the entry is built from the invitation while it still exists (D5).
            AuditEventType type = invitation.IsExpiredAt(dateTimeProvider.UtcNow)
                ? AuditEventType.InvitationDeleted
                : AuditEventType.InvitationRevoked;
            await auditTrail.RecordAsync(InvitationEvent(invitation, type, caller, role), ct);
            unitOfWork.Repository<OrganizationInvitation, Guid>().Delete(invitation);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task<InvitationLookupDto> LookupAsync(
        InvitationTokenRequest request, CancellationToken cancellationToken)
    {
        // Read only: no lock and no write, so opening the link never uses it up (TECH00 §6.3).
        OrganizationInvitation? invitation = await FindByTokenAsync(request.Token, cancellationToken);
        if (invitation is null)
        {
            return LookupResult(InvitationLookupStatus.NoLongerValid);
        }

        if (invitation.IsExpiredAt(dateTimeProvider.UtcNow))
        {
            return LookupResult(InvitationLookupStatus.Expired, expiresAt: invitation.ExpiresAt);
        }

        Account? account = TryGetSessionAccountId() is { } accountId
            ? await unitOfWork.Repository<Account, Guid>().GetByIdAsync(accountId, cancellationToken)
            : null;
        if (account is not null && !IsInvitedAccount(invitation, account))
        {
            return LookupResult(InvitationLookupStatus.WrongAccount);
        }

        if (account is { EmailConfirmed: false })
        {
            return LookupResult(InvitationLookupStatus.EmailNotConfirmed);
        }

        Organization? organization = await unitOfWork.Repository<Organization, Guid>()
            .GetByIdAsync(invitation.OrganizationId, cancellationToken);
        IReadOnlyList<RoleGrantRole> roles = await roleGrantReader.LoadRolesAsync(
            invitation.OrganizationId, [invitation.RoleId], cancellationToken);

        // Deleting the organization or the role since the first read took the invitation with it (ON DELETE CASCADE).
        if (organization is null || roles.Count != 1)
        {
            return LookupResult(InvitationLookupStatus.NoLongerValid);
        }

        // Anonymous visitors see a suspended organization's invitation as valid (BR-81 point 6, AC-332).
        if (account is not null && !organization.Status)
        {
            return LookupResult(
                InvitationLookupStatus.OrganizationSuspended, organization.Name, expiresAt: invitation.ExpiresAt);
        }

        return LookupResult(
            InvitationLookupStatus.Valid,
            organization.Name,
            new RoleRefDto(roles[0].Id, roles[0].Name, roles[0].SystemKind),
            invitation.ExpiresAt,
            invitation.Email);
    }

    public async Task<AcceptInvitationResult> AcceptAsync(
        InvitationTokenRequest request, CancellationToken cancellationToken)
    {
        Guid accountId = TryGetSessionAccountId()
            ?? throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        AcceptInvitationResult? accepted = null;
        bool joined = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            (Guid organizationId, OrganizationInvitation? invitation) =
                await LockInvitationOrganizationAsync(request.Token, ct);
            OrganizationMember? member = await unitOfWork.Repository<OrganizationMember, Guid>().FirstOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.AccountId == accountId, ct);

            // A second tab joined while this one waited for the lock (TECH00 §6.3).
            if (invitation is null)
            {
                accepted = member is { IsActive: true }
                    ? await AcceptedAsync(organizationId, ct)
                    : throw new ConflictException(ApplicationMessages.InvitationNoLongerValid);
                return;
            }

            Account account = await unitOfWork.Repository<Account, Guid>().GetByIdAsync(accountId, ct)
                ?? throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
            Organization organization = await CheckCanJoinAsync(invitation, account, ct);

            if (member is { IsActive: true })
            {
                unitOfWork.Repository<OrganizationInvitation, Guid>().Delete(invitation);
            }
            else
            {
                await JoinAsync(invitation, account, ct);
                joined = true;
            }

            await unitOfWork.SaveChangesAsync(ct);
            accepted = new AcceptInvitationResult(organization.Id, organization.Name);
        }, cancellationToken);

        if (joined && accepted is not null)
        {
            await InvalidateMemberCachesAsync(accepted.OrganizationId, accountId, cancellationToken);
        }

        return accepted ?? throw new InvalidOperationException("The invitation transaction completed without a result.");
    }

    public async Task<RegisterWithInvitationResult> RegisterAsync(
        RegisterWithInvitationRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        Account? registered = null;
        Guid? joinedOrganizationId = null;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            (_, OrganizationInvitation? invitation) = await LockInvitationOrganizationAsync(request.Token, ct);
            if (invitation is null)
            {
                throw new ConflictException(ApplicationMessages.InvitationNoLongerValid);
            }

            if (invitation.IsExpiredAt(dateTimeProvider.UtcNow))
            {
                throw new ConflictException(ApplicationMessages.InvitationExpired);
            }

            IRepository<Account, Guid> accounts = unitOfWork.Repository<Account, Guid>();
            await AccountUniqueness.EnsureAvailableAsync(accounts, request.Username, invitation.Email, ct);

            // The link proves the address (BR-63 point 6): the email is the invitation's and counts as verified.
            Account account = Account.Create(request.ToParams(invitation.Email));
            account.SetPasswordHash(passwordHasher.Hash(request.Password));
            account.ConfirmEmail();
            await accounts.AddAsync(account, ct);

            Organization organization = await unitOfWork.Repository<Organization, Guid>()
                    .GetByIdAsync(invitation.OrganizationId, ct)
                ?? throw new ConflictException(ApplicationMessages.InvitationNoLongerValid);
            // A suspended organization keeps the invitation, to be accepted once reactivated (BR-81 point 6, AC-333).
            if (organization.Status)
            {
                await JoinAsync(invitation, account, ct);
                joinedOrganizationId = invitation.OrganizationId;
            }

            await unitOfWork.SaveChangesAsync(ct);
            registered = account;
        }, cancellationToken);

        Account created = registered
            ?? throw new InvalidOperationException("The registration transaction completed without an account.");
        if (joinedOrganizationId is { } organizationId)
        {
            await InvalidateMemberCachesAsync(organizationId, created.Id, cancellationToken);
        }

        // After the commit, so the session carries the saved membership and never outlives a rollback (QĐ-11).
        AuthResult auth = await tokenIssuer.IssueTokensAsync(
            created,
            joinedOrganizationId,
            deviceInfo,
            ipAddress,
            isPersistent: false,
            loginAt: dateTimeProvider.UtcNow,
            familyId: null,
            cancellationToken);
        return new RegisterWithInvitationResult(
            auth, joinedOrganizationId is null ? InvitationOutcome.OrganizationSuspended : InvitationOutcome.Joined);
    }

    // Reactivates or creates the member row with exactly the invitation's role, records it and uses the invitation up;
    // the caller saves. Leftover roles of a former member go (BR-30, QĐ-16).
    internal async Task JoinAsync(OrganizationInvitation invitation, Account account, CancellationToken ct)
    {
        RoleGrantRole role = await LoadRoleOfAsync(invitation.OrganizationId, invitation, ct);
        IRepository<OrganizationMember, Guid> members = unitOfWork.Repository<OrganizationMember, Guid>();
        OrganizationMember? member = await members.FirstOrDefaultAsync(
            candidate => candidate.OrganizationId == invitation.OrganizationId && candidate.AccountId == account.Id, ct);

        if (member is null)
        {
            member = OrganizationMember.Create(new OrganizationMemberParams(invitation.OrganizationId, account.Id));
            await members.AddAsync(member, ct);
        }
        else
        {
            member.Reactivate();
            members.Update(member);
        }

        IRepository<OrganizationMemberRole, Guid> memberRoles = unitOfWork.Repository<OrganizationMemberRole, Guid>();
        Guid memberId = member.Id;
        IReadOnlyList<OrganizationMemberRole> links =
            await memberRoles.ListAsync(link => link.OrganizationMemberId == memberId, ct);
        foreach (OrganizationMemberRole link in links.Where(link => link.RoleId != role.Id))
        {
            memberRoles.Delete(link);
        }

        if (links.All(link => link.RoleId != role.Id))
        {
            await memberRoles.AddAsync(
                OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, role.Id)), ct);
        }

        await auditTrail.RecordAsync(
            AuditEvent.For(
                member,
                AuditEventType.MemberJoined,
                ActorKind.Account,
                account.Id,
                new Dictionary<string, object>
                {
                    ["accountName"] = account.Name,
                    ["email"] = account.Email,
                    ["role"] = new AuditRoleRef(role.Id, role.Name, role.SystemKind)
                }),
            ct);
        unitOfWork.Repository<OrganizationInvitation, Guid>().Delete(invitation);
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

    // Resend and revoke, in the D2 order: lock, gate re-check and members.manage, then the invitation in the
    // organization (404 by id only, F17). BR-58 (403) comes after it, once per operation: in RevokeOrDeleteAsync,
    // and for resend in ReplaceAndSendAsync.
    private async Task<(RoleGrantCaller Caller, OrganizationInvitation Invitation, RoleGrantRole Role)>
        LockAndLoadInvitationAsync(Guid organizationId, Guid callerId, Guid invitationId, CancellationToken ct)
    {
        RoleGrantCaller caller = await LockAndLoadManagerAsync(organizationId, callerId, ct);

        OrganizationInvitation invitation = await unitOfWork.Repository<OrganizationInvitation, Guid>()
                .FirstOrDefaultAsync(
                    candidate => candidate.Id == invitationId && candidate.OrganizationId == organizationId, ct)
            ?? throw new NotFoundException(nameof(OrganizationInvitation), invitationId);
        RoleGrantRole role = await LoadRoleOfAsync(organizationId, invitation, ct);

        return (caller, invitation, role);
    }

    // After the lock and any 404: BR-58 for the role and any earlier invitation's role (403), the address not an
    // active member (409), then the earlier invitation replaced, recorded, saved and mailed (TECH00 §6.2).
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

    // BR-81 point 3 after the lock, in the order of TECH00 §6.3; the account and organization come from the database.
    private async Task<Organization> CheckCanJoinAsync(
        OrganizationInvitation invitation, Account account, CancellationToken ct)
    {
        if (invitation.IsExpiredAt(dateTimeProvider.UtcNow))
        {
            throw new ConflictException(ApplicationMessages.InvitationExpired);
        }

        if (!IsInvitedAccount(invitation, account))
        {
            throw new ConflictException(ApplicationMessages.InvitationWrongAccount);
        }

        if (!account.EmailConfirmed)
        {
            throw new ForbiddenException(ApplicationMessages.EmailNotConfirmed);
        }

        Organization organization = await unitOfWork.Repository<Organization, Guid>()
                .GetByIdAsync(invitation.OrganizationId, ct)
            ?? throw new ConflictException(ApplicationMessages.InvitationNoLongerValid);
        return organization.Status
            ? organization
            : throw new ConflictException(ApplicationMessages.InvitationOrganizationSuspended);
    }

    private async Task<AcceptInvitationResult> AcceptedAsync(Guid organizationId, CancellationToken ct)
    {
        Organization organization = await unitOfWork.Repository<Organization, Guid>().GetByIdAsync(organizationId, ct)
            ?? throw new ConflictException(ApplicationMessages.InvitationNoLongerValid);
        return new AcceptInvitationResult(organization.Id, organization.Name);
    }

    // The current address only: a pending email change does not count (BR-51, AC-348).
    private static bool IsInvitedAccount(OrganizationInvitation invitation, Account account) =>
        Account.NormalizeEmail(account.Email) == invitation.Email;

    // The invitee is not a member, so no write gate: the first read only names the organization to lock, and the
    // invitation is read again under the lock (F9).
    private async Task<(Guid OrganizationId, OrganizationInvitation? Invitation)> LockInvitationOrganizationAsync(
        string token, CancellationToken ct)
    {
        OrganizationInvitation found = await FindByTokenAsync(token, ct)
            ?? throw new ConflictException(ApplicationMessages.InvitationNoLongerValid);
        await organizationLock.AcquireAsync(found.OrganizationId, ct);
        return (found.OrganizationId, await FindByTokenAsync(token, ct));
    }

    // By hash through the unique index; the raw token goes nowhere else (TECH00 §7.3, F17).
    private Task<OrganizationInvitation?> FindByTokenAsync(string token, CancellationToken ct)
    {
        string tokenHash = TokenHash.Compute(token);
        return unitOfWork.Repository<OrganizationInvitation, Guid>()
            .FirstOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, ct);
    }

    private static InvitationLookupDto LookupResult(
        InvitationLookupStatus status,
        string? organizationName = null,
        RoleRefDto? role = null,
        DateTime? expiresAt = null,
        string? email = null) =>
        new(status, organizationName, role, expiresAt, email);

    // Only after the commit: a request between the invalidation and the commit would cache the old state.
    private async Task InvalidateMemberCachesAsync(Guid organizationId, Guid accountId, CancellationToken ct)
    {
        await tenantAccessService.InvalidateMemberAsync(organizationId, accountId, ct);
        await permissionResolver.InvalidateMemberAsync(organizationId, accountId, ct);
    }

    // An API key acts for an organization, not a person, so it counts as no session (TECH00 §5.2).
    private Guid? TryGetSessionAccountId() =>
        !currentUserService.IsApiKeyCaller && Guid.TryParse(currentUserService.UserId, out Guid accountId)
            ? accountId
            : null;

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
