using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BLL.Interfaces;
using BLL.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BLL.Service;

/// <summary>
/// Calls the private LibreTranslate sidecar. Translation runs only when
/// editorial content is saved; player-facing read requests never wait for it.
/// </summary>
public sealed class LibreTranslateTextTranslationService : ITextTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly TranslationOptions _options;
    private readonly ILogger<LibreTranslateTextTranslationService> _logger;
    private readonly Dictionary<string, TranslatedTextPair> _cache = new(StringComparer.Ordinal);

    public LibreTranslateTextTranslationService(
        HttpClient httpClient,
        IOptions<TranslationOptions> options,
        ILogger<LibreTranslateTextTranslationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 30));
    }

    public async Task<TranslatedTextPair> TranslateAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        sourceText = sourceText.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceText)));
        if (_cache.TryGetValue(hash, out var cached)) return cached;

        if (!_options.Enabled || string.IsNullOrWhiteSpace(sourceText))
            return Cache(hash, Fallback(sourceText, hash));

        try
        {
            if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri) ||
                baseUri.Scheme is not ("http" or "https"))
            {
                _logger.LogWarning("LibreTranslate base URL is invalid; retaining source text.");
                return Cache(hash, Fallback(sourceText, hash));
            }

            var sourceLanguage = DetectSourceLanguage(sourceText);
            var vietnamese = sourceLanguage == "vi"
                ? sourceText
                : await TranslateOneAsync(baseUri, sourceText, sourceLanguage, "vi", cancellationToken);
            var english = sourceLanguage == "en"
                ? sourceText
                : await TranslateOneAsync(baseUri, sourceText, sourceLanguage, "en", cancellationToken);

            if (string.IsNullOrWhiteSpace(vietnamese) || string.IsNullOrWhiteSpace(english) ||
                !PreservesTokens(sourceText, vietnamese) || !PreservesTokens(sourceText, english))
            {
                return Cache(hash, Fallback(sourceText, hash));
            }

            return Cache(
                hash,
                new TranslatedTextPair(
                    sourceLanguage,
                    sourceText,
                    vietnamese,
                    english,
                    "translated",
                    hash,
                    DateTime.UtcNow));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "LibreTranslate is unavailable; retaining source text.");
            return Cache(hash, Fallback(sourceText, hash));
        }
    }

    private async Task<string?> TranslateOneAsync(
        Uri baseUri,
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["q"] = text,
            ["source"] = sourceLanguage,
            ["target"] = targetLanguage,
            ["format"] = "text"
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            body["api_key"] = _options.ApiKey;

        var endpoint = new Uri(baseUri, "/translate");
        var attempts = Math.Clamp(_options.MaxRetries, 0, 2) + 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json")
            };
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken));
                if (document.RootElement.TryGetProperty("translatedText", out var translated))
                    return WebUtility.HtmlDecode(translated.GetString());
                return null;
            }

            if (!IsTransient(response.StatusCode) || attempt == attempts - 1)
            {
                _logger.LogWarning(
                    "LibreTranslate request failed with status {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150 * (attempt + 1)), cancellationToken);
        }

        return null;
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        (int)statusCode == 429 ||
        (int)statusCode >= 500;

    private static string DetectSourceLanguage(string text)
    {
        if (text.Length < 16) return "vi";
        if (Regex.IsMatch(text, "[ăâđêôơưàáạảãèéẹẻẽìíịỉĩòóọỏõùúụủũỳýỵỷỹ]", RegexOptions.IgnoreCase))
            return "vi";
        var englishWords = Regex.Matches(
            text.ToLowerInvariant(),
            "\\b(the|and|for|your|with|item|speed|reward|mission|player)\\b");
        return englishWords.Count >= 2 ? "en" : "vi";
    }

    private static bool PreservesTokens(string source, string translated)
    {
        var placeholders = Regex.Matches(source, @"\{[^{}]+\}")
            .Select(x => x.Value)
            .ToList();
        var numbers = Regex.Matches(source, @"\d+(?:[.,]\d+)?%?")
            .Select(x => x.Value.Replace(',', '.'))
            .ToList();
        var normalized = translated.Replace(',', '.').ToLowerInvariant();
        return placeholders.All(token =>
                   normalized.Contains(token.ToLowerInvariant(), StringComparison.Ordinal)) &&
               numbers.All(token =>
                   normalized.Contains(token.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static TranslatedTextPair Fallback(string text, string hash) =>
        new("vi", text, text, text, "fallback", hash, null);

    private TranslatedTextPair Cache(string hash, TranslatedTextPair value)
    {
        if (_cache.Count > 512) _cache.Clear();
        _cache[hash] = value;
        return value;
    }
}
