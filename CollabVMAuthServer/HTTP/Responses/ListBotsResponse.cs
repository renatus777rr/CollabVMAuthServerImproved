namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class ListBotsResponse
{
    public bool success { get; set; }
    public string? error { get; set; }
    public int? totalPageCount { get; set; } = null;
    public ListBot[]? bots { get; set; }
}