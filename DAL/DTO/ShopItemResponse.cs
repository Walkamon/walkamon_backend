using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class ShopItemResponse
    {
        public Guid ShopItemId { get; set; }

        public Guid ItemId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string? Image { get; set; }

        public string? ItemTypeName { get; set; }

        public string? EffectTypeCode { get; set; }

        public string? Description { get; set; }

        public int PriceAmount { get; set; }

        public bool IsActive { get; set; }
    }
}
