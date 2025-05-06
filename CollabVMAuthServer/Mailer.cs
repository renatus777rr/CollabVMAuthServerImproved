using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Computernewb.CollabVMAuthServer;

public class Mailer
{
    private readonly SMTPConfig Config;
    private readonly ILogger _logger;
    public Mailer(SMTPConfig config)
    {
        if (config.Host == null || config.Port == null || config.Username == null || config.Password == null ||
            config.FromName == null || config.FromEmail == null || config.VerificationCodeSubject == null ||
            config.VerificationCodeBody == null || config.ResetPasswordSubject == null ||
            config.ResetPasswordBody == null)
        {
            throw new InvalidOperationException("SMTPConfig is missing required fields");
        }
        Config = config;
        _logger = LoggerFactory.Create(Utilities.ConfigureLogging).CreateLogger<Mailer>();
    }

    public async Task SendVerificationCode(string username, string email, string code)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(Config.FromName, Config.FromEmail));
        message.To.Add(new MailboxAddress(username, email));
        message.Subject = Config.VerificationCodeSubject!
            .Replace("$USERNAME", username)
            .Replace("$EMAIL", email)
            .Replace("$CODE", code);
        message.Body = new TextPart("plain")
        {
            Text = Config.VerificationCodeBody!
                .Replace("$USERNAME", username)
                .Replace("$EMAIL", email)
                .Replace("$CODE", code)
        };
        using var client = new SmtpClient();
        await client.ConnectAsync(Config.Host, (int)Config.Port!, SecureSocketOptions.StartTlsWhenAvailable);
        await client.AuthenticateAsync(Config.Username, Config.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        _logger.LogInformation("Sent e-mail verification code to {username} <{email}>", username, email);
    }
    
    public async Task SendPasswordResetEmail(string username, string email, string code)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(Config.FromName, Config.FromEmail));
        message.To.Add(new MailboxAddress(username, email));
        message.Subject = Config.ResetPasswordSubject!
            .Replace("$USERNAME", username)
            .Replace("$EMAIL", email)
            .Replace("$CODE", code);
        message.Body = new TextPart("plain")
        {
            Text = Config.ResetPasswordBody!
                .Replace("$USERNAME", username)
                .Replace("$EMAIL", email)
                .Replace("$CODE", code)
        };
        using var client = new SmtpClient();
        await client.ConnectAsync(Config.Host, (int)Config.Port!, SecureSocketOptions.StartTlsWhenAvailable);
        await client.AuthenticateAsync(Config.Username, Config.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        _logger.LogInformation("Sent password reset verification code to {username} <{email}>", username, email);
    }
    
}