namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class SendResetEmailPayload
{
    public string email { get; set; }
    public string username { get; set; }
    public string? captchaToken { get; set; }
}