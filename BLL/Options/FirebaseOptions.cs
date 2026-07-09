namespace BLL.Options;

public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string? ProjectId { get; set; }

    public string? ServiceAccountPath { get; set; }

    public string? ServiceAccountJsonBase64 { get; set; }
}
