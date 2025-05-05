using System;
using System.Net;

namespace Computernewb.CollabVMAuthServer;

public class IPBan
{
    public IPAddress IP { get; set; }
    public string Reason { get; set; }
    public string? BannedBy { get; set; }
    public DateTime BannedAt { get; set; }
}