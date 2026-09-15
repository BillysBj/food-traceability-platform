using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.UnitTests;

public sealed class SpecificationTests
{
    private static readonly Guid SpecificationId = Guid.Parse("5129eb12-648f-46db-80f4-690c651a2754");
    private static readonly Guid ArticleId = Guid.Parse("ca1bbfe8-8c21-4ef2-be8c-01e9d4a3c89c");
    private static readonly DateTimeOffset ValidFrom = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidSpecificationRetainsAllValues()
    {
        var validTo = ValidFrom.AddYears(1);
        var specification = Create(version: 2, validTo: validTo);

        Assert.Equal(SpecificationId, specification.Id);
        Assert.Equal(ArticleId, specification.ArticleId);
        Assert.Equal(2, specification.Version);
        Assert.Equal(ValidFrom, specification.ValidFrom);
        Assert.Equal(validTo, specification.ValidTo);
        Assert.Equal(CreatedAt, specification.CreatedAt);
    }

    [Fact]
    public void SpecificationWithoutEndDateIsAllowed()
    {
        Assert.Null(Create().ValidTo);
    }

    [Fact]
    public void EmptySpecificationIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(id: Guid.Empty));
    }

    [Fact]
    public void EmptyArticleIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(articleId: Guid.Empty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonPositiveVersionIsRejected(int version)
    {
        Assert.Throws<QualityDomainException>(() => Create(version: version));
    }

    [Fact]
    public void EndBeforeStartIsRejectedEvenWithDifferentOffsets()
    {
        var validTo = ValidFrom.AddTicks(-1).ToOffset(TimeSpan.FromHours(2));

        Assert.Throws<QualityDomainException>(() => Create(validTo: validTo));
    }

    [Fact]
    public void EndEqualToStartIsAllowed()
    {
        Assert.Equal(ValidFrom, Create(validTo: ValidFrom).ValidTo);
    }

    [Fact]
    public void AllTimestampsRetainSubMicrosecondPrecisionAndSuppliedOffsets()
    {
        var validFrom = ValidFrom.ToOffset(TimeSpan.FromHours(2)).AddTicks(17);
        var validTo = ValidFrom.AddYears(1).ToOffset(TimeSpan.FromHours(-3)).AddTicks(29);
        var createdAt = CreatedAt.ToOffset(TimeSpan.FromHours(4)).AddTicks(31);

        var specification = Specification.Create(SpecificationId, ArticleId, 1, validFrom, validTo, createdAt);

        Assert.True(validFrom.EqualsExact(specification.ValidFrom));
        Assert.True(validTo.EqualsExact(Assert.IsType<DateTimeOffset>(specification.ValidTo)));
        Assert.True(createdAt.EqualsExact(specification.CreatedAt));
    }

    private static Specification Create(
        Guid? id = null, Guid? articleId = null, int version = 1, DateTimeOffset? validTo = null) =>
        Specification.Create(id ?? SpecificationId, articleId ?? ArticleId, version, ValidFrom, validTo, CreatedAt);
}
