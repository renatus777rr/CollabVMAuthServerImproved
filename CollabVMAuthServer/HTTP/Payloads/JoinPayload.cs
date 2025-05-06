namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class JoinPayload
{
    public required string secretKey { get; set; }
    public required string sessionToken { get; set; }
    public required string ip { get; set; }
}