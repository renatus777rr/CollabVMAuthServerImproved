namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class AdminUpdateBotPayload
{
    public string token { get; set; }
    public string username { get; set; }
    public int? rank { get; set; }
}