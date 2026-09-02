using FoodTraceability.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Catalog.Infrastructure.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product", CatalogDbContext.Schema);

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasColumnName("product_id")
            .ValueGeneratedNever();

        builder.Property(product => product.ProductCode)
            .HasMaxLength(Product.MaximumProductCodeLength)
            .IsRequired();

        builder.Property(product => product.Name)
            .HasMaxLength(Product.MaximumNameLength)
            .IsRequired();

        builder.Property(product => product.CreatedAt)
            .IsRequired();
    }
}
