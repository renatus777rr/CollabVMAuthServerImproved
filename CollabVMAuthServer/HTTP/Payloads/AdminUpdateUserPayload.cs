namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class AdminUpdateUserPayload
{
    public string token { get; set; }
    public string username { get; set; }
    public int? rank { get; set; }
    public bool? developer { get; set; } = null;
}