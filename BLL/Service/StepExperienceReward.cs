using BLL.Exceptions;
using DAL.Models;

namespace BLL.Service;

internal static class StepExperienceReward
{
    

    internal const int StepsPerReward = 100;



    private const int LifeforceTier1 = 20;
    private const int LifeforceTier2 = 40;
    private const int LifeforceTier3 = 60;
    private const int LifeforceTier4 = 80;

    private const double LifeforceRate1 = 0.50;
    private const double LifeforceRate2 = 0.60;
    private const double LifeforceRate3 = 0.75;
    private const double LifeforceRate4 = 0.90;
    private const double LifeforceRate5 = 1.00;



    private const int BondTier1 = 30;
    private const int BondTier2 = 50;
    private const int BondTier3 = 70;
    private const int BondTier4 = 90;

    private const double BondBonus1 = 0.00;
    private const double BondBonus2 = 0.05;
    private const double BondBonus3 = 0.10;
    private const double BondBonus4 = 0.15;
    private const double BondBonus5 = 0.20;


    

    internal static int ParseExpPerReward(
        string? configuredValue)
    {
        if (!int.TryParse(
                configuredValue,
                out var expPerReward)
            || expPerReward <= 0)
        {
            throw new AppSystemException(
                "Step-to-exp rate is not configured correctly.");
        }

        return expPerReward;
    }


   

    internal static int ParseExpIncreasePerLevel(
        string? configuredValue)
    {
        if (!int.TryParse(
                configuredValue,
                out var increment)
            || increment <= 0)
        {
            throw new AppSystemException(
                "Pet EXP increase per level is not configured correctly.");
        }

        return increment;
    }


   

    internal static int CalculateRewardsCrossed(
        long previousValidatedSteps,
        int newlyValidatedSteps)
    {
        if (previousValidatedSteps < 0 ||
            newlyValidatedSteps < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newlyValidatedSteps));
        }

        var previousRewards =
            previousValidatedSteps / StepsPerReward;

        var currentRewards =
            checked(
                previousValidatedSteps +
                newlyValidatedSteps)
            / StepsPerReward;

        return checked(
            (int)(
                currentRewards -
                previousRewards));
    }


    

    internal static double CalculateLifeforceRate(
        int currentLifeforce)
    {
        if (currentLifeforce < 0 ||
            currentLifeforce > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentLifeforce),
                "Lifeforce must be between 0 and 100.");
        }

        if (currentLifeforce < LifeforceTier1)
            return LifeforceRate1;

        if (currentLifeforce < LifeforceTier2)
            return LifeforceRate2;

        if (currentLifeforce < LifeforceTier3)
            return LifeforceRate3;

        if (currentLifeforce < LifeforceTier4)
            return LifeforceRate4;

        return LifeforceRate5;
    }


   
    internal static double CalculateBondBonus(
        int currentBond)
    {
        if (currentBond < 0 ||
            currentBond > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentBond),
                "Bond must be between 0 and 100.");
        }

        if (currentBond < BondTier1)
            return BondBonus1;

        if (currentBond < BondTier2)
            return BondBonus2;

        if (currentBond < BondTier3)
            return BondBonus3;

        if (currentBond < BondTier4)
            return BondBonus4;

        return BondBonus5;
    }


   
    internal static double CalculateExperienceRate(
        int currentBond,
        int currentLifeforce)
    {
        var lifeforceRate =
            CalculateLifeforceRate(
                currentLifeforce);

        var bondBonus =
            CalculateBondBonus(
                currentBond);

        return lifeforceRate *
               (1.0 + bondBonus);
    }


   

    internal static int CalculateActualExperience(
        int baseExp,
        int currentBond,
        int currentLifeforce)
    {
        if (baseExp <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseExp));
        }

        var experienceRate =
            CalculateExperienceRate(
                currentBond,
                currentLifeforce);

        var calculatedExp =
            baseExp *
            experienceRate;

        return checked(
            (int)Math.Round(
                calculatedExp,
                MidpointRounding.AwayFromZero));
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

        if (userPet.Level < 1 ||
            pet.Exp <= 0 ||
            expIncreasePerLevel <= 0)
        {
            throw new AppSystemException(
                "Pet progression is not configured correctly.");
        }


       

        userPet.PetExp =
            CalculateRequiredExperience(
                userPet.Level,
                pet.Exp,
                expIncreasePerLevel);


       

        userPet.CurrentPetExp =
            checked(
                userPet.CurrentPetExp +
                expToAdd);


 

        while (userPet.CurrentPetExp >=
               userPet.PetExp)
        {
            userPet.CurrentPetExp -=
                userPet.PetExp;

            userPet.Level =
                checked(
                    userPet.Level + 1);

            userPet.PetExp =
                CalculateRequiredExperience(
                    userPet.Level,
                    pet.Exp,
                    expIncreasePerLevel);
        }


       

        userPet.ExpUpdatedAt =
            updatedAt;
    }

    internal static int CalculateRequiredExperience(
        int level,
        int baseExp,
        int expIncreasePerLevel)
    {
        if (level < 1 ||
            baseExp <= 0 ||
            expIncreasePerLevel <= 0)
        {
            throw new AppSystemException(
                "Pet progression is not configured correctly.");
        }

        return checked(
            baseExp +
            checked(
                (level - 1) *
                expIncreasePerLevel));
    }
}
