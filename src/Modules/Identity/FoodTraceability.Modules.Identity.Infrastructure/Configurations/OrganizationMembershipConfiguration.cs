using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class OrganizationMembershipConfiguration
    : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_membership", IdentityDbContext.Schema);

        builder.HasKey(membership => new
        {
            membership.UserId,
            membership.OrganizationId,
        });

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(membership => membership.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(membership => membership.CreatedAt)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The organization FK is added explicitly in the migration to preserve module isolation.
    }
}
