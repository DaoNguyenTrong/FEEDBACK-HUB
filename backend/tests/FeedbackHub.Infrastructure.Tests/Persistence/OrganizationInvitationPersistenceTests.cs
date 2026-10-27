using Microsoft.EntityFrameworkCore;
using Npgsql;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

// Commits rows on purpose (cascades and unique indexes need real commits); every test uses its own organization.
[Collection(nameof(PostgresCollection))]
public sealed class OrganizationInvitationPersistenceTests(PostgresContainerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);

    private sealed record Seeded(Guid OrganizationId, Guid RoleId);

    private static string NewHash() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private static OrganizationInvitation NewInvitation(Guid organizationId, Guid roleId, string email) =>
        OrganizationInvitation.Create(new OrganizationInvitationParams(organizationId, email, roleId, NewHash(), Now));

    private async Task<Seeded> SeedAsync()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        Organization organization = Organization.Create(
            new OrganizationParams("Lời mời", $"inv-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        Role role = Role.Create(new RoleParams(organization.Id, "Tester"));
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return new Seeded(organization.Id, role.Id);
    }

    [Fact]
    public async Task SaveChanges_SecondInvitationSameOrganizationAndEmail_ViolatesUniqueIndex()
    {
        Seeded seeded = await SeedAsync();
        await using (AppDbContext first = fixture.CreateDbContext())
        {
            first.OrganizationInvitations.Add(NewInvitation(seeded.OrganizationId, seeded.RoleId, "dup@example.com"));
            await first.SaveChangesAsync();
        }

        await using AppDbContext second = fixture.CreateDbContext();
        second.OrganizationInvitations.Add(NewInvitation(seeded.OrganizationId, seeded.RoleId, "dup@example.com"));

        DbUpdateException thrown = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        PostgresException violation = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal("23505", violation.SqlState);
        Assert.Equal("IX_organization_invitations_OrganizationId_Email", violation.ConstraintName);
    }

    [Fact]
    public async Task SaveChanges_SameTokenHashInTwoOrganizations_ViolatesUniqueIndex()
    {
        Seeded one = await SeedAsync();
        Seeded two = await SeedAsync();
        string hash = NewHash();
        await using (AppDbContext first = fixture.CreateDbContext())
        {
            first.OrganizationInvitations.Add(OrganizationInvitation.Create(
                new OrganizationInvitationParams(one.OrganizationId, "a@example.com", one.RoleId, hash, Now)));
            await first.SaveChangesAsync();
        }

        await using AppDbContext second = fixture.CreateDbContext();
        second.OrganizationInvitations.Add(OrganizationInvitation.Create(
            new OrganizationInvitationParams(two.OrganizationId, "b@example.com", two.RoleId, hash, Now)));

        DbUpdateException thrown = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        PostgresException violation = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal("23505", violation.SqlState);
        Assert.Equal("IX_organization_invitations_TokenHash", violation.ConstraintName);
    }

    [Fact]
    public async Task SaveChanges_SameEmailInTwoOrganizations_Succeeds()
    {
        Seeded one = await SeedAsync();
        Seeded two = await SeedAsync();
        await using AppDbContext context = fixture.CreateDbContext();
        context.OrganizationInvitations.AddRange(
            NewInvitation(one.OrganizationId, one.RoleId, "shared@example.com"),
            NewInvitation(two.OrganizationId, two.RoleId, "shared@example.com"));

        await context.SaveChangesAsync();

        Assert.Equal(1, await context.OrganizationInvitations.CountAsync(i => i.OrganizationId == one.OrganizationId));
        Assert.Equal(1, await context.OrganizationInvitations.CountAsync(i => i.OrganizationId == two.OrganizationId));
    }

    // How API-11 replaces an invitation: EF must run the delete before the insert for the unique index.
    [Fact]
    public async Task SaveChanges_DeleteOldAndAddNewSameEmailInOneSave_Succeeds()
    {
        Seeded seeded = await SeedAsync();
        OrganizationInvitation old = NewInvitation(seeded.OrganizationId, seeded.RoleId, "swap@example.com");
        await using (AppDbContext setup = fixture.CreateDbContext())
        {
            setup.OrganizationInvitations.Add(old);
            await setup.SaveChangesAsync();
        }

        OrganizationInvitation replacement = NewInvitation(seeded.OrganizationId, seeded.RoleId, "swap@example.com");
        await using (AppDbContext context = fixture.CreateDbContext())
        {
            OrganizationInvitation tracked = await context.OrganizationInvitations.SingleAsync(i => i.Id == old.Id);
            context.OrganizationInvitations.Remove(tracked);
            context.OrganizationInvitations.Add(replacement);
            await context.SaveChangesAsync();
        }

        await using AppDbContext reader = fixture.CreateDbContext();
        List<Guid> ids = await reader.OrganizationInvitations
            .Where(i => i.OrganizationId == seeded.OrganizationId).Select(i => i.Id).ToListAsync();
        Assert.Equal([replacement.Id], ids);
    }

    // BR-63 point 5, AC-181: deleting a role removes its invitations; the link then reads "no longer valid".
    [Fact]
    public async Task DeleteRole_CascadesToItsInvitations()
    {
        Seeded seeded = await SeedAsync();
        await using (AppDbContext setup = fixture.CreateDbContext())
        {
            setup.OrganizationInvitations.Add(NewInvitation(seeded.OrganizationId, seeded.RoleId, "role@example.com"));
            await setup.SaveChangesAsync();
        }

        await using AppDbContext context = fixture.CreateDbContext();
        await context.Roles.Where(role => role.Id == seeded.RoleId).ExecuteDeleteAsync();

        Assert.False(await context.OrganizationInvitations.AnyAsync(i => i.OrganizationId == seeded.OrganizationId));
    }

    [Fact]
    public async Task DeleteOrganization_CascadesToItsInvitations()
    {
        Seeded seeded = await SeedAsync();
        await using (AppDbContext setup = fixture.CreateDbContext())
        {
            setup.OrganizationInvitations.Add(NewInvitation(seeded.OrganizationId, seeded.RoleId, "org@example.com"));
            await setup.SaveChangesAsync();
        }

        await using AppDbContext context = fixture.CreateDbContext();
        await context.Organizations.Where(org => org.Id == seeded.OrganizationId).ExecuteDeleteAsync();

        Assert.False(await context.OrganizationInvitations.AnyAsync(i => i.OrganizationId == seeded.OrganizationId));
    }

    [Fact]
    public async Task SaveChanges_Invitation_WritesNoAuditRow()
    {
        Seeded seeded = await SeedAsync();
        OrganizationInvitation invitation = NewInvitation(seeded.OrganizationId, seeded.RoleId, "audit@example.com");

        await using (AppDbContext context = fixture.CreateDbContext())
        {
            context.OrganizationInvitations.Add(invitation);
            await context.SaveChangesAsync();
            OrganizationInvitation tracked = await context.OrganizationInvitations.SingleAsync(i => i.Id == invitation.Id);
            context.OrganizationInvitations.Remove(tracked);
            await context.SaveChangesAsync();
        }

        await using AppDbContext reader = fixture.CreateDbContext();
        List<AuditLog> logs = await reader.AuditLogs.Where(log => log.OrganizationId == seeded.OrganizationId).ToListAsync();
        Assert.NotEmpty(logs);
        Assert.DoesNotContain(logs, log => log.EntityName == nameof(OrganizationInvitation));
        Assert.DoesNotContain(logs, log =>
            (log.NewValues ?? "").Contains(invitation.TokenHash) || (log.OldValues ?? "").Contains(invitation.TokenHash));
    }
}
