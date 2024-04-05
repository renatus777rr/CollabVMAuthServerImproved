namespace Computernewb.CollabVMAuthServer;

public class UpdatePayload
{
    public string token { get; set; }
    public string currentPassword { get; set; }
    
    public string? newPassword { get; set; }
    public string? username { get; set; }
    public string? email { get; set; }
}