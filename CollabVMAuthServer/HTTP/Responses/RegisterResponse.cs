namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class RegisterResponse : ApiResponse
{
    public bool? verificationRequired { get; set; } = null;
    public string? username { get; set; }
    public string? email { get; set; }
    public string? sessionToken { get; set; }
}