using BLL.Interfaces;
using BLL.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net;

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
        await SendOtpAsync(
            recipientEmail,
            otp,
            expiryMinutes,
            "Mã xác thực email Walkamon",
            "Xác thực tài khoản Walkamon",
            "Dùng mã này để hoàn tất việc tạo tài khoản Walkamon của bạn.",
            "Nếu bạn không tạo tài khoản Walkamon, bạn có thể bỏ qua email này.");
    }

    public async Task SendPasswordResetOtpAsync(
        string recipientEmail,
        string otp,
        int expiryMinutes)
    {
        await SendOtpAsync(
            recipientEmail,
            otp,
            expiryMinutes,
            "Mã đặt lại mật khẩu Walkamon",
            "Đặt lại mật khẩu Walkamon",
            "Dùng mã này để đặt lại mật khẩu Walkamon của bạn.",
            "Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này để giữ an toàn cho tài khoản.");
    }

    private async Task SendOtpAsync(
        string recipientEmail,
        string otp,
        int expiryMinutes,
        string subject,
        string title,
        string intro,
        string securityNote)
    {
        if (string.IsNullOrWhiteSpace(_options.Username)
            || string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            throw new InvalidOperationException("SMTP credentials are not configured");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.Username));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            TextBody =
                $"{title}{Environment.NewLine}{Environment.NewLine}"
                + $"{intro}{Environment.NewLine}"
                + $"Mã của bạn là: {otp}{Environment.NewLine}"
                + $"Mã này sẽ hết hạn sau {expiryMinutes} phút.{Environment.NewLine}{Environment.NewLine}"
                + securityNote,
            HtmlBody = BuildOtpHtml(otp, expiryMinutes, title, intro, securityNote)
        };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.Host, _options.Port, socketOptions);
        await client.AuthenticateAsync(_options.Username, _options.AppPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    private static string BuildOtpHtml(
        string otp,
        int expiryMinutes,
        string title,
        string intro,
        string securityNote)
    {
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedIntro = WebUtility.HtmlEncode(intro);
        var encodedSecurityNote = WebUtility.HtmlEncode(securityNote);
        var encodedOtp = WebUtility.HtmlEncode(otp);

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{encodedTitle}}</title>
</head>
<body style="margin:0;padding:0;background:#f4f7fb;font-family:Arial,Helvetica,sans-serif;color:#172033;">
  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f7fb;padding:32px 12px;">
    <tr>
      <td align="center">
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #e6edf5;box-shadow:0 12px 32px rgba(23,32,51,0.08);">
          <tr>
            <td style="background:#76a084;padding:26px 28px;">
              <div style="font-size:13px;letter-spacing:0.12em;text-transform:uppercase;color:#eef6f1;font-weight:700;">Walkamon</div>
              <h1 style="margin:10px 0 0;font-size:24px;line-height:1.25;color:#ffffff;font-weight:800;">{{encodedTitle}}</h1>
            </td>
          </tr>
          <tr>
            <td style="padding:30px 28px 12px;">
              <p style="margin:0;font-size:16px;line-height:1.65;color:#344055;">{{encodedIntro}}</p>
            </td>
          </tr>
          <tr>
            <td align="center" style="padding:18px 28px 8px;">
              <div style="display:inline-block;padding:18px 26px;background:#f2f7f4;border:1px solid #c9dccc;border-radius:14px;font-size:34px;line-height:1;letter-spacing:0.28em;color:#3f6b51;font-weight:800;">{{encodedOtp}}</div>
            </td>
          </tr>
          <tr>
            <td style="padding:16px 28px 24px;">
              <p style="margin:0;text-align:center;font-size:14px;line-height:1.6;color:#607086;">Mã này sẽ hết hạn sau <strong style="color:#172033;">{{expiryMinutes}} phút</strong>.</p>
            </td>
          </tr>
          <tr>
            <td style="padding:0 28px 30px;">
              <div style="background:#fff8e8;border:1px solid #ffe0a3;border-radius:12px;padding:14px 16px;color:#72521a;font-size:13px;line-height:1.55;">{{encodedSecurityNote}}</div>
            </td>
          </tr>
        </table>
        <p style="margin:18px 0 0;font-size:12px;color:#8a98aa;">Đây là email tự động từ Walkamon.</p>
      </td>
    </tr>
  </table>
</body>
</html>
""";
    }

    public async Task SendEmailAsync(
     string recipientEmail,
     string subject,
     string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_options.Username)
            || string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            throw new InvalidOperationException(
                "SMTP credentials are not configured");
        }

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                _options.FromName,
                _options.Username));

        message.To.Add(
            MailboxAddress.Parse(recipientEmail));

        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        var socketOptions = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            socketOptions);

        await client.AuthenticateAsync(
            _options.Username,
            _options.AppPassword);

        await client.SendAsync(message);

        await client.DisconnectAsync(true);
    }
}
