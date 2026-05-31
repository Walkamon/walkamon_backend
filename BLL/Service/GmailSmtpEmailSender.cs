using BLL.Interfaces;
using BLL.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BLL.Service;

public class GmailSmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public GmailSmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendRegistrationOtpAsync(
        string recipientEmail,
        string otp,
        int expiryMinutes)
    {
        if (string.IsNullOrWhiteSpace(_options.Username)
            || string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            throw new InvalidOperationException("SMTP credentials are not configured");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.Username));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "Walkamon email verification code";
        message.Body = new TextPart("plain")
        {
            Text =
                $"Your Walkamon verification code is: {otp}{Environment.NewLine}{Environment.NewLine}"
                + $"This code expires in {expiryMinutes} minutes."
        };

        using var client = new SmtpClient();
        var socketOptions = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.Host, _options.Port, socketOptions);
        await client.AuthenticateAsync(_options.Username, _options.AppPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
