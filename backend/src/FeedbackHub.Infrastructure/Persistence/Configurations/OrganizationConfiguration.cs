using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Slugs created before the 50-character domain cap can be up to 100 long and must still load.
        builder.Property(organization => organization.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(organization => organization.Status)
            .IsRequired()
            .HasDefaultValue(true);

        // No foreign key: the deactivating account may later be removed; the id is only a historical pointer.
        builder.Property(organization => organization.DeactivatedBySystemAdministrator)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(organization => organization.DeactivationReason)
            .HasMaxLength(500);

        builder.HasIndex(organization => organization.Slug)
            .IsUnique();
    }
}
