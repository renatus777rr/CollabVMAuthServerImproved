using System;
using System.Collections.Generic;

namespace Computernewb.CollabVMAuthServer.Database.Schema;

public partial class Session
{
    public string Token { get; set; } = null!;

    public uint UserId { get; set; }

    public DateTime Created { get; set; }

    public DateTime LastUsed { get; set; }

    public byte[] LastIp { get; set; } = null!;

    public virtual User UserNavigation { get; set; } = null!;
}
