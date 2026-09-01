using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable(
            "refresh_token",
            IdentityDbContext.Schema,
            table =>
            {
                table.HasCheckConstraint(
                    "ck_refresh_token_expires_after_issued",
                    "expires_at > issued_at");
                table.HasCheckConstraint(
                    "ck_refresh_token_revoked_not_before_issued",
                    "revoked_at IS NULL OR revoked_at >= issued_at");
            });

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(token => token.SessionId)
            .HasColumnName("session_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .IsRequired();

        builder.Property(token => token.IssuedAt)
            .HasColumnName("issued_at")
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(token => token.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.HasIndex(token => token.UserId);

        builder.HasIndex(token => token.SessionId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
