namespace DAL.DTO;

public class UserPreferenceResponse
{
    public string LanguageCode { get; set; } = string.Empty;

    public string ThemeCode { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
