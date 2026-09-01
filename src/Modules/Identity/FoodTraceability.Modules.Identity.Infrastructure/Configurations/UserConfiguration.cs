using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Identity.Infrastructure.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    // Persisted values pass through the domain factory again; invalid database data fails materialization.
    private static readonly ValueConverter<EmailAddress, string> EmailAddressConverter = new(
        emailAddress => emailAddress.Value,
        value => EmailAddress.Create(value));

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user", IdentityDbContext.Schema);

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasConversion(EmailAddressConverter)
            .HasMaxLength(EmailAddress.MaximumLength)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.FirstName)
            .HasMaxLength(User.MaximumNameLength)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasMaxLength(User.MaximumNameLength)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .IsRequired();
    }
}
