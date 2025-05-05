using System;
using System.Net;

namespace Computernewb.CollabVMAuthServer;

public class Session
{
    public string Token { get; set; }
    public string Username { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastUsed { get; set; }
    public IPAddress LastIP { get; set; }
}