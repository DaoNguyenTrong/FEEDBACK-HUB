using Microsoft.EntityFrameworkCore;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

// Commits rows on purpose (raw insert of a legacy slug, reload in a fresh context); each test has its own organization.
[Collection(nameof(PostgresCollection))]
public sealed class OrganizationPersistenceTests(PostgresContainerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveAndLoad_DeactivationFields_RoundTrip()
    {
        Organization organization = Organization.Create(new OrganizationParams("Round trip", $"trip-{Guid.NewGuid():N}"));
        Guid actor = Guid.NewGuid();
        organization.Deactivate(new DeactivationParams(actor, true, "Hết hợp đồng", Now));
        await using (AppDbContext writer = fixture.CreateDbContext())
        {
            writer.Organizations.Add(organization);
            await writer.SaveChangesAsync();
        }

        await using AppDbContext reader = fixture.CreateDbContext();
        Organization stored = await reader.Organizations.AsNoTracking().SingleAsync(o => o.Id == organization.Id);

        Assert.False(stored.Status);
        Assert.Equal(Now, stored.DeactivatedAt);
        Assert.Equal(actor, stored.DeactivatedByAccountId);
        Assert.True(stored.DeactivatedBySystemAdministrator);
        Assert.Equal("Hết hợp đồng", stored.DeactivationReason);
    }

    [Fact]
    public async Task Rename_LegacySlugOutsideFormat_SavesWithoutRecheck()
    {
        Guid id = Guid.NewGuid();
        await using (AppDbContext raw = fixture.CreateDbContext())
        {
            await raw.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO organizations ("Id", "Name", "Slug", "Status", "CreatedAt") VALUES ({id}, {"Legacy"}, {"Old_Slug"}, TRUE, {Now})""");
        }

        await using (AppDbContext context = fixture.CreateDbContext())
        {
            Organization loaded = await context.Organizations.SingleAsync(o => o.Id == id);
            loaded.Update(new OrganizationNameParams("Renamed"));
            await context.SaveChangesAsync();
        }

        await using AppDbContext reader = fixture.CreateDbContext();
        Organization stored = await reader.Organizations.AsNoTracking().SingleAsync(o => o.Id == id);
        Assert.Equal("Renamed", stored.Name);
        Assert.Equal("Old_Slug", stored.Slug);
    }
}
