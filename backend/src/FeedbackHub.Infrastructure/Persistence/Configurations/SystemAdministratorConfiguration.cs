using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Configurations;

public sealed class SystemAdministratorConfiguration : IEntityTypeConfiguration<SystemAdministrator>
{
    public void Configure(EntityTypeBuilder<SystemAdministrator> builder)
    {
        builder.ToTable("system_administrators");

        builder.HasKey(administrator => administrator.Id);

        builder.HasIndex(administrator => administrator.AccountId)
            .IsUnique();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(administrator => administrator.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
