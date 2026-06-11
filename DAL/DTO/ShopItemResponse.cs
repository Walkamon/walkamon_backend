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

        public string ItemName { get; set; }

        public int ItemQuantity { get; set; }

        public int PriceAmount { get; set; }

        public bool IsActive { get; set; }
    }
}
