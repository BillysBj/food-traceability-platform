namespace FoodTraceability.Modules.Catalog.Application.Articles;

public sealed class ArticleValidationException : Exception
{
    public ArticleValidationException(string message)
        : base(message)
    {
    }
}

public sealed class ArticleConflictException : Exception
{
    public ArticleConflictException(ArticleConflictField field, string message)
        : base(message)
    {
        Field = field;
    }

    public ArticleConflictField Field { get; }
}
