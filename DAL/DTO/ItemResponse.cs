using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.DTO
{
    public class ItemResponse
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string ItemTypeName { get; set; } = null!;
        public string? EffectTypeCode { get; set; }
        public int? EffectValue { get; set; }
    
        public bool IsActive { get; set; }

        public string? Image { get; set; }
    }
    

}
