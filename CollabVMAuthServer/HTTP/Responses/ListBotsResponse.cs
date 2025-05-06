namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class ListBotsResponse : ApiResponse
{
    public int? totalPageCount { get; set; } = null;
    public ListBot[]? bots { get; set; }
}