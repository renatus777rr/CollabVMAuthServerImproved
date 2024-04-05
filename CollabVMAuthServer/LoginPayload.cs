namespace Computernewb.CollabVMAuthServer;

public class LoginPayload
{
    public string username { get; set; }
    public string password { get; set; }
    public string? captchaToken { get; set; }
}