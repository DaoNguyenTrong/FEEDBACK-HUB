using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.EntityName)
            .HasColumnName("entity_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.OldValues)
            .HasColumnName("old_values")
            .HasColumnType("jsonb");

        builder.Property(x => x.NewValues)
            .HasColumnName("new_values")
            .HasColumnType("jsonb");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50);

        builder.Property(x => x.OrganizationId)
            .HasColumnName("organization_id");

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(50);

        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(x => x.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        // Sentinel 0 is not an enum member, so the default only applies to raw inserts that omit the column.
        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(10)
            .HasDefaultValue(AuditLogKind.Change)
            .IsRequired();

        builder.Property(x => x.ActorKind)
            .HasColumnName("actor_kind")
            .HasMaxLength(20);

        builder.HasIndex(x => x.EntityName);
        builder.HasIndex(x => x.EntityId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => x.Timestamp);

        // Keyset paging of the organization's event feed (TECH00 §3.7); Change rows stay out of it.
        builder.HasIndex(x => new { x.OrganizationId, x.Timestamp, x.Id })
            .IsDescending(false, true, true)
            .HasFilter("kind = 'Event'")
            .HasDatabaseName("IX_audit_logs_event_keyset");
    }
}
