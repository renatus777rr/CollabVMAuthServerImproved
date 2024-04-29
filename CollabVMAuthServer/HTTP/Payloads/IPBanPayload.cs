namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class IPBanPayload
{
    public string session { get; set; }
    public string ip { get; set; }
    public bool banned { get; set; }
    public string reason { get; set; }
}