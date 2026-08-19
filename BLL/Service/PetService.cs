using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using DAL.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Service
{
    public class PetService : IPetService
    {
        private readonly IPetRepository _petRepository;
        private readonly IGenericRepository<UserPet> _repository;
        private readonly IGenericRepository<PetInteraction> _PetInteraction;
        private readonly IGenericRepository<PetEvolutionHistory> _PetHistory;
        private readonly IPetInteractionRepository _interactionRepository;
        private readonly IPetEvolutionHistoryRepository _PetEvolutionHistory;
        private readonly IGenericRepository<Pet> _Pet;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly IGenericRepository<UserProfile> _userProfileRepository;
        private readonly IGenericRepository<Wallet> _walletRepository;
        public PetService(
           IPetRepository petRepository, IGenericRepository<UserPet> repository ,
           IPetInteractionRepository petInteractionRepository, IGenericRepository<PetInteraction> PetInteraction,
          IPetEvolutionHistoryRepository petEvolutionHistoryRepository,
          IGenericRepository<PetEvolutionHistory> PetHistory,
          IGenericRepository<Pet> Pet,
          ISystemSettingRepository systemSettingRepository,
           IGenericRepository<UserProfile> userProfileRepository,
             IGenericRepository<Wallet> walletRepository)
            
        {
            _Pet = Pet;
            _PetHistory = PetHistory;
            _PetEvolutionHistory = petEvolutionHistoryRepository;
            _PetInteraction = PetInteraction;
            _interactionRepository = petInteractionRepository;
            _petRepository = petRepository;
            _repository = repository;
            _systemSettingRepository = systemSettingRepository;
            _userProfileRepository = userProfileRepository;
            _walletRepository = walletRepository;
        }
        public async Task CreateUserPetAsync(Guid userId, CreateUserPetRequest request)
        {
            var starterPet = await _petRepository.GetStarterPetAsync();

            if (starterPet == null)
                throw new NotFoundException("Starter pet not found.");

            var userPet = new UserPet
            {
                UserId = userId,
                PetId = starterPet.PetId,

                PetName = request.PetName,

                Level = 1,

                PetExp = starterPet.Exp,
                PetEnergy = starterPet.Energy,
                PetBond = starterPet.Bond,
                PetLifeForce = starterPet.LifeForce,

                CurrentPetExp = 0,
                CurrentPetEnergy = 50,
                CurrentPetBond = 50,
                CurrentPetLifeForce = 50,

                EnergyUpdatedAt = DateTime.UtcNow,
                BondUpdatedAt = DateTime.UtcNow,
                LifeForceUpdatedAt = DateTime.UtcNow,
                ExpUpdatedAt = DateTime.UtcNow

            };
            var existed = await _repository.AnyAsync(x => x.UserId == userId);

            if (existed)
                throw new BadRequestException("User already has a pet.");
            await _repository.AddAsync(userPet);
            var profile = (await _userProfileRepository.GetAllAsync())
    .FirstOrDefault(x => x.UserId == userId);

            if (profile != null)
            {
                profile.HasSeenStory = true;
                profile.UpdatedAt = DateTime.UtcNow;

                _userProfileRepository.Update(profile);
            }
            await _repository.SaveAsync();
            await _userProfileRepository.SaveAsync();
        }
        public async Task<PetStatusResponse> GetPetStatusAsync(Guid currentUserId)
        {
            var pet = await _petRepository.GetUserPetAsync(currentUserId);

            if (pet == null)
                throw new NotFoundException("Pet not found.");

            await UpdateEnergy(pet);

            await UpdateBond(pet);

            await UpdateLifeForce(pet);

            _repository.Update(pet);

            await _repository.SaveAsync();

            return new PetStatusResponse
            {
               
                CurrentEnergy = pet.CurrentPetEnergy,
                MaxEnergy = pet.PetEnergy,

                CurrentBond = pet.CurrentPetBond,
                MaxBond = pet.PetBond,

                CurrentLifeForce = pet.CurrentPetLifeForce,
                MaxLifeForce = pet.PetLifeForce
            };
        }
        public async Task<PetOverviewResponse> GetPetOverviewAsync(Guid userId)
        {
            var userPet = await _petRepository.GetUserPetWithPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            await UpdateEnergy(userPet);
            await UpdateBond(userPet);
            await UpdateLifeForce(userPet);

            _repository.Update(userPet);
            await _repository.SaveAsync();

            var latest = await _PetEvolutionHistory.GetLatestAsync(userId);
            var stage = latest?.Stage
                ?? await _petRepository.GetFirstStageAsync(userPet.PetId);

            var isStarter = userPet.Pet.PetName.Equals(
                "Lumina",
                StringComparison.OrdinalIgnoreCase);

            int? nextEvolutionLevel;
            if (isStarter)
            {
                nextEvolutionLevel = 5;
            }
            else if (stage != null)
            {
                var nextStage = await _petRepository.GetNextStageAsync(
                    userPet.PetId,
                    stage.StageNo);
                nextEvolutionLevel = nextStage?.RequiredLevel;
            }
            else
            {
                nextEvolutionLevel = null;
            }

            return new PetOverviewResponse
            {
                PetId = userPet.PetId,
                Nickname = userPet.PetName,
                FormName = userPet.Pet.PetName,
                AffinityCode = ResolveAffinityCode(userPet.Pet),
                Level = userPet.Level,
                CurrentExp = userPet.CurrentPetExp,
                MaxExp = userPet.PetExp,
                CurrentEnergy = userPet.CurrentPetEnergy,
                MaxEnergy = userPet.PetEnergy,
                CurrentLifeForce = userPet.CurrentPetLifeForce,
                MaxLifeForce = userPet.PetLifeForce,
                CurrentBond = userPet.CurrentPetBond,
                MaxBond = userPet.PetBond,
                StageNo = stage?.StageNo ?? 0,
                StageName = stage?.StageName ?? "Mầm Non",
                AnimationType = ResolveAnimationType(userPet),
                CanEvolve = nextEvolutionLevel.HasValue
                    && userPet.Level >= nextEvolutionLevel.Value,
                NextEvolutionLevel = nextEvolutionLevel
            };
        }
        public async Task<PetStatusResponse> TapSpiritAsync(Guid userId)
        {
            var today = DateOnly.FromDateTime(GetVietnamNow());

            var interaction = await _interactionRepository
                .GetTodayInteractionAsync(userId, "tap", today);

            bool isNew = false;

            if (interaction == null)
            {
                interaction = new PetInteraction
                {
                    InteractionId = Guid.NewGuid(),
                    UserId = userId,
                    InteractionType = "tap",
                    InteractionDate = today,
                    Count = 0
                };

                await _PetInteraction.AddAsync(interaction);
                isNew = true;
            }

            var userPet = await _petRepository.GetUserPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");


            await UpdateEnergy(userPet);
            await UpdateBond(userPet);
            await UpdateLifeForce(userPet);

          
            if (userPet.CurrentPetBond >= userPet.PetBond)
                throw new BadRequestException("Pet bond is already full.");

          
            if (interaction.Count >= 5)
                throw new BadRequestException("You have reached the maximum tap limit today.");

          
            userPet.CurrentPetBond = Math.Min(
                userPet.PetBond,
                userPet.CurrentPetBond + 20);

            interaction.Count++;

            _repository.Update(userPet);

          
            if (!isNew)
            {
                _PetInteraction.Update(interaction);
            }

            await _repository.SaveAsync();

            return new PetStatusResponse
            {
               

                CurrentEnergy = userPet.CurrentPetEnergy,
                MaxEnergy = userPet.PetEnergy,

                CurrentBond = userPet.CurrentPetBond,
                MaxBond = userPet.PetBond,

                CurrentLifeForce = userPet.CurrentPetLifeForce,
                MaxLifeForce = userPet.PetLifeForce,

               
            };
        }

        public async Task<PetStatusResponse> FeedSpiritAsync(Guid userId)
        {
            var today = DateOnly.FromDateTime(GetVietnamNow());

            var interaction = await _interactionRepository
                .GetTodayInteractionAsync(userId, "feed", today);

            bool isNew = false;

            if (interaction == null)
            {
                interaction = new PetInteraction
                {
                    InteractionId = Guid.NewGuid(),
                    UserId = userId,
                    InteractionType = "feed",
                    InteractionDate = today,
                    Count = 0
                };

                await _PetInteraction.AddAsync(interaction);
                isNew = true;
            }

            var userPet = await _petRepository.GetUserPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            var wallet = (await _walletRepository.GetAllAsync())
                .FirstOrDefault(x => x.UserId == userId);

            if (wallet == null)
                throw new NotFoundException("Wallet not found.");

            await UpdateEnergy(userPet);
            await UpdateBond(userPet);
            await UpdateLifeForce(userPet);

            if (userPet.CurrentPetLifeForce >= userPet.PetLifeForce)
                throw new BadRequestException("Pet life force is already full.");

            if (interaction.Count >= 10)
                throw new BadRequestException("You have reached the maximum feed limit today.");

            if (wallet.Balance < 5)
                throw new BadRequestException(
                    $"Not enough balance. Need 5 dewdrop to feed your pet.");

           
            wallet.Balance -= 5;

       
            userPet.CurrentPetLifeForce = Math.Min(
                userPet.PetLifeForce,
                userPet.CurrentPetLifeForce + 20);

            interaction.Count++;

            _repository.Update(userPet);
            _walletRepository.Update(wallet);

            if (!isNew)
            {
                _PetInteraction.Update(interaction);
            }

            await _repository.SaveAsync();
            await _walletRepository.SaveAsync();

            return new PetStatusResponse
            {
                CurrentEnergy = userPet.CurrentPetEnergy,
                MaxEnergy = userPet.PetEnergy,

                CurrentBond = userPet.CurrentPetBond,
                MaxBond = userPet.PetBond,

                CurrentLifeForce = userPet.CurrentPetLifeForce,
                MaxLifeForce = userPet.PetLifeForce,

            };
        }
        public async Task<PetInfoResponse> GetPetInfoAsync(Guid userId)
        {
            var userPet = await _petRepository.GetUserPetWithPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            return new PetInfoResponse
            {
                PetId = userPet.Pet.PetId,

                PetName = userPet.Pet.PetName,

                ExpRate = userPet.Pet.ExpRate,

                EnergyRate = userPet.Pet.EnergyRate,

                BondRate = userPet.Pet.BondRate,

                LifeForceRate = userPet.Pet.LifeForceRate
            };
        }
        public async Task<List<EvolutionOptionResponse>> GetEvolutionOptionsAsync(Guid userId)
        {
            var userPet = await _petRepository.GetUserPetWithPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            
            if (!userPet.Pet.PetName.Equals("Lumina",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Pet has already evolved.");
            }

            
            if (userPet.Level < 5)
            {
                throw new BadRequestException(
                    "Pet must reach level 5.");
            }

            var pets = await _petRepository.GetEvolutionOptionsAsync();

            var result = new List<EvolutionOptionResponse>();

            foreach (var pet in pets.Where(IsAllowedEvolutionPet))
            {
                var stage = await _petRepository.GetFirstStageAsync(pet.PetId);

                result.Add(new EvolutionOptionResponse
                {
                    PetId = pet.PetId,
                    PetName = pet.PetName,
                    RequiredLevel = stage?.RequiredLevel ?? 1,
                    StateUrl = stage?.StateUrl
                });
            }

            return result;
        }
        public async Task EvolveStarterAsync(
    Guid userId,
    Guid petId)
        {
            var userPet = await _petRepository.GetUserPetWithPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            if (!userPet.Pet.PetName.Equals("Lumina",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Pet has already evolved.");
            }

            if (userPet.Level < 5)
                throw new BadRequestException(
                    "Pet must reach level 5.");

            var pet = await _Pet.GetByIdAsync(petId);

            if (pet == null)
                throw new NotFoundException("Evolution pet not found.");

            if (!IsAllowedEvolutionPet(pet))
                throw new BadRequestException("Evolution pet is not an allowed Lumina branch.");

            var firstStage = await _petRepository
                .GetFirstStageAsync(petId);

            if (firstStage == null)
                throw new BadRequestException(
                    "Evolution stage not found.");

            
            userPet.PetId = pet.PetId;

          
            userPet.PetEnergy = pet.Energy;
            userPet.PetBond = pet.Bond;
            userPet.PetLifeForce = pet.LifeForce;
            var expIncrementSetting = await _systemSettingRepository
                .GetByKeyAsync("PetExpIncreasePerLevel");
            var expIncrement = StepExperienceReward.ParseExpIncreasePerLevel(
                expIncrementSetting?.SettingValue);
            userPet.PetExp = StepExperienceReward.CalculateRequiredExperience(
                userPet.Level,
                pet.Exp,
                expIncrement);

            userPet.CurrentPetEnergy = pet.Energy;
            userPet.CurrentPetBond = pet.Bond;
            userPet.CurrentPetLifeForce = pet.LifeForce;

            userPet.EnergyUpdatedAt = DateTime.UtcNow;
            userPet.BondUpdatedAt = DateTime.UtcNow;
            userPet.LifeForceUpdatedAt = DateTime.UtcNow;
            userPet.ExpUpdatedAt = DateTime.UtcNow;

            _repository.Update(userPet);

            await _PetHistory.AddAsync(new PetEvolutionHistory
            {
                EvolutionId = Guid.NewGuid(),
                UserId = userId,
                StageId = firstStage.StageId,
                Level = userPet.Level,
                EvolvedAt = DateTime.UtcNow
            });

            await _repository.SaveAsync();
        }
        private async Task UpdateEnergy(UserPet pet)
        {
            var setting = await _systemSettingRepository
                .GetByKeyAsync("EnergyRecoveryIntervalMinutes");

            var intervalMinutes = ParseSetting(setting, "EnergyRecoveryIntervalMinutes", 1, 1440);

            var now = DateTime.UtcNow;

            var elapsedMinutes = (long)(now - pet.EnergyUpdatedAt).TotalMinutes;
            var elapsedIntervals = elapsedMinutes / intervalMinutes;

            if (elapsedIntervals <= 0)
                return;

            pet.CurrentPetEnergy = (int)Math.Min(
                (long)pet.PetEnergy,
                (long)pet.CurrentPetEnergy + elapsedIntervals);

            pet.EnergyUpdatedAt =
                pet.EnergyUpdatedAt.AddMinutes(elapsedIntervals * intervalMinutes);
        }
        public async Task<List<EvolutionStageResponse>> GetEvolutionStagesAsync(Guid userId)
        {
            var userPet = await _petRepository.GetUserPetWithPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            var stages = await _petRepository
                .GetStagesByPetIdAsync(userPet.PetId);

            if (!stages.Any())
                throw new NotFoundException("Pet stages not found.");

            var latest = await _PetEvolutionHistory
                .GetLatestAsync(userId);

            int currentStageNo;

          
            if (latest == null)
            {
                currentStageNo = 1;
            }
            else
            {
                currentStageNo = latest.Stage.StageNo;
            }

            var result = new List<EvolutionStageResponse>();

            foreach (var stage in stages)
            {
                var animations = await _petRepository
                    .GetAnimationsAsync(
                        userPet.PetId,
                        stage.StageNo);

                result.Add(new EvolutionStageResponse
                {
                    StageId = stage.StageId,

                    StageNo = stage.StageNo,

                    StageName = stage.StageName,

                    StateUrl = stage.StateUrl,

                    RequiredLevel = stage.RequiredLevel,

                    IsCurrent = stage.StageNo == currentStageNo,

                    IsUnlocked = userPet.Level >= stage.RequiredLevel,

                    Animations = animations
                        .Select(a => new PetAnimationResponse
                        {
                            TypeAnimation = a.TypeAnimation,
                            AnimationUrl = a.AnimationUrl
                        })
                        .ToList()
                });
            }

            return result;
        }
        public async Task<EvolutionStageResponse> EvolveNextAsync(Guid userId)
        {
            var userPet = await _petRepository.GetUserPetWithPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            var latest = await _PetEvolutionHistory.GetLatestAsync(userId);

            int currentStageNo = latest?.Stage.StageNo ?? 1;

            var nextStage = await _petRepository.GetNextStageAsync(
                userPet.PetId,
                currentStageNo);

            if (nextStage == null)
                throw new BadRequestException("Pet is already at the final evolution stage.");

            if (userPet.Level < nextStage.RequiredLevel)
                throw new BadRequestException(
                    $"Pet must reach level {nextStage.RequiredLevel}.");

            var history = new PetEvolutionHistory
            {
                EvolutionId = Guid.NewGuid(),
                UserId = userId,
                StageId = nextStage.StageId,
                Level = userPet.Level,
                EvolvedAt = DateTime.UtcNow
            };

            await _PetHistory.AddAsync(history);
            await _repository.SaveAsync();

            var animations = await _petRepository.GetAnimationsAsync(
                userPet.PetId,
                nextStage.StageNo);

            return new EvolutionStageResponse
            {
                StageId = nextStage.StageId,
                StageNo = nextStage.StageNo,
                StageName = nextStage.StageName,
                StateUrl = nextStage.StateUrl,
                RequiredLevel = nextStage.RequiredLevel,
                IsCurrent = true,
                IsUnlocked = true,
                Animations = animations.Select(x => new PetAnimationResponse
                {
                    TypeAnimation = x.TypeAnimation,
                    AnimationUrl = x.AnimationUrl
                }).ToList()
            };
        }
        public async Task<List<PetLeaderboardResponse>> GetLeaderboardAsync()
        {
            var userPets = await _petRepository.GetLeaderboardAsync();

            var result = new List<PetLeaderboardResponse>();

            int rank = 1;

            foreach (var userPet in userPets)
            {
                var latestEvolution = await _PetEvolutionHistory
                    .GetLatestAsync(userPet.UserId);

                PetStage? stage;

                if (latestEvolution != null)
                {
                    stage = latestEvolution.Stage;
                }
                else
                {
                    stage = await _petRepository.GetFirstStageAsync(userPet.PetId);
                }

                result.Add(new PetLeaderboardResponse
                {
                    Rank = rank,

                    UserId = userPet.UserId,

                    UserName = userPet.User?.UserProfile?.Username
                                ?? "Unknown",

                    PetName = userPet.PetName
                              ?? "Unknown",

                    Level = userPet.Level,

                    CurrentExp = userPet.CurrentPetExp,

                    MaxExp = userPet.PetExp,

                    StageName = stage?.StageName
                                ?? "Unknown",

                    StageImage = stage?.StateUrl
                });

                rank++;
            }

            return result;
        }
        private async Task UpdateBond(UserPet pet)
        {
            var setting = await _systemSettingRepository
                .GetByKeyAsync("BondDecayPercentPerDay");
            var floorSetting = await _systemSettingRepository
                .GetByKeyAsync("PassiveStatFloorPercent");

            var decayPercent = ParseSetting(setting, "BondDecayPercentPerDay", 0, 100);
            var floorPercent = ParseSetting(floorSetting, "PassiveStatFloorPercent", 0, 100);

            var now = DateTime.UtcNow;

            var elapsedDays = (long)(now - pet.BondUpdatedAt).TotalHours / 24;

            if (elapsedDays <= 0)
                return;

            var floor = PercentOfMax(pet.PetBond, floorPercent);
            if (pet.CurrentPetBond > floor)
            {
                var amountPerDay = PercentOfMax(pet.PetBond, decayPercent);
                pet.CurrentPetBond = Math.Max(
                    floor,
                    (int)Math.Max(0L, (long)pet.CurrentPetBond - amountPerDay * elapsedDays));
            }

            pet.BondUpdatedAt =
                pet.BondUpdatedAt.AddDays(elapsedDays);
        }

        private async Task UpdateLifeForce(UserPet pet)
        {
            var setting = await _systemSettingRepository
                .GetByKeyAsync("LifeForceDecayPercentPerDay");
            var floorSetting = await _systemSettingRepository
                .GetByKeyAsync("PassiveStatFloorPercent");

            var decayPercent = ParseSetting(setting, "LifeForceDecayPercentPerDay", 0, 100);
            var floorPercent = ParseSetting(floorSetting, "PassiveStatFloorPercent", 0, 100);

            var now = DateTime.UtcNow;

            var elapsedDays = (long)(now - pet.LifeForceUpdatedAt).TotalHours / 24;

            if (elapsedDays <= 0)
                return;

            var floor = PercentOfMax(pet.PetLifeForce, floorPercent);
            if (pet.CurrentPetLifeForce > floor)
            {
                var amountPerDay = PercentOfMax(pet.PetLifeForce, decayPercent);
                pet.CurrentPetLifeForce = Math.Max(
                    floor,
                    (int)Math.Max(0L, (long)pet.CurrentPetLifeForce - amountPerDay * elapsedDays));
            }

            pet.LifeForceUpdatedAt =
                pet.LifeForceUpdatedAt.AddDays(elapsedDays);
        }

        private static int ParseSetting(
            SystemSetting? setting,
            string key,
            int minimum,
            int maximum)
        {
            if (setting == null ||
                !int.TryParse(setting.SettingValue, out var value) ||
                value < minimum ||
                value > maximum)
            {
                throw new AppSystemException($"{key} is not configured correctly.");
            }

            return value;
        }

        private static int PercentOfMax(int max, int percent)
        {
            if (max < 0 || percent < 0 || percent > 100)
                throw new AppSystemException("Pet stat configuration is not valid.");

            return checked((int)Math.Ceiling(max * percent / 100d));
        }

        private static bool IsAllowedEvolutionPet(Pet pet) =>
            pet.PvpAffinityCode is "dawn" or "moonlight" or "warm_sun";
        private static DateTime GetVietnamNow()
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        }
        private static PetStatusResponse MapToResponse(UserPet pet)
        {
            return new PetStatusResponse
            {
              

                CurrentEnergy = pet.CurrentPetEnergy,
                MaxEnergy = pet.PetEnergy,

                CurrentBond = pet.CurrentPetBond,
                MaxBond = pet.PetBond,

                CurrentLifeForce = pet.CurrentPetLifeForce,
                MaxLifeForce = pet.PetLifeForce
            };
        }
        public async Task<List<EvolutionHistoryResponse>> GetEvolutionHistoryAsync(Guid userId)
        {
            var history = await _PetEvolutionHistory.GetHistoryAsync(userId);

            return history.Select(x => new EvolutionHistoryResponse
            {
                PetName = x.Stage.Pet.PetName,
                StageName = x.Stage.StageName,
                StageNo = x.Stage.StageNo,
                Level = x.Level,
                EvolvedAt = x.EvolvedAt
            }).ToList();
        }
        public async Task<FriendSpiritResponse> GetFriendSpiritAsync(Guid friendUserId)
        {
            var userPet = await _petRepository.GetFriendPetAsync(friendUserId);

            if (userPet == null)
                throw new NotFoundException("Friend spirit not found.");

            var latest = await _PetEvolutionHistory.GetLatestAsync(friendUserId);

            PetStage? stage;

            if (latest != null)
            {
                stage = latest.Stage;
            }
            else
            {
                stage = await _petRepository.GetFirstStageAsync(userPet.PetId);
            }

            var animations = await _petRepository.GetAnimationsAsync(
                userPet.PetId,
                stage!.StageNo);

            return new FriendSpiritResponse
            {
                UserId = friendUserId,

                UserName = userPet.User.UserProfile.Username,

                PetNickName = userPet.PetName,

                PetName = userPet.Pet.PetName,

                Level = userPet.Level,

                CurrentExp = userPet.CurrentPetExp,

                MaxExp = userPet.PetExp,

                CurrentEnergy = userPet.CurrentPetEnergy,
                MaxEnergy = userPet.PetEnergy,

                CurrentBond = userPet.CurrentPetBond,
                MaxBond = userPet.PetBond,

                CurrentLifeForce = userPet.CurrentPetLifeForce,
                MaxLifeForce = userPet.PetLifeForce,

                StageName = stage.StageName,

                StageImage = stage.StateUrl,

                Animations = animations.Select(x => new PetAnimationResponse
                {
                    TypeAnimation = x.TypeAnimation,
                    AnimationUrl = x.AnimationUrl
                }).ToList()
            };
        }
        public async Task<List<PetEvolutionPreviewResponse>>
GetEvolutionPreviewAsync()
        {
            var pets = await _petRepository.GetEvolutionPetsAsync();

            var result = new List<PetEvolutionPreviewResponse>();

            foreach (var pet in pets)
            {
                var stages = await _petRepository
                    .GetStagesByPetIdAsync(pet.PetId);

                var stageResponses = new List<PetStageAnimationResponse>();

                foreach (var stage in stages)
                {
                    var animations = await _petRepository
                        .GetAnimationsAsync(
                            pet.PetId,
                            stage.StageNo);

                    stageResponses.Add(new PetStageAnimationResponse
                    {
                        StageNo = stage.StageNo,

                        StageName = stage.StageName,

                        StageImage = stage.StateUrl,

                        RequiredLevel = stage.RequiredLevel,

                        Animations = animations
                            .Select(x => new PetAnimationInfoResponse
                            {
                                TypeAnimation = x.TypeAnimation,
                                AnimationUrl = x.AnimationUrl
                            })
                            .ToList()
                    });
                }

                result.Add(new PetEvolutionPreviewResponse
                {
                    PetId = pet.PetId,

                    PetName = pet.PetName,

                    Stages = stageResponses
                });
            }

            return result;
        }
        public async Task<CurrentAnimationResponse> GetCurrentAnimationAsync(Guid userId)
        {
            var userPet = await _petRepository.GetUserPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            await UpdateEnergy(userPet);
            await UpdateBond(userPet);
            await UpdateLifeForce(userPet);


            _repository.Update(userPet);
            await _repository.SaveAsync();


            var latest = await _PetEvolutionHistory.GetLatestAsync(userId);

            PetStage stage;

            if (latest == null)
            {
                stage = await _petRepository.GetStageAsync(userPet.PetId, 1);
            }
            else
            {
                stage = latest.Stage;
            }

            var animationType = ResolveAnimationType(userPet);

            var animation = await _petRepository.GetAnimationAsync(
                userPet.PetId,
                stage.StageNo,
                animationType);

            if (animation == null)
                throw new NotFoundException("Animation not found.");

            return new CurrentAnimationResponse
            {
                AnimationType = animationType,
                AnimationUrl = animation.AnimationUrl!,
                StageNo = stage.StageNo,
                StageName = stage.StageName
            };
        }

        public async Task<UserPetNameResponse> GetUserPetNameAsync(Guid userId)
        {
            var userPet = await _petRepository.GetUserPetWithPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            return new UserPetNameResponse
            {
                PetId = userPet.PetId,
                PetName = userPet.PetName
            };
        }

        public async Task<List<PetListResponse>> GetAllPetsAsync()
        {
            var pets = await _petRepository.GetAllWithDetailAsync();

            return pets.Select(x => new PetListResponse
            {
                PetId = x.PetId,
                PetName = x.PetName,
                LifeForce = x.LifeForce,
                Energy = x.Energy,
                Bond = x.Bond,
                Exp = x.Exp
            }).ToList();
        }
        public async Task<PetDetailResponse> GetPetDetailAsync(Guid petId)
        {
            var pet = await _petRepository.GetPetDetailAsync(petId);

            if (pet == null)
                throw new NotFoundException("Pet not found.");

            return new PetDetailResponse
            {
                PetId = pet.PetId,
                PetName = pet.PetName,

                LifeForce = pet.LifeForce,
                Energy = pet.Energy,
                Bond = pet.Bond,
                Exp = pet.Exp,

                LifeForceRate = pet.LifeForceRate,
                EnergyRate = pet.EnergyRate,
                BondRate = pet.BondRate,
                ExpRate = pet.ExpRate,

                Stages = pet.PetStages
                    .OrderBy(x => x.StageNo)
                    .Select(x => new PetStageDto
                    {
                        StageId = x.StageId,
                        StageNo = x.StageNo,
                        StageName = x.StageName,
                        RequiredLevel = x.RequiredLevel,
                        StateUrl = x.StateUrl,
                        IsActive = x.IsActive
                    }).ToList(),

                Animations = pet.PetAnimations
                    .OrderBy(x => x.PetStageUse)
                    .ThenBy(x => x.TypeAnimation)
                    .Select(x => new PetAnimationDto
                    {
                        PetAnimationId = x.PetAnimationId,
                        TypeAnimation = x.TypeAnimation,
                        PetStageUse = x.PetStageUse,
                        AnimationUrl = x.AnimationUrl,
                        IsActive = x.IsActive
                    }).ToList()
            };
        }
        public async Task UpdatePetAsync(Guid petId, UpdatePetRequest request)
        {
            var pet = await _petRepository.GetPetByIdAsync(petId);

            if (pet == null)
                throw new NotFoundException("Pet not found.");

            pet.PetName = request.PetName;

            pet.LifeForce = request.LifeForce;
            pet.Energy = request.Energy;
            pet.Bond = request.Bond;
            pet.Exp = request.Exp;

            pet.LifeForceRate = request.LifeForceRate;
            pet.EnergyRate = request.EnergyRate;
            pet.BondRate = request.BondRate;
            pet.ExpRate = request.ExpRate;

            pet.UpdatedAt = DateTime.UtcNow;

            _Pet.Update(pet);

            await _repository.SaveAsync();
        }
        private static string ResolveAnimationType(UserPet pet)
        {
            var energyPercent = pet.PetEnergy > 0
                ? (double)pet.CurrentPetEnergy / pet.PetEnergy
                : 0;
            var bondPercent = pet.PetBond > 0
                ? (double)pet.CurrentPetBond / pet.PetBond
                : 0;
            var lifePercent = pet.PetLifeForce > 0
                ? (double)pet.CurrentPetLifeForce / pet.PetLifeForce
                : 0;

            if (energyPercent <= 0.2) return "sleep";
            if (lifePercent <= 0.2) return "hungry";
            if (bondPercent <= 0.2) return "sad";
            if (energyPercent >= 0.8
                && bondPercent >= 0.8
                && lifePercent >= 0.8)
            {
                return "happy";
            }

            return "idle";
        }

        private static string ResolveAffinityCode(Pet pet)
        {
            var configured = pet.PvpAffinityCode?.Trim().ToLowerInvariant();
            if (configured is "sprout" or "dawn" or "moonlight" or "warm_sun")
                return configured;

            var name = pet.PetName.ToLowerInvariant();
            if (name.Contains("bình minh")) return "dawn";
            if (name.Contains("ánh trăng")) return "moonlight";
            if (name.Contains("nắng ấm")) return "warm_sun";
            return "sprout";

        }
    }
}

