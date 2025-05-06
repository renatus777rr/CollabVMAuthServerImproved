namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class VerifyPayload
{
    public required string username { get; set; }
    public required string password { get; set; }
    public required string code { get; set; }
}