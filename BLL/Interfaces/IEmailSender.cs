namespace BLL.Interfaces;

public interface IEmailSender
{
    Task SendRegistrationOtpAsync(
        string recipientEmail,
        string otp,
        int expiryMinutes);

    Task SendPasswordResetOtpAsync(
        string recipientEmail,
        string otp,
        int expiryMinutes);


    Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string htmlBody);
}
