namespace BLL.Interfaces;

public interface IEmailSender
{
    Task SendRegistrationOtpAsync(
        string recipientEmail,
        string otp,
        int expiryMinutes);
}
