namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class ResetPasswordPayload
{
    public required string username { get; set; }
    public required string email { get; set; }
    public required string code { get; set; }
    public required string newPassword { get; set; }
}