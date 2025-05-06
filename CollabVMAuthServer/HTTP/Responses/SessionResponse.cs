namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class SessionResponse : ApiResponse
{
    public bool banned { get; set; } = false;
    public string? username { get; set; }
    public string? email { get; set; }
    public uint rank { get; set; }
    public bool? developer { get; set; } = null;
}