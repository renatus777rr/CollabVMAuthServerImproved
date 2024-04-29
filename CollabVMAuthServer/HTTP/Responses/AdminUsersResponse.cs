namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class AdminUsersResponse
{
    public bool success { get; set; }
    public string? error { get; set; }
    public int? totalPageCount { get; set; } = null;
    public AdminUser[]? users { get; set; }
}

public class AdminUser
{
    public uint id { get; set; }
    public string username { get; set; }
    public string email { get; set; }
    public int rank { get; set; }
    public bool banned { get; set; }
    public string banReason { get; set; }
    public string dateOfBirth { get; set; }
    public string dateJoined { get; set; }
    public string registrationIp { get; set; }
    public bool developer { get; set; }
}