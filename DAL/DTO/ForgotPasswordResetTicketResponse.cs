namespace DAL.DTO;

public class ForgotPasswordResetTicketResponse
{
    public string ResetToken { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}
