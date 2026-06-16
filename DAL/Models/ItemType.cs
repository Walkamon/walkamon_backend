using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class ItemType
{
    public Guid ItemTypeId { get; set; }

    public string ItemTypeName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
