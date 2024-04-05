namespace Computernewb.CollabVMAuthServer;

public class VerifyResponse
{
    public bool success { get; set; }
    public string? error { get; set; }
    public string? sessionToken { get; set; }
}