using BLL.Service;
using BLL.Exceptions;
using DAL.Interfaces;
using DAL.Models;
using Moq;
using Xunit;

public class EvolutionThresholdTests
{
    private readonly Mock<IPetRepository> pets = new();
    private readonly Mock<IGenericRepository<UserPet>> users = new();
    private readonly Mock<IGenericRepository<PetEvolutionHistory>> history = new();
    private readonly Mock<IGenericRepository<Pet>> definitions = new();
    private readonly Mock<ISystemSettingRepository> settings = new();
    private readonly Guid userId = Guid.NewGuid();
    private PetService Service() => new(pets.Object, users.Object,
        Mock.Of<IPetInteractionRepository>(), Mock.Of<IGenericRepository<PetInteraction>>(),
        Mock.Of<IPetEvolutionHistoryRepository>(), history.Object, definitions.Object,
        settings.Object, Mock.Of<IGenericRepository<UserProfile>>(), Mock.Of<IGenericRepository<Wallet>>());

    [Theory]
    [InlineData("dawn", 1, 15)] [InlineData("moonlight", 1, 15)] [InlineData("warm_sun", 1, 15)]
    [InlineData("dawn", 2, 30)] [InlineData("moonlight", 2, 30)] [InlineData("warm_sun", 2, 30)]
    public void OverridesLegacyStageData(string affinity, int stage, int expected) =>
        Assert.Equal(expected, PetEvolutionPolicy.RequiredLevel(affinity, stage, 5));

    [Theory] [InlineData(5)] [InlineData(10)] [InlineData(14)]
    public async Task StarterRejectedBelow15(int level)
    {
        pets.Setup(p => p.GetUserPetWithPetAsync(userId)).ReturnsAsync(new UserPet { Level=level, Pet=new Pet { PetName="Lumina" } });
        await Assert.ThrowsAsync<BadRequestException>(()=>Service().EvolveStarterAsync(userId,Guid.NewGuid()));
        await Assert.ThrowsAsync<BadRequestException>(()=>Service().GetEvolutionOptionsAsync(userId));
        history.Verify(h=>h.AddAsync(It.IsAny<PetEvolutionHistory>()),Times.Never);
    }

    [Theory] [InlineData(15)] [InlineData(30)]
    public async Task StarterSucceedsWithoutResettingExperience(int level)
    {
        settings.Setup(s=>s.GetByKeyAsync("PetExpIncreasePerLevel")).ReturnsAsync(new SystemSetting { SettingValue="50" });
        var pet=new Pet { PetId=Guid.NewGuid(), PetName="Dawn", PvpAffinityCode="dawn", Exp=100 };
        var state=new UserPet { Level=level, Pet=new Pet { PetName="Lumina" }, PetExp=800, CurrentPetExp=55 };
        pets.Setup(p=>p.GetUserPetWithPetAsync(userId)).ReturnsAsync(state);
        definitions.Setup(p=>p.GetByIdAsync(pet.PetId)).ReturnsAsync(pet);
        pets.Setup(p=>p.GetFirstStageAsync(pet.PetId)).ReturnsAsync(new PetStage { StageNo=1, RequiredLevel=5 });
        await Service().EvolveStarterAsync(userId,pet.PetId);
        Assert.Equal(pet.PetId,state.PetId); Assert.Equal(level,state.Level);
        Assert.Equal(100+50*(level-1),state.PetExp); Assert.Equal(55,state.CurrentPetExp);
        history.Verify(h=>h.AddAsync(It.IsAny<PetEvolutionHistory>()),Times.Once);
    }

    [Theory] [InlineData(15,false)] [InlineData(29,false)] [InlineData(30,true)] [InlineData(31,true)]
    public async Task SecondEvolutionHonors30DespiteLegacy10(int level,bool eligible)
    {
        var id=Guid.NewGuid();
        pets.Setup(p=>p.GetUserPetWithPetAsync(userId)).ReturnsAsync(new UserPet { PetId=id,Level=level,Pet=new Pet { PetName="Dawn",PvpAffinityCode="dawn" } });
        pets.Setup(p=>p.GetNextStageAsync(id,1)).ReturnsAsync(new PetStage { StageNo=2, RequiredLevel=10 });
        pets.Setup(p=>p.GetAnimationsAsync(id,2)).ReturnsAsync(new List<PetAnimation>());
        if (!eligible) await Assert.ThrowsAsync<BadRequestException>(()=>Service().EvolveNextAsync(userId));
        else Assert.Equal(30,(await Service().EvolveNextAsync(userId)).RequiredLevel);
        history.Verify(h=>h.AddAsync(It.IsAny<PetEvolutionHistory>()),eligible?Times.Once():Times.Never());
    }
}
