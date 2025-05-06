namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class AuthServerInformation
{
    public bool registrationOpen { get; set; }
    public required AuthServerInformationCaptcha hcaptcha { get; set; }
}

public class AuthServerInformationCaptcha
{
    public bool required { get; set; }
    public string? siteKey { get; set; }
}