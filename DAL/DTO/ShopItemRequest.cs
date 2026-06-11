using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class ShopItemRequest
    {
        public Guid ItemId { get; set; }

        public int ItemQuantity { get; set; }

        public int PriceAmount { get; set; }
    }
}
