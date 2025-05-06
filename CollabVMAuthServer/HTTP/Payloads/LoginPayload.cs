namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class LoginPayload
{
    public required string username { get; set; }
    public required string password { get; set; }
    public string? captchaToken { get; set; }
}