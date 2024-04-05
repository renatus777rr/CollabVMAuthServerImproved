namespace Computernewb.CollabVMAuthServer;

public class LoginResponse
{
    public bool success { get; set; }
    public string? token { get; set; }
    public string? error { get; set; }
    public bool? verificationRequired { get; set; }
    public string? email { get; set; }
    public string? username { get; set; }
}