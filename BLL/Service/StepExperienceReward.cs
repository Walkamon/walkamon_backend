using BLL.Exceptions;
using DAL.Models;

namespace BLL.Service;

internal static class StepExperienceReward
{
    internal const int StepsPerReward = 10;

    private const double LowLifeForceExpMultiplier = 0.80;
    private const double HighBondExpMultiplier = 1.10;
    private const int LowLifeForceThreshold = 20;
    private const int HighBondThreshold = 50;

    internal static int ParseExpPerReward(string? configuredValue)
    {
        if (!int.TryParse(configuredValue, out var expPerReward) || expPerReward <= 0)
            throw new AppSystemException("Step-to-exp rate is not configured correctly.");

        return expPerReward;
    }

    internal static int CalculateRewardsCrossed(long previousValidatedSteps, int newlyValidatedSteps)
    {
        if (previousValidatedSteps < 0 || newlyValidatedSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(newlyValidatedSteps));

        var previousRewards = previousValidatedSteps / StepsPerReward;
        var currentRewards = checked(previousValidatedSteps + newlyValidatedSteps) / StepsPerReward;
        return checked((int)(currentRewards - previousRewards));
    }

    internal static int CalculateAdjustedExperience(
        UserPet userPet,
        int baseExp)
    {
        if (baseExp <= 0)
            return 0;

        if (userPet.PetLifeForce <= 0)
            throw new AppSystemException(
                "Pet LifeForce is not configured correctly.");

        if (userPet.PetBond < 0)
            throw new AppSystemException(
                "Pet Bond is not configured correctly.");

        var multiplier = 1.0;
        if (userPet.CurrentPetLifeForce < LowLifeForceThreshold)
            multiplier *= LowLifeForceExpMultiplier;
        if (userPet.CurrentPetBond > HighBondThreshold)
            multiplier *= HighBondExpMultiplier;

        return checked((int)Math.Ceiling(baseExp * multiplier));
    }

    internal static void ApplyExperience(
        UserPet userPet,
        Pet pet,
        int expToAdd,
        DateTime updatedAt)
    {
        if (expToAdd <= 0)
            return;
        if (userPet.PetExp <= 0 || pet.ExpRate <= 0)
            throw new AppSystemException("Pet progression is not configured correctly.");

        userPet.CurrentPetExp = checked(userPet.CurrentPetExp + expToAdd);
        while (userPet.CurrentPetExp >= userPet.PetExp)
        {
            userPet.CurrentPetExp -= userPet.PetExp;
            userPet.Level = checked(userPet.Level + 1);
            userPet.PetEnergy = Scale(userPet.PetEnergy, pet.EnergyRate);
            userPet.PetBond = Scale(userPet.PetBond, pet.BondRate);
            userPet.PetLifeForce = Scale(userPet.PetLifeForce, pet.LifeForceRate);
            userPet.PetExp = Scale(userPet.PetExp, pet.ExpRate);
            if (userPet.PetExp <= 0)
                throw new AppSystemException("Pet progression is not configured correctly.");

            userPet.CurrentPetEnergy = userPet.PetEnergy;
            userPet.CurrentPetBond = userPet.PetBond;
            userPet.CurrentPetLifeForce = userPet.PetLifeForce;
        }

        userPet.ExpUpdatedAt = updatedAt;
    }

    private static int Scale(int value, double rate)
    {
        if (value < 0 || rate <= 0 || double.IsNaN(rate) || double.IsInfinity(rate))
            throw new AppSystemException("Pet progression is not configured correctly.");

        return checked((int)Math.Ceiling(value * rate));
    }
}
