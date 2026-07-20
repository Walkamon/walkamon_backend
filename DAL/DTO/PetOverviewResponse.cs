namespace DAL.DTO;

public sealed class PetOverviewResponse
{
    public Guid PetId { get; set; }
    public string Nickname { get; set; } = null!;
    public string FormName { get; set; } = null!;
    public string AffinityCode { get; set; } = null!;
    public int Level { get; set; }
    public int CurrentExp { get; set; }
    public int MaxExp { get; set; }
    public int CurrentEnergy { get; set; }
    public int MaxEnergy { get; set; }
    public int CurrentLifeForce { get; set; }
    public int MaxLifeForce { get; set; }
    public int CurrentBond { get; set; }
    public int MaxBond { get; set; }
    public int StageNo { get; set; }
    public string StageName { get; set; } = null!;
    public string AnimationType { get; set; } = null!;
    public bool CanEvolve { get; set; }
    public int? NextEvolutionLevel { get; set; }
}
