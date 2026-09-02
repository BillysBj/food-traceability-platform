using FoodTraceability.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Catalog.Infrastructure.Configurations;

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("article", CatalogDbContext.Schema, tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_article_gtin_format",
                "gtin IS NULL OR (gtin ~ '^[0-9]+$' AND length(gtin) IN (8, 12, 13, 14))");
        });

        builder.HasKey(article => article.Id);

        builder.Property(article => article.Id)
            .HasColumnName("article_id")
            .ValueGeneratedNever();

        builder.Property(article => article.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(article => article.ProductId)
            .HasColumnName("product_id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(article => article.ArticleNumber)
            .HasMaxLength(Article.MaximumArticleNumberLength)
            .IsRequired();

        builder.Property(article => article.Gtin)
            .HasMaxLength(Article.MaximumGtinLength);

        builder.Property(article => article.CreatedAt)
            .IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(article => article.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasAlternateKey(article => new { article.Id, article.OrganizationId });

        builder.HasIndex(article => new { article.OrganizationId, article.Gtin })
            .IsUnique();
    }
}
