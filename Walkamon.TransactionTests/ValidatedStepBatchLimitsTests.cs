using BLL.Exceptions;
using BLL.Interfaces;
using BLL.Options;
using BLL.Service;
using DAL.DTO;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class ValidatedStepBatchLimitsTests
{
    [Fact]
    public async Task SubmitDailyBatch_RejectsMoreThanConfiguredEventsBeforeOpeningTransaction()
    {
        var service = CreateService(new StepValidationOptions { MaxBatchEvents = 2 });
        var request = new SubmitPvpStepBatchRequest
        {
            Events = [Event(), Event(), Event()]
        };

        var error = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SubmitDailyBatchAsync(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.Contains("between 1 and 2 events", error.Message);
    }

    [Fact]
    public async Task SubmitDailyBatch_RejectsMoreThanConfiguredMotionWindowsBeforeOpeningTransaction()
    {
        var service = CreateService(new StepValidationOptions { MaxBatchMotionWindows = 1 });
        var request = new SubmitPvpStepBatchRequest
        {
            Events = [Event()],
            MotionWindows = [Window(), Window()]
        };

        var error = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SubmitDailyBatchAsync(Guid.NewGuid(), Guid.NewGuid(), request));

        Assert.Contains("more than 1 motion windows", error.Message);
    }

    private static ValidatedStepService CreateService(StepValidationOptions options) =>
        new(
            context: null!,
            attestationVerifier: new Mock<IAppAttestationVerifier>().Object,
            achievementProgressService: new Mock<IAchievementProgressService>().Object,
            missionProgressService: new Mock<IMissionProgressService>().Object,
            options: Options.Create(options),
            motionOptions: Options.Create(new MotionValidationOptions()));

    private static PvpStepEventRequest Event() => new()
    {
        IntervalStartedAt = DateTime.UtcNow.AddSeconds(-1),
        RecordedAt = DateTime.UtcNow,
        StepCount = 1
    };

    private static StepMotionWindowRequest Window() => new()
    {
        WindowStartedAt = DateTime.UtcNow.AddSeconds(-1),
        WindowEndedAt = DateTime.UtcNow,
        SampleCount = 25,
        AccelerometerSource = "linear",
        ActivityCode = "walking",
        ActivityConfidence = 80
    };
}
