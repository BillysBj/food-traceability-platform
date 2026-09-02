using FoodTraceability.Modules.Catalog.Application.Articles;

namespace FoodTraceability.UnitTests;

public sealed class ArticleQueryServiceTests
{
    [Fact]
    public async Task FindPassesBothOrganizationAndArticleIdentifiersToReader()
    {
        var organizationId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        var reader = new CapturingArticleReader();
        var service = new ArticleQueryService(reader);

        await service.FindByIdAsync(organizationId, articleId, CancellationToken.None);

        Assert.Equal(organizationId, reader.OrganizationId);
        Assert.Equal(articleId, reader.ArticleId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task EmptyIdentifierDoesNotReachReader(
        bool emptyOrganizationId,
        bool emptyArticleId)
    {
        var reader = new CapturingArticleReader();
        var service = new ArticleQueryService(reader);

        var result = await service.FindByIdAsync(
            emptyOrganizationId ? Guid.Empty : Guid.NewGuid(),
            emptyArticleId ? Guid.Empty : Guid.NewGuid(),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Null(reader.OrganizationId);
        Assert.Null(reader.ArticleId);
    }

    private sealed class CapturingArticleReader : IArticleReader
    {
        public Guid? OrganizationId { get; private set; }

        public Guid? ArticleId { get; private set; }

        public Task<ArticleDetails?> FindByIdAsync(
            Guid organizationId,
            Guid articleId,
            CancellationToken cancellationToken)
        {
            OrganizationId = organizationId;
            ArticleId = articleId;
            return Task.FromResult<ArticleDetails?>(null);
        }

        public Task<bool> ProductExistsAsync(
            Guid productId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }
}
