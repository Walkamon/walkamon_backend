using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BLL.Interfaces;
using BLL.Options;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace BLL.Service;

public sealed class DevelopmentAttestationVerifier : IAppAttestationVerifier
{
    private readonly bool _enabled;

    public DevelopmentAttestationVerifier(string environmentName) =>
        _enabled = string.Equals(environmentName, "Development", StringComparison.Ordinal);

    public Task<AppAttestationResult> VerifyAsync(
        AppAttestationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Task.FromResult(new AppAttestationResult(
                false, "rejected", null, null, null, "attestation_token_required"));
        var expected = $"DEV_BYPASS:{request.PayloadHash}";
        var valid = _enabled && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(request.Token),
            Encoding.UTF8.GetBytes(expected));
        return Task.FromResult(new AppAttestationResult(
            valid,
            valid ? "development_bypass" : "rejected",
            "development",
            request.ServerTime,
            null,
            valid ? null : _enabled ? "invalid_development_bypass" : "development_bypass_forbidden"));
    }
}

public sealed class PlayIntegrityAttestationVerifier : IAppAttestationVerifier
{
    private const string Scope = "https://www.googleapis.com/auth/playintegrity";
    private readonly HttpClient _httpClient;
    private readonly StepValidationOptions _options;
    private readonly GoogleCredential _credential;

    public PlayIntegrityAttestationVerifier(
        HttpClient httpClient,
        IOptions<StepValidationOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        if (_options.StrictAttestation)
        {
            if (string.IsNullOrWhiteSpace(_options.AndroidPackageName))
                throw new InvalidOperationException("StepValidation:AndroidPackageName is required in strict mode.");
            if (string.IsNullOrWhiteSpace(_options.GoogleCredentialPath) ||
                !File.Exists(_options.GoogleCredentialPath))
                throw new InvalidOperationException("StepValidation:GoogleCredentialPath is required and must exist in strict mode.");
        }
        _credential = GoogleCredential
            .FromFile(_options.GoogleCredentialPath!)
            .CreateScoped(Scope);
    }

    public async Task<AppAttestationResult> VerifyAsync(
        AppAttestationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Rejected("attestation_token_required");
        if (request.Token.StartsWith("DEV_BYPASS:", StringComparison.Ordinal))
            return Rejected("development_bypass_forbidden");
        if (!string.Equals(request.PlatformCode, "android", StringComparison.Ordinal))
            return Rejected("platform_not_supported");

        var accessToken = await _credential.UnderlyingCredential
            .GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://playintegrity.googleapis.com/v1/{Uri.EscapeDataString(_options.AndroidPackageName!)}:decodeIntegrityToken");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Content = new StringContent(
            JsonSerializer.Serialize(new { integrity_token = request.Token }),
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return new(false, "rate_limited", null, null, json, "play_integrity_rate_limited");
        if (!response.IsSuccessStatusCode)
            return new(false, "decode_failed", null, null, json, $"play_integrity_http_{(int)response.StatusCode}");
        return PlayIntegrityVerdictValidator.Validate(json, request.PayloadHash, request.ServerTime, _options);
    }

    private static AppAttestationResult Rejected(string reason) =>
        new(false, "rejected", null, null, null, reason);
}

public static class PlayIntegrityVerdictValidator
{
    public static AppAttestationResult Validate(
        string json,
        string expectedHash,
        DateTime serverTime,
        StepValidationOptions options)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var payload = document.RootElement.GetProperty("tokenPayloadExternal");
            var request = payload.GetProperty("requestDetails");
            var package = request.GetProperty("requestPackageName").GetString();
            var hash = request.GetProperty("requestHash").GetString();
            var timestampMs = request.GetProperty("timestampMillis").ValueKind == JsonValueKind.String
                ? long.Parse(request.GetProperty("timestampMillis").GetString()!)
                : request.GetProperty("timestampMillis").GetInt64();
            var verdictTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;

