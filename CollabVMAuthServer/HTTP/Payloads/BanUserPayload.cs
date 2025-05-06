namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class BanUserPayload
{
    public required string username { get; set; }
    public bool banned { get; set; }
    public string? reason { get; set; }
}