using BLL.Exceptions;
using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class InventoryItemEffectTests
{
    [Theory]
    [InlineData("energy", 90, 100, 25, 100)]
    [InlineData("bond", 25, 100, 15, 40)]
    [InlineData("life_force", 95, 100, 20, 100)]
    [InlineData("sml", 10, 100, 20, 30)]
    public void ApplyItemEffect_RestoresCurrentStatWithoutChangingMaximum(
        string effectCode,
        int current,
        int maximum,
        int amount,
        int expected)
    {
        var pet = new UserPet
        {
            CurrentPetEnergy = current,
            PetEnergy = maximum,
            CurrentPetBond = current,
            PetBond = maximum,
            CurrentPetLifeForce = current,
            PetLifeForce = maximum
        };

        InventoryService.ApplyItemEffect(pet, effectCode, amount);

        var actual = effectCode switch
        {
            "energy" => pet.CurrentPetEnergy,
            "bond" => pet.CurrentPetBond,
            _ => pet.CurrentPetLifeForce
        };
        Assert.Equal(expected, actual);
        Assert.Equal(maximum, pet.PetEnergy);
        Assert.Equal(maximum, pet.PetBond);
        Assert.Equal(maximum, pet.PetLifeForce);
    }

    [Fact]
    public void ApplyItemEffect_RejectsNegativeAmount()
    {
        Assert.Throws<BadRequestException>(() =>
            InventoryService.ApplyItemEffect(
                new UserPet { PetEnergy = 100 },
                "energy",
                -1));
    }
}
