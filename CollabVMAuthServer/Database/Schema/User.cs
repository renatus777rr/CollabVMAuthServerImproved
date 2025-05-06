using System;
using System.Collections.Generic;

namespace Computernewb.CollabVMAuthServer.Database.Schema;

public partial class User
{
    public uint Id { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Email { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public bool EmailVerified { get; set; }

    public string? EmailVerificationCode { get; set; }

    public string? PasswordResetCode { get; set; }

    public uint CvmRank { get; set; }

    public bool Banned { get; set; }

    public string? BanReason { get; set; }

    public byte[] RegistrationIp { get; set; } = null!;

    public DateTime Created { get; set; }

    public bool Developer { get; set; }

    public virtual ICollection<Bot> Bots { get; set; } = new List<Bot>();

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
