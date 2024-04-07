namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class ListBotsPayload
{
    public string token { get; set; }
    public int resultsPerPage { get; set; }
    public int page { get; set; }
    public string? owner { get; set; }
}