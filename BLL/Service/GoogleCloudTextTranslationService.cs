using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BLL.Interfaces;
using BLL.Options;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BLL.Service;

/// <summary>
/// Optional Google Cloud Translation v3 adapter. Missing configuration is a
/// safe fallback, so existing deployments can roll out the schema first.
/// </summary>
public sealed class GoogleCloudTextTranslationService : ITextTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly TranslationOptions _options;
    private readonly ILogger<GoogleCloudTextTranslationService> _logger;
    private readonly Dictionary<string, TranslatedTextPair> _cache = new(StringComparer.Ordinal);

    public GoogleCloudTextTranslationService(
        HttpClient httpClient,
        IOptions<TranslationOptions> options,
        ILogger<GoogleCloudTextTranslationService> logger)
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

        // Short labels and disabled providers are intentionally non-blocking.
        if (!_options.Enabled || string.IsNullOrWhiteSpace(sourceText))
            return Cache(hash, new TranslatedTextPair("vi", sourceText, sourceText, sourceText, "fallback", hash, null));

        try
        {
            var credential = LoadCredential();
            if (credential == null || string.IsNullOrWhiteSpace(_options.ProjectId))
                return Cache(hash, Fallback(sourceText, hash));

            var tokenAccess = credential as ITokenAccess;
            var token = tokenAccess == null
                ? null
                : await tokenAccess.GetAccessTokenForRequestAsync(null, cancellationToken);
            if (string.IsNullOrWhiteSpace(token)) return Cache(hash, Fallback(sourceText, hash));

            var sourceLanguage = DetectSourceLanguage(sourceText);
            var vi = await TranslateOneAsync(token, sourceText, sourceLanguage, "vi", cancellationToken);
            var en = await TranslateOneAsync(token, sourceText, sourceLanguage, "en", cancellationToken);
            if (string.IsNullOrWhiteSpace(vi) || string.IsNullOrWhiteSpace(en))
                return Cache(hash, Fallback(sourceText, hash));
            if (!PreservesTokens(sourceText, vi) || !PreservesTokens(sourceText, en))
                return Cache(hash, Fallback(sourceText, hash));

            var pair = new TranslatedTextPair(sourceLanguage, sourceText, vi, en, "translated", hash, DateTime.UtcNow);
            return Cache(hash, pair);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Editorial translation unavailable; retaining source text.");
            return Cache(hash, Fallback(sourceText, hash));
        }
    }

    private async Task<string?> TranslateOneAsync(
        string token,
        string text,
        string sourceLanguage,
        string target,
        CancellationToken cancellationToken)
    {
        var project = Uri.EscapeDataString(_options.ProjectId!);
        var location = Uri.EscapeDataString(_options.Location);
        var requestBody = new Dictionary<string, object?>
        {
            ["sourceLanguageCode"] = sourceLanguage,
            ["targetLanguageCode"] = target,
            ["contents"] = new[] { text },
            ["mimeType"] = "text/plain"
        };
        var glossary = GlossaryConfig(project, location, target);
        if (glossary != null) requestBody["glossaryConfig"] = glossary;

        var attempts = Math.Clamp(_options.MaxRetries, 0, 2) + 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://translation.googleapis.com/v3/projects/{project}/locations/{location}:translateText");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken));
                return document.RootElement.GetProperty("translations")[0]
                    .GetProperty("translatedText").GetString();
            }

            if (!IsTransient(response.StatusCode) || attempt == attempts - 1) return null;
            await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), cancellationToken);
        }
        return null;
    }

    private Dictionary<string, object?>? GlossaryConfig(
        string project,
        string location,
        string target)
    {
        if (string.IsNullOrWhiteSpace(_options.GlossaryId)) return null;
        var glossaryProject = Uri.EscapeDataString(
            string.IsNullOrWhiteSpace(_options.GlossaryProjectId)
                ? _options.ProjectId!
                : _options.GlossaryProjectId!);
        var glossaryLocation = Uri.EscapeDataString(
            string.IsNullOrWhiteSpace(_options.GlossaryLocation)
                ? _options.Location
                : _options.GlossaryLocation!);
        return new Dictionary<string, object?>
        {
            ["glossary"] =
                $"projects/{glossaryProject}/locations/{glossaryLocation}/glossaries/{Uri.EscapeDataString(_options.GlossaryId!)}",
            ["ignoreCase"] = true
        };
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.RequestTimeout ||
        (int)statusCode == 429 ||
        (int)statusCode >= 500;

    private static string DetectSourceLanguage(string text)
    {
        if (text.Length < 16) return "vi";
        if (Regex.IsMatch(text, "[ăâđêôơưàáạảãèéẹẻẽìíịỉĩòóọỏõùúụủũỳýỵỷỹ]", RegexOptions.IgnoreCase))
            return "vi";
        var englishWords = Regex.Matches(text.ToLowerInvariant(), "\\b(the|and|for|your|with|item|speed|reward|mission|player)\\b");
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
        return placeholders.All(token => normalized.Contains(token.ToLowerInvariant(), StringComparison.Ordinal)) &&
               numbers.All(token => normalized.Contains(token.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private GoogleCredential? LoadCredential()
    {
        if (!string.IsNullOrWhiteSpace(_options.CredentialsJsonBase64))
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(_options.CredentialsJsonBase64));
            return CredentialFactory
                .FromJson<ServiceAccountCredential>(json)
                .ToGoogleCredential()
                .CreateScoped("https://www.googleapis.com/auth/cloud-translation");
        }
        if (!string.IsNullOrWhiteSpace(_options.CredentialsPath) && File.Exists(_options.CredentialsPath))
            return CredentialFactory
                .FromFile<ServiceAccountCredential>(_options.CredentialsPath)
                .ToGoogleCredential()
                .CreateScoped("https://www.googleapis.com/auth/cloud-translation");
        return null;
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
