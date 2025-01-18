namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class AdminUsersPayload
{
    public string token { get; set; }
    public int resultsPerPage { get; set; }
    public int page { get; set; }
    public string? filterUsername { get; set; }
    public string? filterIp { get; set; }
    public string? orderBy { get; set; }
    public bool orderByDescending { get; set; } = false;
}