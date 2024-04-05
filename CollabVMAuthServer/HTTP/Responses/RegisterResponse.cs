namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class RegisterResponse
{
    public bool success { get; set; }
    public string? error { get; set; }
    public bool? verificationRequired { get; set; } = null;
    public string? username { get; set; }
    public string? email { get; set; }
    public string? sessionToken { get; set; }
}