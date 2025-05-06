namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class ListBotsPayload
{
    public int resultsPerPage { get; set; }
    public int page { get; set; }
    public string? owner { get; set; }
}