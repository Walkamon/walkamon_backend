using System.Text.Json;
using BLL.Interfaces;
using BLL.Options;
using BLL.Service;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class AppAttestationVerifierTests
{
    private static readonly DateTime Now = new(2026, 7, 17, 3, 0, 0, DateTimeKind.Utc);
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public async Task DevelopmentBypass_WorksOnlyInDevelopment()
    {
        var request = new AppAttestationRequest($"DEV_BYPASS:{Hash}", Hash, "android", Now);
        Assert.True((await new DevelopmentAttestationVerifier("Development").VerifyAsync(request)).IsValid);
        var production = await new DevelopmentAttestationVerifier("Production").VerifyAsync(request);
        Assert.False(production.IsValid);
        Assert.Equal("development_bypass_forbidden", production.RejectionReason);
    }

    [Theory]
    [InlineData("bad.package", Hash, "PLAY_RECOGNIZED", true, "package_mismatch")]
    [InlineData("com.walkamon", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "PLAY_RECOGNIZED", true, "request_hash_mismatch")]
    [InlineData("com.walkamon", Hash, "UNRECOGNIZED_VERSION", true, "app_not_play_recognized")]
    [InlineData("com.walkamon", Hash, "PLAY_RECOGNIZED", false, "device_integrity_failed")]
    public void VerdictValidator_RejectsMismatches(
        string package, string hash, string appVerdict, bool deviceIntegrity, string reason)
    {
        var result = PlayIntegrityVerdictValidator.Validate(
            Verdict(package, hash, appVerdict, deviceIntegrity, Now),
            Hash, Now, Options());
        Assert.False(result.IsValid);
        Assert.Equal(reason, result.RejectionReason);
    }

    [Fact]
    public void VerdictValidator_AcceptsRequiredVerdicts()
    {
        var result = PlayIntegrityVerdictValidator.Validate(
            Verdict("com.walkamon", Hash, "PLAY_RECOGNIZED", true, Now),
            Hash, Now, Options());
        Assert.True(result.IsValid);
        Assert.Equal("verified", result.Status);
    }

    [Fact]
    public void VerdictValidator_RejectsStaleTimestamp()
    {
        var result = PlayIntegrityVerdictValidator.Validate(
            Verdict("com.walkamon", Hash, "PLAY_RECOGNIZED", true, Now.AddMinutes(-3)),
            Hash, Now, Options());
        Assert.Equal("verdict_timestamp_invalid", result.RejectionReason);
    }

    [Fact]
    public void VerdictValidator_AcceptsSideloadedApkWithAllowedCertificate()
    {
        var certificate = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var options = Options();
        options.AppRecognitionMode = "certificate_allowlist";
        options.AllowedCertificateSha256Hex = Convert.ToHexString(certificate);
        options.MinimumVersionCode = 10;
        var verdict = Verdict(
            "com.walkamon",
            Hash,
            "UNRECOGNIZED_VERSION",
            true,
            Now,
            Convert.ToBase64String(certificate).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            "10");

        var result = PlayIntegrityVerdictValidator.Validate(verdict, Hash, Now, options);

        Assert.True(result.IsValid);
    }

    private static StepValidationOptions Options() => new()
    {
        AndroidPackageName = "com.walkamon",
        AttestationMaxAgeSeconds = 120
    };

    private static string Verdict(
        string package,
        string hash,
        string appVerdict,
        bool deviceIntegrity,
        DateTime time,
        string? certificate = null,
        string versionCode = "1") =>
        JsonSerializer.Serialize(new
        {
            tokenPayloadExternal = new
            {
                requestDetails = new
                {
                    requestPackageName = package,
                    requestHash = hash,
                    timestampMillis = new DateTimeOffset(time).ToUnixTimeMilliseconds().ToString()
                },
                appIntegrity = new
                {
                    appRecognitionVerdict = appVerdict,
                    packageName = package,
                    certificateSha256Digest = certificate == null
                        ? Array.Empty<string>()
                        : new[] { certificate },
                    versionCode
                },
                deviceIntegrity = new
                {
                    deviceRecognitionVerdict = deviceIntegrity
                        ? new[] { "MEETS_DEVICE_INTEGRITY" }
                        : Array.Empty<string>()
                },
                accountDetails = new { appLicensingVerdict = "LICENSED" }
            }
        });
}
