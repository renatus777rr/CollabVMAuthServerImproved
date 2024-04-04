namespace Computernewb.CollabVMAuthServer;

public class Session
{
    public string Token { get; set; }
    public uint UserId { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastUsed { get; set; }
}