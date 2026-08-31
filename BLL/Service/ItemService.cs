using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BLL.Exceptions;
namespace BLL.Service
{
    public class ItemService : IItemService
    {
        private static readonly HashSet<string> SupportedPvpEffects = new(StringComparer.OrdinalIgnoreCase)
        {
            "pvp_speed_up",
            "pvp_speed_down",
            "pvp_cleanse",
            "pvp_shield"
        };

        private readonly IGenericRepository<Item> _itemRepository;
        private readonly IGenericRepository<ItemType> _itemTypeRepository;
        private readonly ITextTranslationService? _translationService;

        public ItemService(
            IGenericRepository<Item> itemRepository,
            IGenericRepository<ItemType> itemTypeRepository,
            ITextTranslationService? translationService = null)
        {
            _itemRepository = itemRepository;
            _itemTypeRepository = itemTypeRepository;
            _translationService = translationService;
        }
        public async Task<ItemResponse?> CreateAsync(
     CreateItemRequest dto,
     string? imageUrl)
        {
            ValidateEffectCode(dto.EffectTypeCode, isActive: true);
            var itemType = await _itemTypeRepository.GetByIdAsync(dto.ItemTypeId);

            if (itemType == null)
                throw new NotFoundException("Item type not found");

            bool exists = await _itemRepository.AnyAsync(x =>
                x.ItemName == dto.ItemName &&
                x.ItemTypeId == dto.ItemTypeId);

            if (exists)
                throw new ConflictException("Item already exists");

            var item = new Item
            {
                ItemId = Guid.NewGuid(),
                ItemName = dto.ItemName,
                ImgUrl = imageUrl,
                ItemTypeId = dto.ItemTypeId,
                EffectTypeCode = dto.EffectTypeCode,
                EffectValue = dto.EffectValue,
                Description = dto.Description,
                IsActive = true
            };

            await ApplyTranslationAsync(item, dto.ItemName, dto.Description);

            await _itemRepository.AddAsync(item);
            await _itemRepository.SaveAsync();

            return ToResponse(item, itemType);
        }

        public async Task DeleteAsync(Guid id)
        {
            await UpdateStatusAsync(id, false);
        }

        public async Task UpdateStatusAsync(Guid id, bool isActive)
        {
            var item = await _itemRepository.GetByIdAsync(id);

            if (item == null)
                throw new NotFoundException("Item not found");

            item.IsActive = isActive;
            _itemRepository.Update(item);
            await _itemRepository.SaveAsync();
        }

        public async Task<IEnumerable<ItemResponse>> GetAllAsync()
        {
            var items = await _itemRepository.GetAllAsync();
            var itemTypes = await _itemTypeRepository.GetAllAsync();

            return items.Select(item =>
            {
                var itemType = itemTypes.FirstOrDefault(x =>
                    x.ItemTypeId == item.ItemTypeId);

                return ToResponse(item, itemType);
            });
        }

        public async Task<IEnumerable<ItemResponse>> GetAllActiveAsync()
        {
            var items = (await _itemRepository.GetAllAsync())
                .Where(x => x.IsActive);

            var itemTypes = (await _itemTypeRepository.GetAllAsync())
                .Where(x => x.IsActive);

            return items.Select(item =>
            {
                var itemType = itemTypes.FirstOrDefault(x =>
                    x.ItemTypeId == item.ItemTypeId);

                return ToResponse(item, itemType);
            });
        }
        public async Task<Item?> GetByIdAsync(Guid id)
        {
            return await _itemRepository.GetByIdAsync(id);
        }

        public async Task<ItemResponse> UpdateAsync(
     Guid id,
     UpdateItemRequest dto,
     string? imageUrl)
        {
            ValidateEffectCode(dto.EffectTypeCode, dto.IsActive);
            var item = await _itemRepository.GetByIdAsync(id);

            if (item == null)
                throw new Exception("Item not found");

            bool exists = await _itemRepository.AnyAsync(x =>
                x.ItemId != id &&
                x.ItemName == dto.ItemName &&
                x.ItemTypeId == dto.ItemTypeId);

            if (exists)
                throw new Exception("Item already exists");

            item.ItemName = dto.ItemName;
            item.ItemTypeId = dto.ItemTypeId;
            item.EffectTypeCode = dto.EffectTypeCode;
            item.EffectValue = dto.EffectValue;
            item.Description = dto.Description;
            item.IsActive = dto.IsActive;

            await ApplyTranslationAsync(item, dto.ItemName, dto.Description);

            if (!string.IsNullOrEmpty(imageUrl))
            {
                item.ImgUrl = imageUrl;
            }
            var itemType = await _itemTypeRepository.GetByIdAsync(dto.ItemTypeId);
            _itemRepository.Update(item);
            await _itemRepository.SaveAsync();

            return ToResponse(item, itemType);
        }

        private async Task ApplyTranslationAsync(Item item, string name, string? description)
        {
            item.ContentCode = "item.editorial";
            if (_translationService == null)
            {
                item.ItemNameVi = name.Trim();
                item.ItemNameEn = name.Trim();
                item.DescriptionVi = description?.Trim();
                item.DescriptionEn = description?.Trim();
                item.SourceLanguageCode = "vi";
                item.TranslationStatusCode = "fallback";
                return;
            }

            var namePair = await _translationService.TranslateAsync(name);
            item.ItemNameVi = namePair.Vietnamese;
            item.ItemNameEn = namePair.English;
            if (!string.IsNullOrWhiteSpace(description))
            {
                var descriptionPair = await _translationService.TranslateAsync(description);
                item.DescriptionVi = descriptionPair.Vietnamese;
                item.DescriptionEn = descriptionPair.English;
                item.TranslationStatusCode = namePair.StatusCode == "translated" && descriptionPair.StatusCode == "translated"
                    ? "translated"
                    : "fallback";
            }
            else
            {
                item.DescriptionVi = null;
                item.DescriptionEn = null;
                item.TranslationStatusCode = namePair.StatusCode;
            }
            item.SourceLanguageCode = namePair.SourceLanguageCode;
            item.TranslationSourceHash = namePair.SourceHash;
            item.TranslatedAt = namePair.TranslatedAt;
        }

        private static void ValidateEffectCode(string? effectTypeCode, bool isActive)
        {
            var normalized = effectTypeCode?.Trim();
            if (!isActive || string.IsNullOrWhiteSpace(normalized) ||
                !normalized.StartsWith("pvp_", StringComparison.OrdinalIgnoreCase))
                return;

            if (!SupportedPvpEffects.Contains(normalized))
            {
                throw new BadRequestException(
                    "Unsupported PvP item effect.",
                    "UNSUPPORTED_ITEM_EFFECT",
                    new Dictionary<string, object?> { ["effectTypeCode"] = normalized });
            }
        }

        private static ItemResponse ToResponse(Item item, ItemType? itemType) => new()
        {
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            ItemTypeName = itemType?.ItemTypeName ?? string.Empty,
            Image = item.ImgUrl,
            EffectTypeCode = item.EffectTypeCode,
            EffectValue = item.EffectValue,
            IsActive = item.IsActive,
            ContentCode = item.ContentCode,
            ItemNameVi = item.ItemNameVi,
            ItemNameEn = item.ItemNameEn,
            DescriptionVi = item.DescriptionVi,
            DescriptionEn = item.DescriptionEn,
            TranslationStatusCode = item.TranslationStatusCode
        };
    }
}
