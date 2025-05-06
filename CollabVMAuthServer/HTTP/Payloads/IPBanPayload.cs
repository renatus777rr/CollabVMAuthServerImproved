namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class IPBanPayload
{
    public required string ip { get; set; }
    public bool banned { get; set; }
    public required string reason { get; set; }
}