using DAL.DTO;

namespace BLL.Interfaces;
{
public interface IAuthService
{
    Task<OtpSentResponse> RegisterAsync(RegisterRequest request, string requestedIp);

    Task<RegisterResponse> VerifyRegistrationOtpAsync(VerifyRegistrationOtpRequest request);


        Task<LoginResponse> LoginAsync(LoginRequest request);
   
    Task<OtpSentResponse?> ResendRegistrationOtpAsync(
        ResendRegistrationOtpRequest request,
        string requestedIp);
}
}
