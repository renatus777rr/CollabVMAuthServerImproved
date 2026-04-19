using System.IO;
using Computernewb.CollabVMAuthServer.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Tomlet;
using Tomlet.Attributes;

namespace Computernewb.CollabVMAuthServer;

public interface IConfig
{
    RegistrationConfig Registration { get; }
    AccountConfig Accounts { get; }
    CollabVMConfig CollabVM { get; }
    HTTPConfig HTTP { get; }
    MySQLConfig MySQL { get; }
    SMTPConfig SMTP { get; }
    hCaptchaConfig hCaptcha { get; }
}

[TomlAutoDefaultedNestedProperties]
public class Config : IConfig
{
    public RegistrationConfig Registration { get; set; } = new();
    public AccountConfig Accounts { get; set; } = new();
    public CollabVMConfig CollabVM { get; set; } = new();
    public HTTPConfig HTTP { get; set; } = new();
    public MySQLConfig MySQL { get; set; } = new();
    public SMTPConfig SMTP { get; set; } = new();
    public hCaptchaConfig hCaptcha { get; set; } = new();

    public static Config Load(string configPath)
    {
        var configRaw = File.ReadAllText(configPath);
        return TomletMain.To<Config>(configRaw)!;
    }
}

public class RegistrationConfig
{
    public bool EmailVerificationRequired { get; set; }
    public bool EmailDomainWhitelist { get; set; }
    public string[] AllowedEmailDomains { get; set; } = Array.Empty<string>();
}

public class AccountConfig
{
    public int MaxSessions { get; set; } = 5;
    public int SessionExpiryDays { get; set; } = 30;
}

public class CollabVMConfig
{
    public string SecretKey { get; set; } = string.Empty;
}

public class HTTPConfig
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5000;
    public bool UseXForwardedFor { get; set; }
    public string[] TrustedProxies { get; set; } = Array.Empty<string>();
}

public class MySQLConfig
{
    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;

    private string ConnectionString => new MySqlConnectionStringBuilder
    {
        Server = Host,
        UserID = Username,
        Password = Password,
        Database = Database
    }.ConnectionString;

    public DbContextOptionsBuilder<CollabVMAuthDbContext> Configure(IServiceProvider? serviceProvider = null)
    {
        var builder = serviceProvider?.GetRequiredService<DbContextOptionsBuilder<CollabVMAuthDbContext>>() 
            ?? new DbContextOptionsBuilder<CollabVMAuthDbContext>();
        
        return builder.UseMySQL(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
    }
}

public class SMTPConfig
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "CollabVM Auth";
    public string FromEmail { get; set; } = string.Empty;
    public string VerificationCodeSubject { get; set; } = "Verify your email";
    public string VerificationCodeBody { get; set; } = string.Empty;
    public string ResetPasswordSubject { get; set; } = "Reset your password";
    public string ResetPasswordBody { get; set; } = string.Empty;
}

public class hCaptchaConfig
{
    public bool Enabled { get; set; }
    public string Secret { get; set; } = string.Empty;
    public string SiteKey { get; set; } = string.Empty;
}
