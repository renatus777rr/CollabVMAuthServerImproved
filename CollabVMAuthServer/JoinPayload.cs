namespace Computernewb.CollabVMAuthServer;

public class JoinPayload
{
    public string secretKey { get; set; }
    public string sessionToken { get; set; }
    public string ip { get; set; }
}