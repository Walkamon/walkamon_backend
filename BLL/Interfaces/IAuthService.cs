using DAL.DTO;

namespace BLL.Interfaces;

public interface IAuthService
{
    Task<OtpSentResponse> RegisterAsync(RegisterRequest request, string requestedIp);

    Task<RegisterResponse> VerifyRegistrationOtpAsync(VerifyRegistrationOtpRequest request);

    Task<LoginResponse> LoginAsync(LoginRequest request);

    Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request);

    Task<OtpSentResponse?> ResendRegistrationOtpAsync(
        ResendRegistrationOtpRequest request,
        string requestedIp);

    Task<OtpSentResponse> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string requestedIp);

    Task<ForgotPasswordResetTicketResponse> VerifyForgotPasswordOtpAsync(
        VerifyForgotPasswordOtpRequest request);

    Task ResetForgotPasswordAsync(ResetForgotPasswordRequest request);

    Task ResetForgotPasswordWithTicketAsync(
        ResetForgotPasswordWithTicketRequest request);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);

    Task LogoutAsync(Guid userId);
}
