using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Computernewb.CollabVMAuthServer.Database;
using Computernewb.CollabVMAuthServer.Database.Schema;
using Computernewb.CollabVMAuthServer.HTTP.Payloads;
using Computernewb.CollabVMAuthServer.HTTP.Responses;
using Isopoh.Cryptography.Argon2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Computernewb.CollabVMAuthServer.HTTP.Controllers;

[Route("api/v1")]
[ApiController]
public class AuthenticationApiController : ControllerBase
{
    private readonly CollabVMAuthDbContext _dbContext;
    public AuthenticationApiController(CollabVMAuthDbContext dbContext) {
        this._dbContext = dbContext;
    }

    [HttpPost]
    [Route("sendreset")]
    public async Task<IResult> HandleSendReset(SendResetEmailPayload payload)
    {
        if (!Program.Config.SMTP!.Enabled)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Password reset is not supported by this server. Please contact an administrator."
            });
        }

        // Check captcha response
        if (Program.Config.hCaptcha!.Enabled)
        {
            if (string.IsNullOrWhiteSpace(payload.captchaToken))
            {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new ApiResponse
                {
                    success = false,
                    error = "Missing hCaptcha token"
                });
            }
            var result =
                await Program.hCaptcha!.Verify(payload.captchaToken, HttpContext.Connection.RemoteIpAddress!.ToString());
            if (!result.success)
            {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new ApiResponse
                {
                    success = false,
                    error = "Invalid captcha response"
                });
            }
        }
        // Check username and E-Mail
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == payload.username);
        if (user == null || user.Email != payload.email)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Invalid username or E-Mail"
            });
        }
        // Generate reset code
        var code = Program.Random.Next(10000000, 99999999).ToString();
        user.PasswordResetCode = code;
        await _dbContext.SaveChangesAsync();
        await Program.Mailer!.SendPasswordResetEmail(payload.username, payload.email, code);
        return Results.Json(new ApiResponse
        {
            success = true
        });
    }
    
    [HttpPost]
    [Route("reset")]
    public async Task<IResult> HandleReset(ResetPasswordPayload payload)
    {
        // Is mailer enabled?
        if (Program.Mailer == null)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Password reset is disabled"
            });
        }
        // Check username and E-Mail
        var user = await _dbContext.Users.Include(u => u.Sessions).FirstOrDefaultAsync(u => u.Username == payload.username);
        if (user == null || user.Email != payload.email)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Invalid username or E-Mail"
            });
        }
        // Check if code is correct
        if (user.PasswordResetCode != payload.code)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Invalid reset code"
            });
        }
        // Validate new password
        if (!Utilities.ValidatePassword(payload.newPassword))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Passwords must be at least 8 characters and must contain an uppercase and lowercase letter, a number, and a symbol."
            });
        }
        if (Program.BannedPasswords.Contains(payload.newPassword))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "That password is commonly used and is not allowed."
            });
        }
        // Reset password
        var newPasswordHashed = Argon2.Hash(payload.newPassword);
        user.Password = newPasswordHashed;
        user.PasswordResetCode = null;
        user.Sessions.Clear();
        await _dbContext.SaveChangesAsync();
        return Results.Json(new ApiResponse
        {
            success = true
        });
    }

    [HttpPost]
    [Route("update")]
    [Authorize("User")]
    public async Task<IResult> HandleUpdate(UpdatePayload payload)
    {
        var user = await _dbContext.Users.Include(u => u.Sessions).FirstOrDefaultAsync(u => u.Id == uint.Parse(HttpContext.User.FindFirstValue("id")!));
        // Check password
        if (!Argon2.Verify(user!.Password, payload.currentPassword))
        {
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "Invalid password",
            });
        }
        // Validate new username
        if (payload.username != null)
        {
            if (!Utilities.ValidateUsername(payload.username))
            {
                return Results.Json(new UpdateResponse
                {
                    success = false,
                    error = "Usernames can contain only numbers, letters, spaces, dashes, underscores, and dots, and must be between 3 and 20 characters."
                });
            }
            // Make sure username isn't taken
            if (await _dbContext.Users.AnyAsync(u => u.Username == payload.username) || await _dbContext.Bots.AnyAsync(b => b.Username == payload.username))
            {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new RegisterResponse
                {
                    success = false,
                    error = "That username is taken."
                });
            }
            user.Username = payload.username;
        }
        // Validate new E-Mail
        if (payload.email != null)
        {
            if (!new EmailAddressAttribute().IsValid(payload.email))
            {
                return Results.Json(new UpdateResponse
                {
                    success = false,
                    error = "Malformed E-Mail address."
                });
            }
            if (Program.Config.Registration!.EmailDomainWhitelist && !Program.Config.Registration!.AllowedEmailDomains!.Contains(payload.email.Split("@")[1]))
            {
                return Results.Json(new UpdateResponse
                {
                    success = false,
                    error = "That E-Mail domain is not allowed."
                });
            }
            // Check if E-Mail is in use
            if (_dbContext.Users.Any(u => u.Email == payload.email))
            {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new RegisterResponse
                {
                    success = false,
                    error = "That E-Mail is already in use."
                });
            }
            user.Email = payload.email;
        }
        // Validate new password
        if (payload.newPassword != null)
        {
            if (!Utilities.ValidatePassword(payload.newPassword))
            {
                return Results.Json(new UpdateResponse
                {
                    success = false,
                    error = "Passwords must be at least 8 characters and must contain an uppercase and lowercase letter, a number, and a symbol."
                });   
            }
            if (Program.BannedPasswords.Contains(payload.newPassword))
            {
                return Results.Json(new UpdateResponse
                {
                    success = false,
                    error = "That password is commonly used and is not allowed."
                });
            }
            user.Password = Argon2.Hash(payload.newPassword);
        }
        // Revoke all sessions
        user.Sessions.Clear();
        // Unverify the account if the E-Mail was changed
        if (payload.email != null && Program.Config.Registration!.EmailVerificationRequired)
        {
            user.EmailVerified = false;
            user.EmailVerificationCode = Program.Random.Next(10000000, 99999999).ToString();
            await Program.Mailer!.SendVerificationCode(user.Username, payload.email, user.EmailVerificationCode);
        }
        // Save changes
        await _dbContext.SaveChangesAsync();
        return Results.Json(new UpdateResponse
        {
            success = true,
            verificationRequired = !user.EmailVerified,
            sessionExpired = true
        });
    }

    [HttpPost]
    [Route("logout")]
    [Authorize("User")]
    public async Task<IResult> HandleLogout()
    {
        var user = await _dbContext.Users.Include(u => u.Sessions).FirstOrDefaultAsync(u => u.Id == uint.Parse(HttpContext.User.FindFirstValue("id")!));
        // Revoke session
        user!.Sessions.Clear();
        await _dbContext.SaveChangesAsync();

        return Results.Json(new ApiResponse
        {
            success = true
        });
    }

    [HttpPost]
    [Route("session")]
    [Authorize("User")]
    public async Task<IResult> HandleSession()
    {
        var user = await _dbContext.Users.FindAsync(uint.Parse(HttpContext.User.FindFirstValue("id")!));
        return Results.Json(new SessionResponse
        {
            success = true,
            banned = user!.Banned,
            username = user.Username,
            email = user.Email,
            rank = user.CvmRank,
            developer = user.Developer
        });
    }

    [HttpPost]
    [Route("join")]
    public async Task<IResult> HandleJoin(JoinPayload payload)
    {
        // Check secret key
        if (payload.secretKey != Program.Config.CollabVM!.SecretKey)
        {
            HttpContext.Response.StatusCode = 401;
            return Results.Json(new JoinResponse
            {
                success = false,
                error = "Invalid secret key"
            });
        }
        // Check if IP banned
        if (!IPAddress.TryParse(payload.ip, out var ip))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new JoinResponse
            {
                success = false,
                error = "Malformed IP address"
            });
        }
        var ban = await _dbContext.IpBans.FirstOrDefaultAsync(b => b.Ip == ip.GetAddressBytes());
        if (ban != null)
        {
            HttpContext.Response.StatusCode = 200;
            return Results.Json(new JoinResponse
            {
                success = true,
                clientSuccess = false,
                error = "Banned",
                banned = true,
                banReason = ban.Reason
            });
        }
        // Check if session is valid
        if (payload.sessionToken.Length == 32)
        {
            // User
            var session = await _dbContext.Sessions.Include(s => s.UserNavigation).FirstOrDefaultAsync(s => s.Token == payload.sessionToken);
            if (session == null)
            {
                return Results.Json(new JoinResponse
                {
                    success = true,
                    clientSuccess = false,
                    error = "Invalid session",
                });
            }
            // Check if session is expired
            if (DateTime.Now > session.LastUsed.AddDays(Program.Config.Accounts!.SessionExpiryDays))
            {
                return Results.Json(new JoinResponse
                {
                    success = true,
                    clientSuccess = false,
                    error = "Invalid session",
                });
            }
            // Check if banned
            if (session.UserNavigation.Banned)
            {
                return Results.Json(new JoinResponse
                {
                    success = true,
                    clientSuccess = false,
                    banned = true,
                    error = "Banned",
                    banReason = session.UserNavigation.BanReason
                });
            }
            // Update session
            session.LastUsed = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Results.Json(new JoinResponse
            {
                success = true,
                clientSuccess = true,
                username = session.UserNavigation.Username,
                rank = session.UserNavigation.CvmRank
            });
        } else if (payload.sessionToken.Length == 64)
        {
            // Bot
            var bot = await _dbContext.Bots.FirstOrDefaultAsync(b => b.Token == payload.sessionToken);
            if (bot == null)
            {
                return Results.Json(new JoinResponse
                {
                    success = true,
                    clientSuccess = false,
                    error = "Invalid session",
                });
            }
            return Results.Json(new JoinResponse
            {
                success = true,
                clientSuccess = true,
                username = bot.Username,
                rank = bot.CvmRank
            });
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new JoinResponse
            {
                success = true,
                clientSuccess = false,
                error = "Invalid session"
            });
        }
    }

    [HttpPost]
    [Route("login")]
    public async Task<IResult> HandleLogin(LoginPayload payload)
    {
        // Check captcha response
        if (Program.Config.hCaptcha!.Enabled)
        {
            if (string.IsNullOrWhiteSpace(payload.captchaToken))
            {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new LoginResponse
                {
                    success = false,
                    error = "Missing hCaptcha token"
                });
            }
            var result =
                await Program.hCaptcha!.Verify(payload.captchaToken, HttpContext.Connection.RemoteIpAddress!.ToString());
            if (!result.success)
            {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new LoginResponse
                {
                    success = false,
                    error = "Invalid captcha response"
                });
            }
        }
        // Validate username and password
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == payload.username);
        if (user == null || !Argon2.Verify(user.Password, payload.password))
        {
            HttpContext.Response.StatusCode = 403;
            return Results.Json(new LoginResponse
            {
                success = false,
                error = "Invalid username or password"
            });
        }
        // Check if IP banned
        var ban = await _dbContext.IpBans.FirstOrDefaultAsync(b => b.Ip == HttpContext.Connection.RemoteIpAddress!.GetAddressBytes());
        if (ban != null)
        {
            HttpContext.Response.StatusCode = 403;
            return Results.Json(new LoginResponse
            {
                success = false,
                error = $"You are banned: {ban.Reason}"
            });
        }
        // Check if account is verified
        if (!user.EmailVerified && Program.Config.Registration!.EmailVerificationRequired)
        {
            if (user.EmailVerificationCode == null) {
                user.EmailVerificationCode = Program.Random.Next(10000000, 99999999).ToString();
                await _dbContext.SaveChangesAsync();
                await Program.Mailer!.SendVerificationCode(user.Username, user.Email, user.EmailVerificationCode);
            }
            return Results.Json(new LoginResponse
            {
                success = true,
                verificationRequired = true,
                email = user.Email,
                username = user.Username,
                rank = user.CvmRank,
                developer = user.Developer
            });
        }
        // Check max sessions
        var sessions = await _dbContext.Sessions.Include(s => s.UserNavigation).CountAsync(s => s.UserNavigation.Username == user.Username);
        if (sessions >= Program.Config.Accounts!.MaxSessions)
        {
            var oldest = await _dbContext.Sessions.Include(s => s.UserNavigation).Where(s => s.UserNavigation.Username == user.Username).OrderBy(s => s.LastUsed).FirstAsync();
            _dbContext.Sessions.Remove(oldest);
            await _dbContext.SaveChangesAsync();
        }
        // Perform sign-in
        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new("id", user.Id.ToString())]));
        await HttpContext.SignInAsync(userPrincipal);
        var token = userPrincipal.FindFirstValue("token")
            ?? throw new InvalidOperationException("Sign in handler did not add token");
        return Results.Json(new LoginResponse
        {
            success = true,
            token = token,
            username = user.Username,
            email = user.Email,
            rank = user.CvmRank,
            developer = user.Developer
        });
    }

    [HttpPost]
    [Route("verify")]
    public async Task<IResult> HandleVerify(VerifyPayload payload)
    {
        // Validate username and password
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == payload.username);
        if (user == null || !Argon2.Verify(user.Password, payload.password))
        {
            HttpContext.Response.StatusCode = 403;
            return Results.Json(new VerifyResponse
            {
                success = false,
                error = "Invalid username or password"
            });
        }
        // Check if account is verified
        if (user.EmailVerified)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new VerifyResponse
            {
                success = false,
                error = "Account is already verified"
            });
        }
        // Check if code is correct
        if (user.EmailVerificationCode != payload.code)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new VerifyResponse
            {
                success = false,
                error = "Invalid verification code"
            });
        }
        // Verify the account
        user.EmailVerified = true;
        // Create a session
        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new("id", user.Id.ToString())]));
        await HttpContext.SignInAsync(userPrincipal);
        var token = userPrincipal.FindFirstValue("token")
            ?? throw new InvalidOperationException("Sign in handler did not add token");
        await _dbContext.SaveChangesAsync();
        return Results.Json(new VerifyResponse
        {
            success = true,
            sessionToken = token,
        });
    }

    [HttpPost]
    [Route("register")]
    public async Task<IResult> HandleRegister(RegisterPayload payload)
    {
        // Check if IP banned
        var ban = await _dbContext.IpBans.FirstOrDefaultAsync(b => b.Ip == HttpContext.Connection.RemoteIpAddress!.GetAddressBytes());
        if (ban != null)
        {
            HttpContext.Response.StatusCode = 403;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = $"You are banned: {ban.Reason}"
            });
        }
        // Check captcha response
        if (Program.Config.hCaptcha!.Enabled)
        {
            if (string.IsNullOrWhiteSpace(payload.captchaToken))
            {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new RegisterResponse
                {
                    success = false,
                    error = "Missing hCaptcha token"
                });
            }
            var result =
                await Program.hCaptcha!.Verify(payload.captchaToken, HttpContext.Connection.RemoteIpAddress!.ToString());
            if (!result.success)
            {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new RegisterResponse
                {
                    success = false,
                    error = "Invalid captcha response"
                });
            }
        }
        // Make sure username isn't taken
        if (await _dbContext.Users.AnyAsync(u => u.Username == payload.username) || await _dbContext.Bots.AnyAsync(b => b.Username == payload.username))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That username is taken."
            });
        }
        // Check if E-Mail is in use
        if (await _dbContext.Users.AnyAsync(u => u.Email == payload.email))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That E-Mail is already in use."
            });
        }
        // Validate username
        if (!Utilities.ValidateUsername(payload.username))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Usernames can contain only numbers, letters, spaces, dashes, underscores, and dots, and must be between 3 and 20 characters."
            });
        }
        // Validate E-Mail
        if (!new EmailAddressAttribute().IsValid(payload.email))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Malformed E-Mail address."
            });
        }
        if (Program.Config.Registration!.EmailDomainWhitelist &&
            !Program.Config.Registration.AllowedEmailDomains!.Contains(payload.email.Split("@")[1]))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That E-Mail domain is not allowed."
            });
        }
        // Validate password
        if (!Utilities.ValidatePassword(payload.password))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Passwords must be at least 8 characters and must contain an uppercase and lowercase letter, a number, and a symbol."
            });
        }
        if (Program.BannedPasswords.Contains(payload.password))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That password is commonly used and is not allowed."
            });
        }
        // Validate date of birth
        if (!DateOnly.TryParseExact(payload.dateOfBirth, "yyyy-MM-dd", out var dob))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Invalid date of birth"
            });
        }

        if (dob.AddYears(13) > DateOnly.FromDateTime(DateTime.Now))
        {
            HttpContext.Response.StatusCode = 400;
            await _dbContext.IpBans.AddAsync(new IpBan {
                Ip = HttpContext.Connection.RemoteIpAddress!.GetAddressBytes(),
                Reason = "You are not old enough to use CollabVM.",
                BannedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "You are not old enough to use CollabVM."
            });
        }
        // theres no fucking chance
        if (dob < new DateOnly(1954, 1, 1))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Are you sure about that?"
            });
        }
        // Create the account
        string? token = null;
        var user = new User {
            Username = payload.username,
            Password = Argon2.Hash(payload.password),
            Email = payload.email,
            DateOfBirth = dob,
            // If this is the first user, make them an admin
            CvmRank = (uint) ((await _dbContext.Users.AnyAsync()) ? 1 : 2),
            RegistrationIp = HttpContext.Connection.RemoteIpAddress!.GetAddressBytes(),
            Created = DateTime.UtcNow,
        };
        _dbContext.Users.Add(user);

        if (Program.Config.Registration.EmailVerificationRequired)
        {
            user.EmailVerificationCode = Program.Random.Next(10000000, 99999999).ToString();
            await _dbContext.SaveChangesAsync();
            await Program.Mailer!.SendVerificationCode(user.Username, user.Email, user.EmailVerificationCode);
        }
        else
        {
            user.EmailVerified = true;
            await _dbContext.SaveChangesAsync();
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new("id", user.Id.ToString())]));
            await HttpContext.SignInAsync(userPrincipal);
            token = userPrincipal.FindFirstValue("token")
                ?? throw new InvalidOperationException("Sign in handler did not add token");
        }

        return Results.Json(new RegisterResponse
        {
            success = true,
            verificationRequired = !user.EmailVerified,
            email = user.Email,
            username = user.Username,
            sessionToken = token
        });
    }

    [HttpGet]
    [Route("info")]
    public IResult HandleInfo()
    {
        return Results.Json(new AuthServerInformation
        {
            // TODO: Implement registration closure
            registrationOpen = true,
            hcaptcha =
            new() {
                required = Program.Config.hCaptcha!.Enabled,
                siteKey = Program.Config.hCaptcha.Enabled ? Program.Config.hCaptcha.SiteKey : null
            }
        });
    }
}