namespace DAL.DTO;

public class VerifyRegistrationOtpRequest
{
    public Guid RequestCode { get; set; }

    public string Otp { get; set; } = string.Empty;
}