            if (!string.Equals(package, options.AndroidPackageName, StringComparison.Ordinal))
                return Result("package_mismatch");
            if (!string.Equals(hash, expectedHash, StringComparison.Ordinal))
                return Result("request_hash_mismatch");
            if (verdictTime > serverTime.AddSeconds(2) ||
                verdictTime < serverTime.AddSeconds(-options.AttestationMaxAgeSeconds))
                return Result("verdict_timestamp_invalid");

            var appVerdict = payload.GetProperty("appIntegrity").GetProperty("appRecognitionVerdict").GetString();
            var appIntegrity = payload.GetProperty("appIntegrity");
            if (string.Equals(options.AppRecognitionMode, "certificate_allowlist", StringComparison.OrdinalIgnoreCase))
            {
                if (appVerdict is not ("PLAY_RECOGNIZED" or "UNRECOGNIZED_VERSION"))
                    return Result("app_integrity_unevaluated");
                var appPackage = appIntegrity.TryGetProperty("packageName", out var packageElement)
                    ? packageElement.GetString()
                    : null;
                if (!string.Equals(appPackage, options.AndroidPackageName, StringComparison.Ordinal))
                    return Result("app_package_mismatch");
                if (!TryReadVersionCode(appIntegrity, out var versionCode) ||
                    versionCode < options.MinimumVersionCode)
                    return Result("app_version_not_allowed");
                if (!CertificateAllowed(appIntegrity, options.AllowedCertificateSha256Hex))
                    return Result("app_certificate_not_allowed");
            }
            else if (appVerdict != "PLAY_RECOGNIZED")
            {
                return Result("app_not_play_recognized");
            }
            var deviceVerdicts = payload.GetProperty("deviceIntegrity")
                .GetProperty("deviceRecognitionVerdict")
                .EnumerateArray().Select(x => x.GetString()).ToArray();
            if (options.RequireDeviceIntegrity &&
                !deviceVerdicts.Contains("MEETS_DEVICE_INTEGRITY"))
                return Result("device_integrity_failed");
            if (options.RequireLicensingVerdict)
            {
                var licensing = payload.GetProperty("accountDetails").GetProperty("appLicensingVerdict").GetString();
                if (licensing != "LICENSED") return Result("licensing_failed");
            }
            return new(true, "verified", package, verdictTime, json, null);

            AppAttestationResult Result(string reason) =>
                new(false, "rejected", package, verdictTime, json, reason);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException)
        {
            return new(false, "rejected", null, null, json, "malformed_verdict");
        }
    }

    private static bool TryReadVersionCode(JsonElement appIntegrity, out int versionCode)
    {
        versionCode = 0;
        if (!appIntegrity.TryGetProperty("versionCode", out var value)) return false;
        return value.ValueKind == JsonValueKind.String
            ? int.TryParse(value.GetString(), out versionCode)
            : value.TryGetInt32(out versionCode);
    }

    private static bool CertificateAllowed(JsonElement appIntegrity, string? configuredHex)
    {
        var allowed = (configuredHex ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeHex)
            .Where(x => x.Length == 64)
            .ToArray();
        if (allowed.Length == 0 ||
            !appIntegrity.TryGetProperty("certificateSha256Digest", out var digests) ||
            digests.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var digest in digests.EnumerateArray())
        {
            var value = digest.GetString();
            if (string.IsNullOrWhiteSpace(value)) continue;
            string actual;
            try
            {
                var padded = value.Replace('-', '+').Replace('_', '/');
                padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
                actual = Convert.ToHexString(Convert.FromBase64String(padded));
            }
            catch (FormatException)
            {
                continue;
            }

            foreach (var expected in allowed)
            {
                if (CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(actual),
                        Convert.FromHexString(expected)))
                    return true;
            }
        }
        return false;
    }

    private static string NormalizeHex(string value) =>
        new(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
}
