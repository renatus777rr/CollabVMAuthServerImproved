using System.IO;
using Computernewb.CollabVMAuthServer.Database;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Tomlet;
using Tomlet.Attributes;

namespace Computernewb.CollabVMAuthServer;

public class IConfig
{
    public RegistrationConfig? Registration { get; set; }
    public AccountConfig? Accounts { get; set; }
    public CollabVMConfig? CollabVM { get; set; }
    public HTTPConfig? HTTP { get; set; }
    public MySQLConfig? MySQL { get; set; }
    public SMTPConfig? SMTP { get; set; }
    public hCaptchaConfig? hCaptcha { get; set; }

    /// <summary>Load config instance from the specified toml file</summary>
    public static IConfig Load(string configPath) {
        // Load from disk
        var configRaw = File.ReadAllText(configPath);
        // Parse toml
        var config = TomletMain.To<IConfig>(configRaw);
        return config;
    }
    
}

public class RegistrationConfig
{
    public bool EmailVerificationRequired { get; set; }
    public bool EmailDomainWhitelist { get; set; }
    public string[]? AllowedEmailDomains { get; set; }
}

public class AccountConfig
{
    public int MaxSessions { get; set; }
    public int SessionExpiryDays { get; set; }
}

public class CollabVMConfig
{
    // We might want to move this to the database, but for now it's fine here.
    public string? SecretKey { get; set; }
}
public class HTTPConfig
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public bool UseXForwardedFor { get; set; }
    public string[]? TrustedProxies { get; set; }
}
public class MySQLConfig
{
    [TomlNonSerialized]
    public string ConnectionString => new MySqlConnectionStringBuilder {
            Server = Host,
            UserID = Username,
            Password = Password,
            Database = Database
        }.ConnectionString;
    public string? Host { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Database { get; set; }

    public DbContextOptionsBuilder<CollabVMAuthDbContext> Configure(DbContextOptionsBuilder<CollabVMAuthDbContext>? builder = null) {
        return (builder ?? new DbContextOptionsBuilder<CollabVMAuthDbContext>())
            .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
    }

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder) {
        return (builder ?? new DbContextOptionsBuilder<CollabVMAuthDbContext>())
            .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
    }
}

public class SMTPConfig
{
    public bool Enabled { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
    public string? VerificationCodeSubject { get; set; }
    public string? VerificationCodeBody { get; set; }
    public string? ResetPasswordSubject { get; set; }
    public string? ResetPasswordBody { get; set; }
}

public class hCaptchaConfig
{
    public bool Enabled { get; set; }
    public string? Secret { get; set; }
    public string? SiteKey { get; set; }
}