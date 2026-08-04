namespace BLL.Options;

public sealed class PvpMatchmakingOptions
{
    public const string SectionName = "PvpMatchmaking";

    public bool Enabled { get; set; } = true;
    public bool ShadowMode { get; set; }
}
