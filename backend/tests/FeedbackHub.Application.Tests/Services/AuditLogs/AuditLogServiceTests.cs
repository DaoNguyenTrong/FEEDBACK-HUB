using Microsoft.Extensions.Logging;
using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditLogs;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Application.Tests.Services.AuditLogs;

public class AuditLogServiceTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc);

    private sealed record Fixture(
        AuditLogService Service,
        IAuditLogRepository Repository,
        ICurrentTenantProvider Tenant,
        ICurrentUserService User,
        IRoleGrantReader Grants,
        CapturingLogger<AuditLogService> Logger);

    private static Fixture CreateFixture(bool canView = true)
    {
        IAuditLogRepository repository = Substitute.For<IAuditLogRepository>();
        ICurrentTenantProvider tenant = Substitute.For<ICurrentTenantProvider>();
        tenant.OrganizationId.Returns(OrganizationId);
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(AccountId.ToString());
        IRoleGrantReader grants = Substitute.For<IRoleGrantReader>();
        grants.LoadCallerAsync(OrganizationId, AccountId, Arg.Any<CancellationToken>())
            .Returns(new RoleGrantCaller(
                AccountId, false, canView ? new HashSet<string> { Permissions.AuditLogsView } : new HashSet<string>()));
        repository.ListEventsAsync(
                Arg.Any<Guid>(), Arg.Any<AuditCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        CapturingLogger<AuditLogService> logger = new();
        AuditLogService service = new(repository, tenant, user, grants, logger);

        return new Fixture(service, repository, tenant, user, grants, logger);
    }

    private static AuditEventRow Row(
        long id = 1,
        string entity = "OrganizationInvitation",
        string action = "InvitationSent",
        string? actorKind = "Account",
        string? actorName = "An",
        string? newValues = """{"email":"a@x.com","role":{"id":"11111111-1111-1111-1111-111111111111","name":"Owner","systemKind":"Owner"}}""") =>
        new(id, Now.AddMinutes(-id), entity, action, actorKind, actorName, newValues);

    private static void Returns(Fixture f, params AuditEventRow[] rows) =>
        f.Repository.ListEventsAsync(
                Arg.Any<Guid>(), Arg.Any<AuditCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(rows);

    private static Task<AuditEntryPage> List(Fixture f, string? cursor = null, int limit = 50) =>
        f.Service.ListAsync(new AuditLogQuery(cursor, limit), CancellationToken.None);

    // ListAsync

    [Fact]
    public async Task ListAsync_PersonalContext_ThrowsForbiddenOrganizationAccessDenied()
    {
        Fixture f = CreateFixture();
        f.Tenant.OrganizationId.Returns((Guid?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => List(f));

        await f.Grants.DidNotReceiveWithAnyArgs().LoadCallerAsync(default, default, default);
        await f.Repository.DidNotReceiveWithAnyArgs().ListEventsAsync(default, default, default, default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task ListAsync_NoAuthenticatedAccount_ThrowsUnauthorizedWithoutQuerying(string? userId)
    {
        Fixture f = CreateFixture();
        f.User.UserId.Returns(userId);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired, () => List(f));

        await f.Repository.DidNotReceiveWithAnyArgs().ListEventsAsync(default, default, default, default);
    }

    [Fact]
    public async Task ListAsync_CallerLacksPermissionInDatabase_ThrowsForbiddenWithoutQuerying()
    {
        Fixture f = CreateFixture(canView: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => List(f));

        await f.Repository.DidNotReceiveWithAnyArgs().ListEventsAsync(default, default, default, default);
    }

    [Fact]
    public async Task ListAsync_MalformedCursorAndNoPermission_ThrowsForbiddenNotInvalidCursor()
    {
        Fixture f = CreateFixture(canView: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => List(f, cursor: "@@@"));
    }

    // ListAsync: cursor and paging

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ListAsync_LimitOutOfRange_ThrowsWithoutQuerying(int limit)
    {
        Fixture f = CreateFixture();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => List(f, limit: limit));

        await f.Repository.DidNotReceiveWithAnyArgs().ListEventsAsync(default, default, default, default);
    }

    [Theory]
    [InlineData("@@@")]
    [InlineData("")]
    [InlineData("A=")]
    public async Task ListAsync_MalformedCursor_Throws400InvalidCursorWithoutQuerying(string cursor)
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.InvalidCursor, () => List(f, cursor));

        await f.Repository.DidNotReceiveWithAnyArgs().ListEventsAsync(default, default, default, default);
    }

    [Fact]
    public async Task ListAsync_ValidCursor_PassesItsPositionToTheRepository()
    {
        Fixture f = CreateFixture();
        AuditCursor cursor = new(Now, 77);

        await List(f, cursor.Encode(), 10);

        await f.Repository.Received(1).ListEventsAsync(OrganizationId, cursor, 11, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_NoCursor_QueriesFromTheNewestEntry()
    {
        Fixture f = CreateFixture();

        await List(f, limit: 5);

        await f.Repository.Received(1).ListEventsAsync(OrganizationId, null, 6, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_AsksForLimitPlusOne_NextCursorFromLimitthRow()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(30), Row(20), Row(10));

        AuditEntryPage page = await List(f, limit: 2);

        await f.Repository.Received(1).ListEventsAsync(OrganizationId, null, 3, Arg.Any<CancellationToken>());
        Assert.Equal([30L, 20L], page.Items.Select(item => item.Id));
        Assert.True(AuditCursor.TryDecode(page.NextCursor!, out AuditCursor next));
        Assert.Equal(new AuditCursor(Now.AddMinutes(-20), 20), next);
    }

    [Fact]
    public async Task ListAsync_FewerThanLimitPlusOne_NextCursorNull()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(2), Row(1));

        AuditEntryPage page = await List(f, limit: 2);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ListAsync_AllRowsOfPageSkipped_ItemsEmptyButNextCursorKept()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(3, action: "Bogus"), Row(2, action: "Bogus"), Row(1));

        AuditEntryPage page = await List(f, limit: 2);

        Assert.Empty(page.Items);
        Assert.True(AuditCursor.TryDecode(page.NextCursor!, out AuditCursor next));
        Assert.Equal(2, next.Id);
    }

    // ListAsync: parameters

    [Fact]
    public async Task ListAsync_UndeclaredKeysStored_ReturnsOnlyDeclaredKeys()
    {
        Fixture f = CreateFixture();
        // BR-83 point 5: tokenHash and ip are not declared for InvitationSent, so they must never leave the service.
        Returns(f, Row(newValues:
            """{"email":"a@x.com","role":{"id":"11111111-1111-1111-1111-111111111111","name":"Owner","systemKind":"Owner"},"tokenHash":"h","ip":"1.2.3.4"}"""));

        AuditEntryPage page = await List(f);

        IReadOnlyDictionary<string, object> parameters = Assert.Single(page.Items).Parameters;
        Assert.Equal(["email", "role"], parameters.Keys.Order());
    }

    [Fact]
    public async Task ListAsync_DeclaredKinds_ConvertsEachToItsType()
    {
        Fixture f = CreateFixture();
        Guid ownerId = Guid.NewGuid();
        Guid customId = Guid.NewGuid();
        Returns(f,
            Row(2, "OrganizationMember", "MemberRolesChanged", newValues:
                $$"""{"accountName":"B","email":"b@x.com","rolesBefore":[],"rolesAfter":[{"id":"{{ownerId}}","name":"Owner","systemKind":"Owner"},{"id":"{{customId}}","name":"Custom","systemKind":null}]}"""),
            Row(1, "Role", "RoleCreated", newValues:
                $$"""{"role":{"id":"{{customId}}","name":"Custom"},"permissionCodes":["a.b","c.d"]}"""));

        AuditEntryPage page = await List(f);

        IReadOnlyDictionary<string, object> changed = page.Items[0].Parameters;
        Assert.Equal("B", Assert.IsType<string>(changed["accountName"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<RoleRefDto>>(changed["rolesBefore"]));
        Assert.Equal(
            [new RoleRefDto(ownerId, "Owner", SystemRoleKind.Owner), new RoleRefDto(customId, "Custom", null)],
            Assert.IsAssignableFrom<IReadOnlyList<RoleRefDto>>(changed["rolesAfter"]));
        IReadOnlyDictionary<string, object> created = page.Items[1].Parameters;
        Assert.Equal(new RoleRefDto(customId, "Custom", null), Assert.IsType<RoleRefDto>(created["role"]));
        Assert.Equal(["a.b", "c.d"], Assert.IsAssignableFrom<IReadOnlyList<string>>(created["permissionCodes"]));
    }

    [Theory]
    [InlineData("""{"email":5,"role":{"id":"11111111-1111-1111-1111-111111111111","name":"Owner"}}""")]
    [InlineData("""{"email":"a@x.com","role":"Owner"}""")]
    public async Task ListAsync_DeclaredKeyWithWrongShape_SkipsRowAndWarns(string newValues)
    {
        Fixture f = CreateFixture();
        Returns(f, Row(9, newValues: newValues));

        AuditEntryPage page = await List(f);

        Assert.Empty(page.Items);
        Assert.Equal((LogLevel.Warning, "Skipped audit entry 9"), Assert.Single(f.Logger.Entries));
    }

    [Theory]
    [InlineData("""{"email":"a@x.com","role":null}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":"11111111-1111-1111-1111-111111111111","name":"O","systemKind":1}}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":"11111111-1111-1111-1111-111111111111","name":"O","systemKind":"1"}}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":"11111111-1111-1111-1111-111111111111","name":"O","systemKind":"owner"}}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":"11111111-1111-1111-1111-111111111111","name":"O","systemKind":"5"}}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":5,"name":"O"}}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":"11111111-1111-1111-1111-111111111111","name":5}}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":"00000000-0000-0000-0000-000000000000","name":"O"}}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":"x","name":"O"}}""")]
    [InlineData("""{"email":"a@x.com","role":{"id":"11111111-1111-1111-1111-111111111111"}}""")]
    public async Task ListAsync_RoleWithPartialOrCoercibleShape_SkipsRow(string newValues)
    {
        Fixture f = CreateFixture();
        Returns(f, Row(newValues: newValues));

        AuditEntryPage page = await List(f);

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task ListAsync_TextListWithNonStringItem_SkipsRow()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(entity: "Role", action: "RoleCreated", newValues:
            """{"role":{"id":"11111111-1111-1111-1111-111111111111","name":"R"},"permissionCodes":["a",2]}"""));

        AuditEntryPage page = await List(f);

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task ListAsync_DeclaredKeyAbsent_KeyOmitted()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(newValues: """{"email":"a@x.com"}"""));

        AuditEntryPage page = await List(f);

        Assert.Equal(["email"], Assert.Single(page.Items).Parameters.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ListAsync_NullNewValuesForParameterlessType_ReturnsEntryWithEmptyParameters(string? newValues)
    {
        Fixture f = CreateFixture();
        Returns(f, Row(entity: "Organization", action: "OrganizationDeactivated", newValues: newValues));

        AuditEntryPage page = await List(f);

        AuditEntryDto entry = Assert.Single(page.Items);
        Assert.Equal(AuditEventType.OrganizationDeactivated, entry.Type);
        Assert.Empty(entry.Parameters);
    }

    [Fact]
    public async Task ListAsync_NullNewValuesForTypeWithParameters_SkipsRow()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(newValues: null));

        AuditEntryPage page = await List(f);

        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("5")]
    [InlineData("null")]
    [InlineData("{not json")]
    public async Task ListAsync_NewValuesNotJsonObject_SkipsRowAndWarns(string newValues)
    {
        Fixture f = CreateFixture();
        Returns(f, Row(4, newValues: newValues));

        AuditEntryPage page = await List(f);

        Assert.Empty(page.Items);
        Assert.Equal("Skipped audit entry 4", Assert.Single(f.Logger.Entries).Message);
    }

    // ListAsync: row identity and log

    [Fact]
    public async Task ListAsync_UnknownAction_SkipsRowAndWarnsWithIdOnly()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(42, action: "Exploded", newValues: """{"email":"x@y.z"}"""));

        AuditEntryPage page = await List(f);

        Assert.Empty(page.Items);
        (LogLevel level, string message) = Assert.Single(f.Logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Equal("Skipped audit entry 42", message);
    }

    [Fact]
    public async Task ListAsync_EntityNotMatchingAction_SkipsRow()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(entity: "Role", action: "InvitationSent"));

        AuditEntryPage page = await List(f);

        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bogus")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("4")]
    [InlineData("account")]
    [InlineData(" Account")]
    [InlineData("")]
    public async Task ListAsync_NullOrUnknownOrNumericActorKind_SkipsRowNeverAccount(string? actorKind)
    {
        Fixture f = CreateFixture();
        Returns(f, Row(7, actorKind: actorKind));

        AuditEntryPage page = await List(f);

        Assert.Empty(page.Items);
        Assert.Equal("Skipped audit entry 7", Assert.Single(f.Logger.Entries).Message);
    }

    [Fact]
    public async Task ListAsync_SystemAdministratorActor_ActorNameNullEvenIfAccountExists()
    {
        Fixture f = CreateFixture();
        // AC-368: the row carries a name because the administrator's account exists; it must not reach the client.
        Returns(f, Row(entity: "Organization", action: "OrganizationDeactivated",
            actorKind: "SystemAdministrator", actorName: "Admin", newValues: null));

        AuditEntryPage page = await List(f);

        AuditEntryDto entry = Assert.Single(page.Items);
        Assert.Equal(ActorKind.SystemAdministrator, entry.ActorKind);
        Assert.Null(entry.ActorName);
    }

    [Fact]
    public async Task ListAsync_SystemActor_ActorNameNull()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(actorKind: "System", actorName: "Someone"));

        AuditEntryPage page = await List(f);

        Assert.Equal(ActorKind.System, Assert.Single(page.Items).ActorKind);
        Assert.Null(page.Items[0].ActorName);
    }

    [Fact]
    public async Task ListAsync_AccountActor_ActorNameFromRow()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(actorKind: "Account", actorName: "An"));

        AuditEntryPage page = await List(f);

        AuditEntryDto entry = Assert.Single(page.Items);
        Assert.Equal(ActorKind.Account, entry.ActorKind);
        Assert.Equal("An", entry.ActorName);
        Assert.Equal(AuditEventType.InvitationSent, entry.Type);
        Assert.Equal(Now.AddMinutes(-1), entry.OccurredAt);
    }

    [Fact]
    public async Task ListAsync_AccountActorWithDeletedAccount_ActorNameNull()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(actorKind: "Account", actorName: null));

        AuditEntryPage page = await List(f);

        Assert.Null(Assert.Single(page.Items).ActorName);
    }

    [Fact]
    public async Task ListAsync_MappableRowsAfterSkippedRow_AreKeptInOrder()
    {
        Fixture f = CreateFixture();
        Returns(f, Row(3), Row(2, action: "Exploded"), Row(1));

        AuditEntryPage page = await List(f);

        Assert.Equal([3L, 1L], page.Items.Select(item => item.Id));
        Assert.Equal("Skipped audit entry 2", Assert.Single(f.Logger.Entries).Message);
    }
}
