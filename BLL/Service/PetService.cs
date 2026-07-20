using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
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
        public PetService(
           IPetRepository petRepository, IGenericRepository<UserPet> repository ,
           IPetInteractionRepository petInteractionRepository, IGenericRepository<PetInteraction> PetInteraction,
          IPetEvolutionHistoryRepository petEvolutionHistoryRepository,
          IGenericRepository<PetEvolutionHistory> PetHistory,
          IGenericRepository<Pet> Pet,
          ISystemSettingRepository systemSettingRepository
            )
        {
            _Pet = Pet;
            _PetHistory = PetHistory;
            _PetEvolutionHistory = petEvolutionHistoryRepository;
            _PetInteraction = PetInteraction;
            _interactionRepository = petInteractionRepository;
            _petRepository = petRepository;
            _repository = repository;
            _systemSettingRepository = systemSettingRepository;
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

                EnergyUpdatedAt = GetVietnamNow(),
                BondUpdatedAt = GetVietnamNow(),
                LifeForceUpdatedAt = GetVietnamNow(),
                ExpUpdatedAt = GetVietnamNow()

            };
            var existed = await _repository.AnyAsync(x => x.UserId == userId);

            if (existed)
                throw new BadRequestException("User already has a pet.");
            await _repository.AddAsync(userPet);
            await _repository.SaveAsync();
        }
        public async Task<PetStatusResponse> GetPetStatusAsync(Guid currentUserId)
        {
            var pet = await _petRepository.GetUserPetAsync(currentUserId);

            if (pet == null)
                throw new NotFoundException("Pet not found.");

            UpdateEnergy(pet);

            UpdateBond(pet);

            UpdateLifeForce(pet);

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
       
        public async Task<LevelPetResponse> AddPetExpAsync(Guid userId)
        {
            var setting = await _systemSettingRepository.GetByKeyAsync("StepToExpRate");
          

            var userPet = await _petRepository.GetUserPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            var pet = await _petRepository.GetPetAsync(userPet.PetId);

            if (pet == null)
                throw new NotFoundException("Pet template not found.");

           
            UpdateEnergy(userPet);
            UpdateBond(userPet);
            UpdateLifeForce(userPet);

            bool levelUp = false;

            
            userPet.CurrentPetExp += int.Parse(setting.SettingValue);

            while (userPet.CurrentPetExp >= userPet.PetExp)
            {
                userPet.CurrentPetExp -= userPet.PetExp;

                LevelUp(userPet, pet);

                levelUp = true;
            }

            _repository.Update(userPet);

            await _repository.SaveAsync();

            return new LevelPetResponse
            {
                Level = userPet.Level,
                CurrentExp = userPet.CurrentPetExp,
                MaxExp = userPet.PetExp,
                LevelUp = levelUp
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

          
            UpdateEnergy(userPet);
            UpdateBond(userPet);
            UpdateLifeForce(userPet);

          
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

        public async Task<PetStatusResponse>  FeedSpiritAsync(Guid userId)
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

           
            UpdateEnergy(userPet);
            UpdateBond(userPet);
            UpdateLifeForce(userPet);

          
            if (userPet.CurrentPetLifeForce >= userPet.PetLifeForce)
                throw new BadRequestException("Pet life force is already full.");

          
            if (interaction.Count >= 5)
                throw new BadRequestException("You have reached the maximum feed limit today.");

          
            userPet.CurrentPetLifeForce = Math.Min(
                userPet.PetLifeForce,
                userPet.CurrentPetLifeForce + 20);

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
                MaxLifeForce = userPet.PetLifeForce
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

            foreach (var pet in pets)
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

            var firstStage = await _petRepository
                .GetFirstStageAsync(petId);

            if (firstStage == null)
                throw new BadRequestException(
                    "Evolution stage not found.");

            
            userPet.PetId = pet.PetId;

          
            userPet.PetEnergy = pet.Energy;
            userPet.PetBond = pet.Bond;
            userPet.PetLifeForce = pet.LifeForce;
            userPet.PetExp = pet.Exp;

            userPet.CurrentPetEnergy = pet.Energy;
            userPet.CurrentPetBond = pet.Bond;
            userPet.CurrentPetLifeForce = pet.LifeForce;
            userPet.CurrentPetExp = 0;

            userPet.EnergyUpdatedAt = GetVietnamNow();
            userPet.BondUpdatedAt = GetVietnamNow();
            userPet.LifeForceUpdatedAt = GetVietnamNow();
            userPet.ExpUpdatedAt = GetVietnamNow();

            _repository.Update(userPet);

            await _PetHistory.AddAsync(new PetEvolutionHistory
            {
                EvolutionId = Guid.NewGuid(),
                UserId = userId,
                StageId = firstStage.StageId,
                Level = userPet.Level,
                EvolvedAt = GetVietnamNow()
            });

            await _repository.SaveAsync();
        }
        private void UpdateEnergy(UserPet pet)
        {
            var now = GetVietnamNow();

            var elapsedMinutes =
                (int)(now - pet.EnergyUpdatedAt).TotalMinutes;

            if (elapsedMinutes <= 0)
                return;

            pet.CurrentPetEnergy = Math.Min(
                pet.PetEnergy,
                pet.CurrentPetEnergy + elapsedMinutes);

            pet.EnergyUpdatedAt =
                pet.EnergyUpdatedAt.AddMinutes(elapsedMinutes);
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
                EvolvedAt = GetVietnamNow()
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
        private void UpdateBond(UserPet pet)
        {
            var now = GetVietnamNow();

            var elapsedMinutes =
                (int)(now - pet.BondUpdatedAt).TotalMinutes;

            int cycles = elapsedMinutes / 20;

            if (cycles <= 0)
                return;

            pet.CurrentPetBond = Math.Max(
                0,
                pet.CurrentPetBond - cycles * 10);

            pet.BondUpdatedAt =
                pet.BondUpdatedAt.AddMinutes(cycles * 20);
        }

        private void UpdateLifeForce(UserPet pet)
        {
            var now = GetVietnamNow();

            var elapsedMinutes =
                (int)(now - pet.LifeForceUpdatedAt).TotalMinutes;

            int cycles = elapsedMinutes / 20;

            if (cycles <= 0)
                return;

            pet.CurrentPetLifeForce = Math.Max(
                0,
                pet.CurrentPetLifeForce - cycles * 10);

            pet.LifeForceUpdatedAt =
                pet.LifeForceUpdatedAt.AddMinutes(cycles * 20);
        }
        private void LevelUp(UserPet userPet, Pet pet)
        {
            userPet.Level++;

           
            userPet.PetEnergy = (int)Math.Ceiling(userPet.PetEnergy * pet.EnergyRate);

            userPet.PetBond = (int)Math.Ceiling(userPet.PetBond * pet.BondRate);

            userPet.PetLifeForce = (int)Math.Ceiling(userPet.PetLifeForce * pet.LifeForceRate);

           
            userPet.PetExp = (int)Math.Ceiling(userPet.PetExp * pet.ExpRate);

           
            userPet.CurrentPetEnergy = userPet.PetEnergy;
            userPet.CurrentPetBond = userPet.PetBond;
            userPet.CurrentPetLifeForce = userPet.PetLifeForce;

            userPet.ExpUpdatedAt = GetVietnamNow();
        }
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

            UpdateEnergy(userPet);
            UpdateBond(userPet);
            UpdateLifeForce(userPet);

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

            string animationType;

            double energyPercent =
                (double)userPet.CurrentPetEnergy / userPet.PetEnergy;

            double bondPercent =
                (double)userPet.CurrentPetBond / userPet.PetBond;

            double lifePercent =
                (double)userPet.CurrentPetLifeForce / userPet.PetLifeForce;

            if (energyPercent <= 0.2)
            {
                animationType = "sleep";
            }
            else if (lifePercent <= 0.2)
            {
                animationType = "hungry";
            }
            else if (bondPercent <= 0.2)
            {
                animationType = "sad";
            }
            else if (
                energyPercent >= 0.8 &&
                bondPercent >= 0.8 &&
                lifePercent >= 0.8)
            {
                animationType = "happy";
            }
            else
            {
                animationType = "idle";
            }

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
            var userPet = await _petRepository.GetUserPetAsync(userId);

            if (userPet == null)
                throw new NotFoundException("Pet not found.");

            return new UserPetNameResponse
            {
                PetId = userPet.PetId,
                PetName = userPet.PetName
            };
        }
    }
}

