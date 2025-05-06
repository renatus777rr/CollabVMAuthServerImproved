using System;

namespace Computernewb.CollabVMAuthServer.Database.Schema;

public partial class Bot
{
    public uint Id { get; set; }

    public string Username { get; set; } = null!;

    public string Token { get; set; } = null!;

    public uint CvmRank { get; set; }

    public uint Owner { get; set; }

    public DateTime Created { get; set; }

    public virtual User OwnerNavigation { get; set; } = null!;
}
