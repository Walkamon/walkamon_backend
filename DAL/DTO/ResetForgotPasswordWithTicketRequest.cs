namespace DAL.DTO;

public class ResetForgotPasswordWithTicketRequest
{
    public string ResetToken { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
