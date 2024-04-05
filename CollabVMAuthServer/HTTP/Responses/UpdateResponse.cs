namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class UpdateResponse
{
    public bool success { get; set; }
    public string? error { get; set; }
    public bool? verificationRequired { get; set; } = null;
    public bool? sessionExpired { get; set; } = null;
}