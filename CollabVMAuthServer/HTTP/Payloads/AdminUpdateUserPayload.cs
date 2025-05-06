namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class AdminUpdateUserPayload
{
    public required string username { get; set; }
    public uint? rank { get; set; }
    public bool? developer { get; set; } = null;
}