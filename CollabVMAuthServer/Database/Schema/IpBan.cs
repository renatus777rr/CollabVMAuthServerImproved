using System;
using System.Collections.Generic;

namespace Computernewb.CollabVMAuthServer.Database.Schema;

public partial class IpBan
{
    public byte[] Ip { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public string? BannedBy { get; set; }

    public DateTime BannedAt { get; set; }
}
