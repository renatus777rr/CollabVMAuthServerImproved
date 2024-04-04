namespace Computernewb.CollabVMAuthServer;

public class IConfig
{
    public RegistrationConfig Registration { get; set; }
    public HTTPConfig HTTP { get; set; }
    public MySQLConfig MySQL { get; set; }
    public SMTPConfig SMTP { get; set; }
    public hCaptchaConfig hCaptcha { get; set; }
}

public class RegistrationConfig
{
    public bool EmailVerificationRequired { get; set; }
    public bool EmailDomainWhitelist { get; set; }
    public string[] AllowedEmailDomains { get; set; }
}
public class HTTPConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
}
public class MySQLConfig
{
    public string Host { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Database { get; set; }
}

public class SMTPConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string FromName { get; set; }
    public string FromEmail { get; set; }
    public string VerificationCodeSubject { get; set; }
    public string VerificationCodeBody { get; set; }
}

public class hCaptchaConfig
{
    public bool Enabled { get; set; }
    public string? Secret { get; set; }
    public string? SiteKey { get; set; }
}