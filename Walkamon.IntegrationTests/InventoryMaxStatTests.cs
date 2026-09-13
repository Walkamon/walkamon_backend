using System.Linq.Expressions;
using BLL.Exceptions;
using BLL.Service;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;

namespace Walkamon.IntegrationTests;

public sealed class InventoryMaxStatTests
{
    [Theory]
    [InlineData("energy", 100)]
    [InlineData("life_force", 100)]
    [InlineData("sml", 100)]
    [InlineData("bond", 100)]
    [InlineData("energy", 110)]
    public async Task FullStat_RejectsWithoutConsumingOrSaving(string effect, int current)
    {
        var (service, inventory, pet, repository) = Setup(effect, current);

        await Assert.ThrowsAsync<ConflictException>(() => service.UseItemAsync(
            inventory.UserId, new UseItemRequest { ItemId = inventory.ItemId }));

        Assert.Equal(2, inventory.Quantity);
        Assert.Equal(current, pet.CurrentPetEnergy);
        Assert.Equal(current, pet.CurrentPetLifeForce);
        Assert.Equal(current, pet.CurrentPetBond);
        Assert.Equal(0, repository.SaveCount);
        Assert.Equal(0, repository.MutationCount);
    }

    [Theory]
    [InlineData("energy")]
    [InlineData("life_force")]
    [InlineData("sml")]
    [InlineData("bond")]
    public async Task BelowMaximum_RestoresToCapAndConsumesExactlyOne(string effect)
    {
        var (service, inventory, pet, repository) = Setup(effect, 95);
        var result = await service.UseItemAsync(
            inventory.UserId, new UseItemRequest { ItemId = inventory.ItemId });

        Assert.Equal(1, result.RemainingQuantity);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(1, repository.MutationCount);
        Assert.Equal(100, effect switch
        {
            "energy" => result.Energy,
            "bond" => result.Bond,
            _ => result.LifeForce
        });
        Assert.Equal(100, pet.PetEnergy);
        Assert.Equal(100, pet.PetLifeForce);
        Assert.Equal(100, pet.PetBond);
    }

    private static (InventoryService, InventoryItem, UserPet, Repository<InventoryItem>) Setup(string effect, int current)
    {
        var item = new Item { ItemId = Guid.NewGuid(), ItemName = "Care item", IsActive = true, EffectTypeCode = effect, EffectValue = 25 };
        var inventory = new InventoryItem { UserId = Guid.NewGuid(), ItemId = item.ItemId, Quantity = 2 };
        var pet = new UserPet { CurrentPetEnergy = current, PetEnergy = 100, CurrentPetLifeForce = current, PetLifeForce = 100, CurrentPetBond = current, PetBond = 100 };
        var repository = new Repository<InventoryItem>(inventory);
        return (new InventoryService(repository, new Repository<Item>(item), new Repository<ItemType>(), new Repository<UserPet>(pet)), inventory, pet, repository);
    }

    private sealed class Repository<T>(params T[] items) : IGenericRepository<T> where T : class
    {
        public int SaveCount { get; private set; }
        public int MutationCount { get; private set; }
        public Task<IEnumerable<T>> GetAllAsync() => Task.FromResult<IEnumerable<T>>(items);
        public Task<T> GetByIdAsync(Guid id) => Task.FromResult(items.Single());
        public Task AddAsync(T entity) { MutationCount++; return Task.CompletedTask; }
        public void Update(T entity) => MutationCount++;
        public void Delete(T entity) => MutationCount++;
        public Task SaveAsync() { SaveCount++; return Task.CompletedTask; }
        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(items.Any(predicate.Compile()));
        public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(items.Where(predicate.Compile()));
        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(items.FirstOrDefault(predicate.Compile()));
    }
}
