namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class RegisterPayload
{
    public string username { get; set; }
    public string password { get; set; }
    public string email { get; set; }
    public string? captchaToken { get; set; }
    public string dateOfBirth { get; set; }
}