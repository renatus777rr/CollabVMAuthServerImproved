namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class RegisterPayload
{
    public required string username { get; set; }
    public required string password { get; set; }
    public required string email { get; set; }
    public string? captchaToken { get; set; }
    public required string dateOfBirth { get; set; }
}