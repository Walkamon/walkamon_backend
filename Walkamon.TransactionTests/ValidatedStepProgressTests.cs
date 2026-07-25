using BLL.Interfaces;
using BLL.Service;
using Moq;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class ValidatedStepProgressTests
{
    [Fact]
    public async Task AcceptedSteps_AdvanceStepAndLevelProgress()
    {
        var userId = Guid.NewGuid();
        var achievements = new Mock<IAchievementProgressService>(MockBehavior.Strict);
        var missions = new Mock<IMissionProgressService>(MockBehavior.Strict);
        achievements
            .Setup(x => x.AddProgressAsync(userId, MissionMetricCodeCatalog.Steps, 125))
            .Returns(Task.CompletedTask);
        missions
            .Setup(x => x.AddProgressAsync(userId, MissionMetricCodeCatalog.Steps, 125))
            .Returns(Task.CompletedTask);
        achievements
            .Setup(x => x.SetProgressMaxAsync(userId, MissionMetricCodeCatalog.PetLevel, 5))
            .Returns(Task.CompletedTask);
        missions
            .Setup(x => x.SetProgressMaxAsync(userId, MissionMetricCodeCatalog.PetLevel, 5))
            .Returns(Task.CompletedTask);

        await ValidatedStepService.SyncAcceptedProgressAsync(
            userId,
            125,
            5,
            achievements.Object,
            missions.Object);

        achievements.VerifyAll();
        missions.VerifyAll();
    }

    [Fact]
    public async Task AcceptedSteps_WithoutLevelUp_OnlyAdvanceStepProgress()
    {
        var userId = Guid.NewGuid();
        var achievements = new Mock<IAchievementProgressService>(MockBehavior.Strict);
        var missions = new Mock<IMissionProgressService>(MockBehavior.Strict);
        achievements
            .Setup(x => x.AddProgressAsync(userId, MissionMetricCodeCatalog.Steps, 20))
            .Returns(Task.CompletedTask);
        missions
            .Setup(x => x.AddProgressAsync(userId, MissionMetricCodeCatalog.Steps, 20))
            .Returns(Task.CompletedTask);

        await ValidatedStepService.SyncAcceptedProgressAsync(
            userId,
            20,
            null,
            achievements.Object,
            missions.Object);

        achievements.VerifyAll();
        missions.VerifyAll();
    }

    [Fact]
    public async Task RejectedOrDuplicateBatch_DoesNotAdvanceProgress()
    {
        var achievements = new Mock<IAchievementProgressService>(MockBehavior.Strict);
        var missions = new Mock<IMissionProgressService>(MockBehavior.Strict);

        await ValidatedStepService.SyncAcceptedProgressAsync(
            Guid.NewGuid(),
            0,
            null,
            achievements.Object,
            missions.Object);

        achievements.VerifyNoOtherCalls();
        missions.VerifyNoOtherCalls();
    }
}
