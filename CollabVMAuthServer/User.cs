using System;
using System.Net;

namespace Computernewb.CollabVMAuthServer;

public class User
{
    public uint Id { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Email { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public bool EmailVerified { get; set; }
    public string? EmailVerificationCode { get; set; }
    public string? PasswordResetCode { get; set; }
    public Rank Rank { get; set; }
    public bool Banned { get; set; }
    public string? BanReason { get; set; }
    public IPAddress RegistrationIP { get; set; }
    public DateTime Joined { get; set; }
    public bool Developer { get; set; }
}

public enum Rank : uint
{
    Registered = 1,
    Admin = 2,
    Moderator = 3,
}