using FoodTraceability.Modules.Catalog.Application.Articles;
using FoodTraceability.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Catalog.Infrastructure.Articles;

internal sealed class ArticleWriter(CatalogDbContext dbContext) : IArticleWriter
{
    private const string ArticleNumberUniqueIndex =
        "ux_article_organization_id_article_number_upper";
    private const string GtinUniqueIndex = "ix_article_organization_id_gtin";

    public async Task AddAsync(Article article, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(article);

        dbContext.Articles.Add(article);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (TryGetConflictField(exception, out var conflictField))
        {
            throw CreateConflictException(conflictField);
        }
    }

    private static bool TryGetConflictField(
        DbUpdateException exception,
        out ArticleConflictField conflictField)
    {
        if (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ArticleNumberUniqueIndex
            })
        {
            conflictField = ArticleConflictField.ArticleNumber;
            return true;
        }

        if (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: GtinUniqueIndex
            })
        {
            conflictField = ArticleConflictField.Gtin;
            return true;
        }

        conflictField = default;
        return false;
    }

    private static ArticleConflictException CreateConflictException(
        ArticleConflictField conflictField)
    {
        var message = conflictField == ArticleConflictField.ArticleNumber
            ? "An article with the same article number already exists in this organization."
            : "An article with the same GTIN already exists in this organization.";
        return new ArticleConflictException(conflictField, message);
    }
}
