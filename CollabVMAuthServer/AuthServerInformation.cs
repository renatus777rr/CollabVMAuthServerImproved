namespace Computernewb.CollabVMAuthServer;

public class AuthServerInformation
{
    public bool registrationOpen { get; set; }
    public AuthServerInformationCaptcha hcaptcha { get; set; }
}

public class AuthServerInformationCaptcha
{
    public bool required { get; set; }
    public string? siteKey { get; set; }
}