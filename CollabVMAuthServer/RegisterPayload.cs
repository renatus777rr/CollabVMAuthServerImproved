namespace Computernewb.CollabVMAuthServer;

public class RegisterPayload
{
    public string username { get; set; }
    public string password { get; set; }
    public string email { get; set; }
    public string? captchaToken { get; set; }
}