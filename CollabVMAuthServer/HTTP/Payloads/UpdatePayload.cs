namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class UpdatePayload
{
    public required string currentPassword { get; set; }
    
    public string? newPassword { get; set; }
    public string? username { get; set; }
    public string? email { get; set; }
}