using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class PetAnimation
{
    public Guid PetAnimationId { get; set; }

    public Guid PetId { get; set; }

    public string? AnimationUrl { get; set; }

    public string TypeAnimation { get; set; } = null!;

    public int PetStageUse { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Pet Pet { get; set; } = null!;
}
