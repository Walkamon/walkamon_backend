namespace BLL.Interfaces;

public sealed record TranslatedTextPair(
    string SourceLanguageCode,
    string SourceText,
    string Vietnamese,
    string English,
    string StatusCode,
    string SourceHash,
    DateTime? TranslatedAt);

/// <summary>Translates editorial text at write time, never on a player request.</summary>
public interface ITextTranslationService
{
    Task<TranslatedTextPair> TranslateAsync(
        string sourceText,
        CancellationToken cancellationToken = default);
}
