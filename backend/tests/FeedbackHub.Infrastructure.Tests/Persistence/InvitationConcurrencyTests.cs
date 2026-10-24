using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Auth;
using FeedbackHub.Application.Services.Invitations;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Persistence.Repositories;
using FeedbackHub.Infrastructure.Services.Context;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

// Commits rows on purpose: the organization lock only orders two real transactions.
[Collection(nameof(PostgresCollection))]
public sealed partial class InvitationConcurrencyTests(PostgresContainerFixture fixture)
{
    // How long the second request is watched while the first one holds the lock.
    private static readonly TimeSpan BlockedWindow = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan StepLimit = TimeSpan.FromSeconds(10);

    // Holds the first request inside its transaction, after its checks and writes, until released.
    private sealed class PausingAuditTrail(IAuditTrail inner, TaskCompletionSource entered, Task release) : IAuditTrail
    {
        public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await release.WaitAsync(StepLimit, cancellationToken);
            await inner.RecordAsync(auditEvent, cancellationToken);
        }
    }

    private sealed record Seeded(
        Guid OrganizationId, string OrganizationName, Guid OwnerId, Guid InviteeId, string InviteeEmail, Guid MemberRoleId);

    [GeneratedRegex("#/invite\\?token=([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    private static ServiceProvider RepositoriesFor(AppDbContext context) =>
        new ServiceCollection()
            .AddSingleton(context)
            .AddScoped(typeof(IRepository<,>), typeof(GenericRepository<,>))
            .BuildServiceProvider();

    private static InvitationService CreateService(
        AppDbContext context,
        Guid organizationId,
        Guid callerId,
        Func<IAuditTrail, IAuditTrail>? wrapTrail = null,
        IEmailSender? emailSender = null)
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(callerId.ToString());
        ICurrentTenantProvider tenant = Substitute.For<ICurrentTenantProvider>();
        tenant.OrganizationId.Returns(organizationId);
        IAuditTrail auditTrail = new AuditTrail(context, new DateTimeProvider());

        return new InvitationService(
            currentUser,
            tenant,
            new UnitOfWork(context, RepositoriesFor(context)),
            new OrganizationLock(context),
            new RoleGrantReader(context),
            new InvitationQueries(context),
            wrapTrail?.Invoke(auditTrail) ?? auditTrail,
            emailSender ?? Substitute.For<IEmailSender>(),
            new DateTimeProvider(),
            Options.Create(new EmailSettings { FrontendBaseUrl = "https://hub.example.com/" }),
            Substitute.For<ITenantAccessService>(),
            Substitute.For<IPermissionResolver>(),
            Substitute.For<ITokenIssuer>(),
            Substitute.For<IPasswordHasher>(),
            NullLogger<InvitationService>.Instance);
    }

    // Two confirmed accounts: the Owner, and the invitee who is not a member.
    private async Task<Seeded> SeedAsync()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        Organization organization = Organization.Create(
            new OrganizationParams("Bảo Minh", $"invitations-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        Role owner = Role.Create(new RoleParams(organization.Id, "Owner", SystemRoleKind.Owner));
        Role member = Role.Create(new RoleParams(organization.Id, "Member", SystemRoleKind.Member));
        context.Roles.AddRange(owner, member);

        Account ownerAccount = AddConfirmedAccount(context, "Chủ sở hữu");
        Account invitee = AddConfirmedAccount(context, "Trần Bảo");
        OrganizationMember ownerRow =
            OrganizationMember.Create(new OrganizationMemberParams(organization.Id, ownerAccount.Id));
        context.OrganizationMembers.Add(ownerRow);
        context.OrganizationMemberRoles.Add(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(ownerRow.Id, owner.Id)));

        await context.SaveChangesAsync();
        return new Seeded(organization.Id, organization.Name, ownerAccount.Id, invitee.Id, invitee.Email, member.Id);
    }

    private static Account AddConfirmedAccount(AppDbContext context, string name)
    {
        string handle = $"u{Guid.NewGuid():N}"[..20];
        Account account = Account.Create(new AccountParams(name, handle, $"{handle}@example.com"));
        account.ConfirmEmail();
        context.Accounts.Add(account);
        return account;
    }

    // Sent through the service and read back from the mail, so only production code ever hashes the token.
    private async Task<string> SendInvitationAsync(Seeded seeded)
    {
        IEmailSender emailSender = Substitute.For<IEmailSender>();
        string? body = null;
        emailSender.When(sender => sender.SendAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => body = callInfo.ArgAt<string>(2));
        await using AppDbContext context = fixture.CreateDbContext();
        InvitationService owner =
            CreateService(context, seeded.OrganizationId, seeded.OwnerId, emailSender: emailSender);

        await owner.SendAsync(
            new SendInvitationRequest(seeded.InviteeEmail, seeded.MemberRoleId), CancellationToken.None);

        Match link = InviteLink().Match(body ?? string.Empty);
        Assert.True(link.Success);
        return link.Groups[1].Value;
    }

    // Review Focus 3, F12: two tabs accept one link; the second waits for the lock, then finds itself already in.
    [Fact]
    public async Task AcceptAsync_TwoTabsAcceptOneTokenConcurrently_BothJoinOnceAndInvitationIsGone()
    {
        Seeded seeded = await SeedAsync();
        string token = await SendInvitationAsync(seeded);
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        InvitationService first = CreateService(
            firstContext, Guid.Empty, seeded.InviteeId,
            trail => new PausingAuditTrail(trail, firstEntered, releaseFirst.Task));
        InvitationService second = CreateService(secondContext, Guid.Empty, seeded.InviteeId);

        Task<AcceptInvitationResult> firstAccept =
            first.AcceptAsync(new InvitationTokenRequest(token), CancellationToken.None);
        await Task.WhenAny(firstEntered.Task, firstAccept).WaitAsync(StepLimit);
        Assert.False(firstAccept.IsCompleted, firstAccept.Exception?.ToString());
        Task<AcceptInvitationResult> secondAccept =
            second.AcceptAsync(new InvitationTokenRequest(token), CancellationToken.None);
        await Task.WhenAny(secondAccept, Task.Delay(BlockedWindow));
        bool secondWaitedForLock = !secondAccept.IsCompleted;
        releaseFirst.SetResult();

        AcceptInvitationResult[] results = await Task.WhenAll(firstAccept, secondAccept).WaitAsync(StepLimit);

        Assert.True(secondWaitedForLock);
        AcceptInvitationResult expected = new(seeded.OrganizationId, seeded.OrganizationName);
        Assert.All(results, result => Assert.Equal(expected, result));

        await using AppDbContext reader = fixture.CreateDbContext();
        OrganizationMember member = await reader.OrganizationMembers.SingleAsync(
            candidate => candidate.OrganizationId == seeded.OrganizationId && candidate.AccountId == seeded.InviteeId);
        Assert.True(member.IsActive);
        Assert.Equal(
            [seeded.MemberRoleId],
            await reader.OrganizationMemberRoles
                .Where(link => link.OrganizationMemberId == member.Id)
                .Select(link => link.RoleId)
                .ToListAsync());
        Assert.False(await reader.OrganizationInvitations.AnyAsync(
            invitation => invitation.OrganizationId == seeded.OrganizationId));
        Assert.Equal(1, await CountEventsAsync(seeded.OrganizationId, AuditEventType.MemberJoined));
    }

    // F12, TECH00 §5.4: two sends to one address leave one invitation; the second replaced the first.
    [Fact]
    public async Task SendAsync_TwoSendsToOneEmailConcurrently_LeaveOneInvitation()
    {
        Seeded seeded = await SeedAsync();
        SendInvitationRequest request = new($"invitee-{Guid.NewGuid():N}@example.com", seeded.MemberRoleId);
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        InvitationService first = CreateService(
            firstContext, seeded.OrganizationId, seeded.OwnerId,
            trail => new PausingAuditTrail(trail, firstEntered, releaseFirst.Task));
        InvitationService second = CreateService(secondContext, seeded.OrganizationId, seeded.OwnerId);

        Task<InvitationDto> firstSend = first.SendAsync(request, CancellationToken.None);
        await Task.WhenAny(firstEntered.Task, firstSend).WaitAsync(StepLimit);
        Assert.False(firstSend.IsCompleted, firstSend.Exception?.ToString());
        Task<InvitationDto> secondSend = second.SendAsync(request, CancellationToken.None);
        await Task.WhenAny(secondSend, Task.Delay(BlockedWindow));
        bool secondWaitedForLock = !secondSend.IsCompleted;
        releaseFirst.SetResult();

        InvitationDto[] sent = await Task.WhenAll(firstSend, secondSend).WaitAsync(StepLimit);

        Assert.True(secondWaitedForLock);
        await using AppDbContext reader = fixture.CreateDbContext();
        OrganizationInvitation stored = await reader.OrganizationInvitations.SingleAsync(
            invitation => invitation.OrganizationId == seeded.OrganizationId);
        Assert.Equal(sent[1].Id, stored.Id);
        Assert.Equal(2, await CountEventsAsync(seeded.OrganizationId, AuditEventType.InvitationSent));
    }

    private async Task<int> CountEventsAsync(Guid organizationId, AuditEventType type)
    {
        await using AppDbContext reader = fixture.CreateDbContext();
        return await reader.AuditLogs.CountAsync(log => log.OrganizationId == organizationId
            && log.Kind == AuditLogKind.Event
            && log.Action == type.ToString());
    }
}
