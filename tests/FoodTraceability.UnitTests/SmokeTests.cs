namespace FoodTraceability.UnitTests;

public sealed class SmokeTests
{
    [Fact]
    public void TestProjectIsConfigured()
    {
        Assert.NotNull(typeof(SmokeTests).Assembly);
    }
}
