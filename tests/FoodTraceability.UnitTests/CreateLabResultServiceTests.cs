using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CreateLabResultServiceTests
{
    [Fact]
    public async Task UsesTimeProviderAndTruncatesMeasurementTimeBeforeWriting()
    {
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        var sample = Sample.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "RESULT-SAMPLE", now, now);
        var writer = new StubWriter(sample);
        var service = new CreateLabResultService(writer, new FixedTimeProvider(now));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var expectedTime = now.AddTicks(1234560);
        var command = new CreateLabResultCommand(sample.OrganizationId, sample.Id, Guid.NewGuid(),
            0.5m, LabResultAssessment.Fail, "ISO 660", expectedTime.AddTicks(7));

        var result = await service.CreateAsync(command, timeout.Token);

        Assert.NotNull(writer.Result);
        Assert.Equal(now, writer.Result.CreatedAt);
        Assert.Equal(expectedTime, writer.Result.MeasuredAt);
        Assert.Equal(expectedTime, result.MeasuredAt);
        Assert.Equal(SampleStatus.Fail, writer.StatusAtWrite);
        Assert.Equal(SampleStatus.Fail, result.SampleStatus);
        Assert.Equal(timeout.Token, writer.ReadToken);
        Assert.Equal(timeout.Token, writer.WriteToken);
    }

    [Fact]
    public async Task MissingSampleStopsBeforeValidationOrWriting()
    {
        var writer = new StubWriter(null);
        var service = new CreateLabResultService(writer, TimeProvider.System);
        var command = new CreateLabResultCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            0m, LabResultAssessment.Pass, null, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<LabResultSampleNotFoundException>(
            () => service.CreateAsync(command, CancellationToken.None));

        Assert.Equal(command.OrganizationId, writer.OrganizationId);
        Assert.Equal(command.SampleId, writer.SampleId);
        Assert.Null(writer.Result);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubWriter(Sample? sample) : ILabResultWriter
    {
        public Guid OrganizationId { get; private set; }
        public Guid SampleId { get; private set; }
        public CancellationToken ReadToken { get; private set; }
        public CancellationToken WriteToken { get; private set; }
        public LabResult? Result { get; private set; }
        public SampleStatus? StatusAtWrite { get; private set; }

        public Task<Sample?> FindSampleAsync(Guid organizationId, Guid sampleId, CancellationToken cancellationToken)
        {
            OrganizationId = organizationId;
            SampleId = sampleId;
            ReadToken = cancellationToken;
            return Task.FromResult(sample);
        }

        public Task AddAsync(LabResult result, CancellationToken cancellationToken)
        {
            Result = result;
            StatusAtWrite = sample?.Status;
            WriteToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
