namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class VerifyResponse : ApiResponse
{
    public string? sessionToken { get; set; }
}