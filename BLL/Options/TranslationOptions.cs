namespace BLL.Options;

public sealed class TranslationOptions
{
    public const string SectionName = "Translation";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "libretranslate";
    public string BaseUrl { get; set; } = "http://libretranslate:5000";
    public string? ApiKey { get; set; }
    public string? ProjectId { get; set; }
    public string? CredentialsPath { get; set; }
    public string? CredentialsJsonBase64 { get; set; }
    public string Location { get; set; } = "global";
    public int TimeoutSeconds { get; set; } = 5;
    public int MaxRetries { get; set; } = 2;
    public string? GlossaryId { get; set; }
    public string? GlossaryProjectId { get; set; }
    public string? GlossaryLocation { get; set; }
}
