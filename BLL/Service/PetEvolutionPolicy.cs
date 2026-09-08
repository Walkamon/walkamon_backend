namespace BLL.Service;

/// <summary>Eligibility thresholds for the supported Lumina branches.</summary>
public static class PetEvolutionPolicy
{
    public const int FirstEvolutionLevel = 15;
    public const int SecondEvolutionLevel = 30;

    public static int RequiredLevel(string? affinity, int stageNo, int configuredLevel)
    {
        if (affinity?.Trim().ToLowerInvariant() is "dawn" or "moonlight" or "warm_sun")
            return stageNo switch
            {
                1 => FirstEvolutionLevel,
                2 => SecondEvolutionLevel,
                _ => configuredLevel
            };
        return configuredLevel;
    }
}
