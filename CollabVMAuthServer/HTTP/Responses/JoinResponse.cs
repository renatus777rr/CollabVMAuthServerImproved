namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class JoinResponse
{
    public bool success { get; set; }
    public bool clientSuccess { get; set; } = false;
    public bool? banned { get; set; } = null;
    public string? banReason { get; set; }
    public string? error { get; set; }
    public string? username { get; set; }
    public Rank? rank { get; set; }
}