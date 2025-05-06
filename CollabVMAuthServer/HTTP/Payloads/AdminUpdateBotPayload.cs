namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class AdminUpdateBotPayload
{
    public required string username { get; set; }
    public uint? rank { get; set; }
}