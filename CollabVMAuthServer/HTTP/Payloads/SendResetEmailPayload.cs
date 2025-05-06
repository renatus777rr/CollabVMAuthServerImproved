namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class SendResetEmailPayload
{
    public required string email { get; set; }
    public required string username { get; set; }
    public string? captchaToken { get; set; }
}