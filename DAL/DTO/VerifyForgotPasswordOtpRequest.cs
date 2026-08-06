namespace DAL.DTO;

public class VerifyForgotPasswordOtpRequest
{
    public Guid RequestCode { get; set; }

    public string Otp { get; set; } = string.Empty;
}
