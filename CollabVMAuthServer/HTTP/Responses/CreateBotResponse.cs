namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class CreateBotResponse
{
    public bool success { get; set; }
    public string? error { get; set; }
    public string? token { get; set; }
}