namespace BLL.Interfaces;

public sealed record AppAttestationRequest(
    string Token,
    string PayloadHash,
    string PlatformCode,
    DateTime ServerTime);

public sealed record AppAttestationResult(
    bool IsValid,
    string Status,
    string? PackageName,
    DateTime? VerdictTimestamp,
    string? VerdictJson,
    string? RejectionReason);

public interface IAppAttestationVerifier
{
    Task<AppAttestationResult> VerifyAsync(
        AppAttestationRequest request,
        CancellationToken cancellationToken = default);
}
