namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class VerifyPayload
{
    public string username { get; set; }
    public string password { get; set; }
    public string code { get; set; }
}