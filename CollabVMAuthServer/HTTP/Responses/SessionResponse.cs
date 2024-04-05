namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class SessionResponse
{
    public bool success { get; set; }
    public string? error { get; set; }
    public bool banned { get; set; } = false;
    public string? username { get; set; }
    public string? email { get; set; }
}