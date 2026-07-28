using BLL.Exceptions;
using BLL.Service;
using DAL.Interfaces;
using DAL.Models;
using DAL.Repository;
using Moq;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class PetOverviewServiceTests
{
    [Fact]
    public async Task GetPetOverview_WhenUserHasNoPet_ThrowsNotFound()
    {
        var fixture = new PetServiceFixture();

        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.GetPetOverviewAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPetOverview_ReturnsServerStatsStageAndEligibility()
    {
        var fixture = new PetServiceFixture();
        var userId = Guid.NewGuid();
        var petId = Guid.NewGuid();
        var now = DateTime.UtcNow.AddHours(7);
        var pet = new Pet
        {
            PetId = petId,
            PetName = "Tinh Linh Ánh Trăng",
            PvpAffinityCode = "moonlight"
        };
        var userPet = new UserPet
        {
            UserId = userId,
            PetId = petId,
            Pet = pet,
            PetName = "Luna",
            Level = 12,
            CurrentPetExp = 240,
            PetExp = 500,
            CurrentPetEnergy = 80,
            PetEnergy = 100,
            CurrentPetBond = 72,
            PetBond = 100,
            CurrentPetLifeForce = 65,
            PetLifeForce = 100,
            EnergyUpdatedAt = now,
            BondUpdatedAt = now,
            LifeForceUpdatedAt = now,
            ExpUpdatedAt = now
        };
        var currentStage = new PetStage
        {
            PetId = petId,
            StageNo = 1,
            StageName = "Stage 1",
            RequiredLevel = 5
        };
        var nextStage = new PetStage
        {
            PetId = petId,
            StageNo = 2,
            StageName = "Stage 2",
            RequiredLevel = 10
        };

        fixture.PetRepository
            .Setup(x => x.GetUserPetWithPetAsync(userId))
            .ReturnsAsync(userPet);
        fixture.PetRepository
            .Setup(x => x.GetFirstStageAsync(petId))
            .ReturnsAsync(currentStage);
        fixture.PetRepository
            .Setup(x => x.GetNextStageAsync(petId, 1))
            .ReturnsAsync(nextStage);

        var result = await fixture.Service.GetPetOverviewAsync(userId);

        Assert.Equal(petId, result.PetId);
        Assert.Equal("Luna", result.Nickname);
        Assert.Equal("moonlight", result.AffinityCode);
        Assert.Equal(12, result.Level);
        Assert.Equal(240, result.CurrentExp);
        Assert.Equal(500, result.MaxExp);
        Assert.Equal(1, result.StageNo);
        Assert.Equal("Stage 1", result.StageName);
        Assert.Equal("idle", result.AnimationType);
        Assert.True(result.CanEvolve);
        Assert.Equal(10, result.NextEvolutionLevel);
    }

    [Fact]
    public async Task GetUserPetName_LoadsPetNavigationAndReturnsNickname()
    {
        var fixture = new PetServiceFixture();
        var userId = Guid.NewGuid();
        var petId = Guid.NewGuid();
        fixture.PetRepository
            .Setup(x => x.GetUserPetWithPetAsync(userId))
            .ReturnsAsync(new UserPet
            {
                UserId = userId,
                PetId = petId,
                PetName = "Lumi",
                Pet = new Pet { PetId = petId, PetName = "Lumina" }
            });

        var result = await fixture.Service.GetUserPetNameAsync(userId);

        Assert.Equal(petId, result.PetId);
        Assert.Equal("Lumi", result.PetName);
        fixture.PetRepository.Verify(
            x => x.GetUserPetWithPetAsync(userId),
            Times.Once);
    }

    private sealed class PetServiceFixture
    {
        public Mock<IPetRepository> PetRepository { get; } = new();
        public Mock<IGenericRepository<UserPet>> UserPets { get; } = new();
        private Mock<IPetInteractionRepository> Interactions { get; } = new();
        private Mock<IGenericRepository<PetInteraction>> PetInteractions { get; } = new();
        private Mock<IPetEvolutionHistoryRepository> EvolutionHistory { get; } = new();
        private Mock<IGenericRepository<PetEvolutionHistory>> PetHistory { get; } = new();
        private Mock<IGenericRepository<Pet>> Pets { get; } = new();
        private Mock<ISystemSettingRepository> SystemSettingRepository { get; } = new();
        public PetServiceFixture()
        {
            UserPets.Setup(x => x.SaveAsync()).Returns(Task.CompletedTask);
            EvolutionHistory
                .Setup(x => x.GetLatestAsync(It.IsAny<Guid>()))
                .ReturnsAsync((PetEvolutionHistory?)null);
            Service = new PetService(
                PetRepository.Object,
                UserPets.Object,
                Interactions.Object,
                PetInteractions.Object,
                EvolutionHistory.Object,
                PetHistory.Object,
                Pets.Object,
                 SystemSettingRepository.Object);
        }

        public PetService Service { get; }
    }
}
