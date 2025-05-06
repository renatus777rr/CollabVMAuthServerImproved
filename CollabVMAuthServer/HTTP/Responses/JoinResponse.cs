namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class JoinResponse : ApiResponse
{
    public bool clientSuccess { get; set; } = false;
    public bool? banned { get; set; } = null;
    public string? banReason { get; set; }
    public string? username { get; set; }
    public uint? rank { get; set; }
}