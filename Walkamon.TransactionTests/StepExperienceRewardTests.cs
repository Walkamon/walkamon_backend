using BLL.Exceptions;
using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class StepExperienceRewardTests
{
    [Theory]
    [InlineData(99, 1, 1)]
    public void CalculateRewardsCrossed_OnlyAwardsNewHundredStepMilestones(
        long previousSteps,
        int newSteps,
        int expectedRewards)
    {
        Assert.Equal(
            expectedRewards,
            StepExperienceReward.CalculateRewardsCrossed(previousSteps, newSteps));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("-1")]
    public void ParseExpPerReward_RejectsMissingOrInvalidConfiguration(string? value)
    {
        Assert.Throws<AppSystemException>(() => StepExperienceReward.ParseExpPerReward(value));
    }

    [Fact]
    public void ApplyExperience_HandlesMultipleLevelUpsAndCarriesRemainingExp()
    {
        var updatedAt = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Unspecified);
        var userPet = new UserPet
        {
            Level = 1,
            CurrentPetExp = 450,
            PetExp = 500,
            PetEnergy = 100,
            PetBond = 100,
            PetLifeForce = 100,
            CurrentPetEnergy = 50,
            CurrentPetBond = 50,
            CurrentPetLifeForce = 50
        };
        var pet = new Pet
        {
            ExpRate = 1.2,
            EnergyRate = 1.2,
            BondRate = 1.2,
            LifeForceRate = 1.2
        };

        StepExperienceReward.ApplyExperience(userPet, pet, 700, updatedAt);

        Assert.Equal(3, userPet.Level);
        Assert.Equal(50, userPet.CurrentPetExp);
        Assert.Equal(720, userPet.PetExp);
        Assert.Equal(144, userPet.PetEnergy);
        Assert.Equal(userPet.PetEnergy, userPet.CurrentPetEnergy);
        Assert.Equal(updatedAt, userPet.ExpUpdatedAt);
    }
}
