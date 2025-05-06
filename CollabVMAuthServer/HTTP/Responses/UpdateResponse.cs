namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class UpdateResponse : ApiResponse
{
    public bool? verificationRequired { get; set; } = null;
    public bool? sessionExpired { get; set; } = null;
}