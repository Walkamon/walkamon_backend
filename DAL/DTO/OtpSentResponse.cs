    namespace DAL.DTO;

public class OtpSentResponse
{
    public Guid RequestCode { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime ResendAvailableAtUtc { get; set; }
}
