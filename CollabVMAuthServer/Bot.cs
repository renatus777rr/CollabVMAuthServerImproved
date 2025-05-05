using System;

namespace Computernewb.CollabVMAuthServer;

public class Bot
{
    public uint Id { get; set; }
    public string Username { get; set; }
    public string Token { get; set; }
    public Rank Rank { get; set; }
    public string Owner { get; set; }
    public DateTime Created { get; set; }
}