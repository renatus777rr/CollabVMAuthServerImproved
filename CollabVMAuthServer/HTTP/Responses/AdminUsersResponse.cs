namespace Computernewb.CollabVMAuthServer.HTTP.Responses;

public class AdminUsersResponse : ApiResponse
{
    public int? totalPageCount { get; set; } = null;
    public AdminUser[]? users { get; set; }
}

public class AdminUser
{
    public uint id { get; set; }
    public required string username { get; set; }
    public required string email { get; set; }
    public required uint rank { get; set; }
    public bool banned { get; set; }
    public required string banReason { get; set; }
    public required string dateOfBirth { get; set; }
    public required string dateJoined { get; set; }
    public required string registrationIp { get; set; }
    public bool developer { get; set; }
}