namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class LoginResponse : ApiResponse
{
    public string? token { get; set; }
    public bool? verificationRequired { get; set; }
    public string? email { get; set; }
    public string? username { get; set; }
    public uint rank { get; set; }
    public bool? developer { get; set; } = null;
}