namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class ListBot
{
    public int id { get; set; }
    public required string username { get; set; }
    public uint rank { get; set; }
    public required string owner { get; set; }
    public required string created { get; set; }
}