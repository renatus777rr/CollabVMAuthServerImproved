namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class ResetPasswordPayload
{
    public string username { get; set; }
    public string email { get; set; }
    public string code { get; set; }
    public string newPassword { get; set; }
}