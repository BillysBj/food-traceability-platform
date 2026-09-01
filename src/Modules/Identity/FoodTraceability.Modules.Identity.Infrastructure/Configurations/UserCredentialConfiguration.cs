using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credential", IdentityDbContext.Schema);

        builder.HasKey(credential => credential.UserId);

        builder.Property(credential => credential.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(credential => credential.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(credential => credential.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(credential => credential.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserCredential>(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
