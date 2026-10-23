using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

// Commits rows on purpose (rollback and raw-insert checks need real commits); every test uses its own organization.
[Collection(nameof(PostgresCollection))]
public sealed class AuditTrailTests(PostgresContainerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    private static Organization NewOrganization() =>
        Organization.Create(new OrganizationParams("Audit trail org", $"audit-{Guid.NewGuid():N}"));

    private static IDateTimeProvider Clock()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    private AppDbContext CreateContext(IHttpContextAccessor? accessor = null)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        return new AppDbContext(options, Clock(), null, accessor);
    }

    private async Task<List<AuditLog>> EventsOfAsync(Guid organizationId)
    {
        await using AppDbContext reader = fixture.CreateDbContext();
        return await reader.AuditLogs
            .Where(log => log.OrganizationId == organizationId && log.Kind == AuditLogKind.Event)
            .ToListAsync();
    }

    [Fact]
    public async Task RecordAsync_ThenSaveChanges_WritesOneEventRowFromTheEvent()
    {
        DefaultHttpContext http = new();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.9");
        http.Request.Headers.UserAgent = "TestAgent/1.0";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(http);
        Guid otherOrganization = Guid.NewGuid();
        Guid actor = Guid.NewGuid();
        Organization organization = NewOrganization();

        await using (AppDbContext context = CreateContext(accessor))
        {
            context.Organizations.Add(organization);
            AuditTrail trail = new(context, Clock());
            await trail.RecordAsync(
                AuditEvent.For(organization, AuditEventType.OrganizationDeactivated, ActorKind.Account, actor),
                CancellationToken.None);
            await context.SaveChangesAsync();
        }

        AuditLog row = Assert.Single(await EventsOfAsync(organization.Id));
        Assert.NotEqual(otherOrganization, row.OrganizationId);
        Assert.Equal(actor.ToString(), row.UserId);
        Assert.Equal("Account", row.ActorKind);
        Assert.Equal("OrganizationDeactivated", row.Action);
        Assert.Equal("Organization", row.EntityName);
        Assert.Equal(organization.Id.ToString(), row.EntityId);
        Assert.Null(row.IpAddress);
        Assert.Null(row.UserAgent);
        Assert.Null(row.OldValues);
        Assert.Null(row.NewValues);
        Assert.Equal(Now, row.Timestamp);
    }

    [Fact]
    public async Task RecordAsync_StoredRow_RoundTripsThroughTryResolve()
    {
        Organization organization = NewOrganization();
        await using (AppDbContext context = CreateContext())
        {
            context.Organizations.Add(organization);
            await new AuditTrail(context, Clock()).RecordAsync(
                AuditEvent.For(organization, AuditEventType.OrganizationReactivated, ActorKind.System, null),
                CancellationToken.None);
            await context.SaveChangesAsync();
        }

        AuditLog row = Assert.Single(await EventsOfAsync(organization.Id));
        Assert.True(AuditEventCatalog.TryResolve(row.EntityName, row.Action, out AuditEventType type));
        Assert.Equal(AuditEventType.OrganizationReactivated, type);
        Assert.Equal("System", row.ActorKind);
        Assert.Null(row.UserId);
    }

    [Fact]
    public async Task RecordAsync_RoleParameters_SerializesSystemKind()
    {
        Organization organization = NewOrganization();
        Role owner = Role.Create(new RoleParams(organization.Id, "Owner", SystemRoleKind.Owner));
        await using (AppDbContext context = CreateContext())
        {
            context.Organizations.Add(organization);
            context.Roles.Add(owner);
            await new AuditTrail(context, Clock()).RecordAsync(
                AuditEvent.For(organization, AuditEventType.RoleDeleted, ActorKind.Account, Guid.NewGuid(),
                    new Dictionary<string, object> { ["role"] = AuditRoleRef.From(owner) }),
                CancellationToken.None);
            await context.SaveChangesAsync();
        }

        AuditLog row = Assert.Single(await EventsOfAsync(organization.Id));
        using JsonDocument json = JsonDocument.Parse(row.NewValues!);
        JsonElement role = json.RootElement.GetProperty("role");
        Assert.Equal(owner.Id, role.GetProperty("id").GetGuid());
        Assert.Equal("Owner", role.GetProperty("name").GetString());
        Assert.Equal("Owner", role.GetProperty("systemKind").GetString());
    }

    [Fact]
    public async Task RecordAsync_CustomRole_SerializesNullSystemKind()
    {
        Organization organization = NewOrganization();
        Role custom = Role.Create(new RoleParams(organization.Id, "Reviewers"));
        await using (AppDbContext context = CreateContext())
        {
            context.Organizations.Add(organization);
            context.Roles.Add(custom);
            await new AuditTrail(context, Clock()).RecordAsync(
                AuditEvent.For(organization, AuditEventType.RoleDeleted, ActorKind.Account, Guid.NewGuid(),
                    new Dictionary<string, object> { ["role"] = AuditRoleRef.From(custom) }),
                CancellationToken.None);
            await context.SaveChangesAsync();
        }

        AuditLog row = Assert.Single(await EventsOfAsync(organization.Id));
        using JsonDocument json = JsonDocument.Parse(row.NewValues!);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("role").GetProperty("systemKind").ValueKind);
    }

    [Fact]
    public async Task RecordAsync_WithoutSaveChanges_WritesNothing()
    {
        Organization organization = NewOrganization();
        await using (AppDbContext context = CreateContext())
        {
            context.Organizations.Add(organization);
            await context.SaveChangesAsync();
            await new AuditTrail(context, Clock()).RecordAsync(
                AuditEvent.For(organization, AuditEventType.OrganizationDeactivated, ActorKind.Account, Guid.NewGuid()),
                CancellationToken.None);
        }

        Assert.Empty(await EventsOfAsync(organization.Id));
    }

    // AC-337: a rolled back change leaves no event behind.
    [Fact]
    public async Task RecordAsync_TransactionRolledBack_WritesNothing()
    {
        Organization organization = NewOrganization();
        await using AppDbContext context = CreateContext();
        UnitOfWork unitOfWork = new(context, null!);
        AuditTrail trail = new(context, Clock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            context.Organizations.Add(organization);
            await trail.RecordAsync(
                AuditEvent.For(organization, AuditEventType.OrganizationDeactivated, ActorKind.Account, Guid.NewGuid()),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            throw new InvalidOperationException("later step failed");
        }));

        Assert.Empty(await EventsOfAsync(organization.Id));
    }

    [Fact]
    public async Task RecordAsync_InvalidEvent_ThrowsAndStagesNothing()
    {
        Organization organization = NewOrganization();
        await using AppDbContext context = CreateContext();
        AuditTrail trail = new(context, Clock());
        AuditEvent invalid = AuditEvent.For(
            organization, AuditEventType.OrganizationDeactivated, ActorKind.Account, Guid.NewGuid(),
            new Dictionary<string, object> { ["unexpected"] = "x" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => trail.RecordAsync(invalid, CancellationToken.None));

        Assert.DoesNotContain(context.ChangeTracker.Entries(), entry => entry.Entity is AuditLog);
    }

    [Fact]
    public async Task SaveChanges_SnapshotOfApiKey_IsKindChange()
    {
        Organization organization = NewOrganization();
        ApiKey key = ApiKey.Create(new ApiKeyParams("Snapshot key"), "pfx", "hash", organization.Id);
        await using (AppDbContext context = CreateContext())
        {
            context.Organizations.Add(organization);
            context.ApiKeys.Add(key);
            await context.SaveChangesAsync();
        }

        await using AppDbContext reader = fixture.CreateDbContext();
        AuditLog snapshot = await reader.AuditLogs.SingleAsync(log =>
            log.OrganizationId == organization.Id && log.EntityName == nameof(ApiKey));
        Assert.Equal(AuditLogKind.Change, snapshot.Kind);
        Assert.Null(snapshot.ActorKind);
    }

    [Fact]
    public async Task Migration_RowInsertedWithoutKind_DefaultsToChange()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        string entityId = Guid.NewGuid().ToString();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO audit_logs (entity_name, entity_id, action, \"timestamp\") VALUES ('Raw', {0}, 'Added', now())",
            entityId);

        string kind = await context.Database
            .SqlQueryRaw<string>("SELECT kind AS \"Value\" FROM audit_logs WHERE entity_id = {0}", entityId)
            .SingleAsync();
        Assert.Equal("Change", kind);
    }

    [Fact]
    public async Task Migration_EventKeysetIndex_IsPartialOnEventRows()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        string definition = await context.Database
            .SqlQueryRaw<string>(
                "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'IX_audit_logs_event_keyset'")
            .SingleAsync();
        Assert.Contains("organization_id, \"timestamp\" DESC, id DESC", definition);
        Assert.Contains("WHERE", definition);
        Assert.Contains("Event", definition);
    }

    [Fact]
    public void AuditTrail_Constructor_TakesNoTenantOrUserContext()
    {
        Type[] parameters = [.. typeof(AuditTrail).GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];
        Assert.Equal([typeof(AppDbContext), typeof(IDateTimeProvider)], parameters);
    }
}
