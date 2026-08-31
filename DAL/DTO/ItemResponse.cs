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
        public string? ContentCode { get; set; }
        public string? ItemNameVi { get; set; }
        public string? ItemNameEn { get; set; }
        public string? DescriptionVi { get; set; }
        public string? DescriptionEn { get; set; }
        public string? TranslationStatusCode { get; set; }
    }
    

}
