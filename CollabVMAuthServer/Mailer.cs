using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Computernewb.CollabVMAuthServer;

public class Mailer
{
    private SMTPConfig Config;
    public Mailer(SMTPConfig config)
    {
        Config = config;
    }

    public async Task SendVerificationCode(string username, string email, string code)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(Config.FromName, Config.FromEmail));
        message.To.Add(new MailboxAddress(username, email));
        message.Subject = Config.VerificationCodeSubject
            .Replace("$USERNAME", username)
            .Replace("$EMAIL", email)
            .Replace("$CODE", code);
        message.Body = new TextPart("plain")
        {
            Text = Config.VerificationCodeBody
                .Replace("$USERNAME", username)
                .Replace("$EMAIL", email)
                .Replace("$CODE", code)
        };
        using var client = new SmtpClient();
        await client.ConnectAsync(Config.Host, Config.Port, SecureSocketOptions.StartTlsWhenAvailable);
        await client.AuthenticateAsync(Config.Username, Config.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        Utilities.Log(LogLevel.INFO, $"Sent verification code to {username} <{email}>");
    }
}