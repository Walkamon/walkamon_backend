using BLL.Exceptions;
using DAL.Models;

namespace BLL.Service;

internal static class StepExperienceReward
{
    internal const int StepsPerReward = 100;

    internal static int ParseExpPerReward(string? configuredValue)
    {
        if (!int.TryParse(configuredValue, out var expPerReward) || expPerReward <= 0)
            throw new AppSystemException("Step-to-exp rate is not configured correctly.");

        return expPerReward;
    }

    internal static int ParseExpIncreasePerLevel(string? configuredValue)
    {
        if (!int.TryParse(configuredValue, out var increment) || increment <= 0)
            throw new AppSystemException("Pet EXP increase per level is not configured correctly.");

        return increment;
    }

    internal static int CalculateRewardsCrossed(long previousValidatedSteps, int newlyValidatedSteps)
    {
        if (previousValidatedSteps < 0 || newlyValidatedSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(newlyValidatedSteps));

        var previousRewards = previousValidatedSteps / StepsPerReward;
        var currentRewards = checked(previousValidatedSteps + newlyValidatedSteps) / StepsPerReward;
        return checked((int)(currentRewards - previousRewards));
    }

    internal static void ApplyExperience(
        UserPet userPet,
        Pet pet,
        int expToAdd,
        int expIncreasePerLevel,
        DateTime updatedAt)
    {
        if (expToAdd <= 0)
            return;
        if (userPet.Level < 1 || pet.Exp <= 0 || expIncreasePerLevel <= 0)
            throw new AppSystemException("Pet progression is not configured correctly.");

        userPet.PetExp = CalculateRequiredExperience(
            userPet.Level,
            pet.Exp,
            expIncreasePerLevel);
        userPet.CurrentPetExp = checked(userPet.CurrentPetExp + expToAdd);
        while (userPet.CurrentPetExp >= userPet.PetExp)
        {
            userPet.CurrentPetExp -= userPet.PetExp;
            userPet.Level = checked(userPet.Level + 1);
            userPet.PetExp = CalculateRequiredExperience(
                userPet.Level,
                pet.Exp,
                expIncreasePerLevel);
        }

        userPet.ExpUpdatedAt = updatedAt;
    }

    internal static int CalculateRequiredExperience(
        int level,
        int baseExp,
        int expIncreasePerLevel)
    {
        if (level < 1 || baseExp <= 0 || expIncreasePerLevel <= 0)
            throw new AppSystemException("Pet progression is not configured correctly.");

        return checked(baseExp + checked((level - 1) * expIncreasePerLevel));
    }
}
