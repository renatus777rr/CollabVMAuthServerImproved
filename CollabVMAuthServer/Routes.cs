using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Isopoh.Cryptography.Argon2;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Computernewb.CollabVMAuthServer;

public static class Routes
{
    public static void RegisterRoutes(WebApplication app)
    {
        app.MapGet("/api/v1/info", HandleInfo);
        app.MapPost("/api/v1/register", (Delegate) HandleRegister);
        app.MapPost("/api/v1/verify", (Delegate) HandleVerify);
        app.MapPost("/api/v1/login", (Delegate) HandleLogin);
        app.MapPost("/api/v1/session", (Delegate) HandleSession);
        app.MapPost("/api/v1/join", (Delegate)HandleJoin);
        app.MapPost("/api/v1/logout", (Delegate)HandleLogout);
        app.MapPost("/api/v1/update", (Delegate)HandleUpdate);
    }

    private static async Task<IResult> HandleUpdate(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        var payload = await context.Request.ReadFromJsonAsync<UpdatePayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.token) ||
            string.IsNullOrWhiteSpace(payload.currentPassword) || (string.IsNullOrWhiteSpace(payload.newPassword) && string.IsNullOrWhiteSpace(payload.username) && string.IsNullOrWhiteSpace(payload.email)))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if session is valid
        var session = await Program.Database.GetSession(payload.token);
        if (session == null || DateTime.Now > session.LastUsed.AddDays(Program.Config.Accounts.SessionExpiryDays))
        {
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "Invalid session",
            }, Utilities.JsonSerializerOptions);
        }
        // Check password
        var user = await Program.Database.GetUser(session.Username) 
            ?? throw new Exception("User not found in database (something is very wrong)");
        if (!Argon2.Verify(user.Password, payload.currentPassword))
        {
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "Invalid password",
            }, Utilities.JsonSerializerOptions);
        }
        // Validate new username
        if (!string.IsNullOrWhiteSpace(payload.username) && !Utilities.ValidateUsername(payload.username))
        {
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "Usernames can contain only numbers, letters, spaces, dashes, underscores, and dots, and must be between 3 and 20 characters."
            }, Utilities.JsonSerializerOptions);
        }
        // Validate new E-Mail
        if (!string.IsNullOrWhiteSpace(payload.email) && !new EmailAddressAttribute().IsValid(payload.email))
        {
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "Malformed E-Mail address."
            }, Utilities.JsonSerializerOptions);
        }
        if (!string.IsNullOrWhiteSpace(payload.email) && Program.Config.Registration.EmailDomainWhitelist &&
            !Program.Config.Registration.AllowedEmailDomains.Contains(payload.email.Split("@")[1]))
        {
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "That E-Mail domain is not allowed."
            }, Utilities.JsonSerializerOptions);
        }
        // Make sure username isn't taken
        var _user = await Program.Database.GetUser(payload.username);
        if (_user != null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That username is taken."
            }, Utilities.JsonSerializerOptions);
        }
        // Check if E-Mail is in use
        _user = await Program.Database.GetUser(email: payload.email);
        if (_user != null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That E-Mail is already in use."
            }, Utilities.JsonSerializerOptions);
        }
        // Validate new password
        if (!string.IsNullOrWhiteSpace(payload.newPassword))
        {
            if (!Utilities.ValidatePassword(payload.newPassword))
            {
                return Results.Json(new UpdateResponse
                {
                    success = false,
                    error = "Passwords must be at least 8 characters and must contain an uppercase and lowercase letter, a number, and a symbol."
                }, Utilities.JsonSerializerOptions);   
            }
            if (Program.BannedPasswords.Contains(payload.newPassword))
            {
                return Results.Json(new UpdateResponse
                {
                    success = false,
                    error = "That password is commonly used and is not allowed."
                }, Utilities.JsonSerializerOptions);
            }
        }
        // Check for duplicate changes
        if (payload.username == user.Username || payload.email == user.Email ||
            Argon2.Verify(user.Password, payload.newPassword))
        {
            return Results.Json(new UpdateResponse
            {
                success = false,
                error = "No changes were made."
            });
        }
        // Perform update
        await Program.Database.UpdateUser(user.Username, payload.username, payload.newPassword, payload.email);
        // Revoke all sessions
        await Program.Database.RevokeAllSessions(user.Username);
        // Unverify the account if the E-Mail was changed
        if (payload.email != null)
        {
            await Program.Database.SetUserVerified(user.Username, false);
            var code = Program.Random.Next(10000000, 99999999).ToString();
            await Program.Database.SetVerificationCode(user.Username, code);
            await Program.Mailer.SendVerificationCode(user.Username, payload.email, code);
        }
        return Results.Json(new UpdateResponse
        {
            success = true,
            verificationRequired = payload.email != null,
            sessionExpired = true
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleLogout(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new LogoutResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        var payload = await context.Request.ReadFromJsonAsync<LogoutPayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.token))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new LogoutResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if session is valid
        var session = await Program.Database.GetSession(payload.token);
        if (session == null)
        {
            return Results.Json(new LogoutResponse
            {
                success = false,
                error = "Invalid session",
            }, Utilities.JsonSerializerOptions);
        }
        // Revoke session
        await Program.Database.RevokeSession(payload.token);
        return Results.Json(new LogoutResponse
        {
            success = true
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleSession(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new SessionResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        var payload = await context.Request.ReadFromJsonAsync<SessionPayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.token))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new SessionResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if session is valid
        var session = await Program.Database.GetSession(payload.token);
        if (session == null)
        {
            return Results.Json(new SessionResponse
            {
                success = false,
                error = "Invalid session",
            }, Utilities.JsonSerializerOptions);
        }
        // Check if session is expired
        if (DateTime.Now > session.LastUsed.AddDays(Program.Config.Accounts.SessionExpiryDays))
        {
            return Results.Json(new SessionResponse
            {
                success = false,
                error = "Expired session",
            }, Utilities.JsonSerializerOptions);
        }
        var user = await Program.Database.GetUser(session.Username) 
            ?? throw new Exception("User not found in database (something is very wrong)");
        return Results.Json(new SessionResponse
        {
            success = true,
            banned = user.Banned,
            username = user.Username,
            email = user.Email
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleJoin(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new JoinResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        var payload = await context.Request.ReadFromJsonAsync<JoinPayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.secretKey) || string.IsNullOrWhiteSpace(payload.sessionToken) || string.IsNullOrWhiteSpace(payload.ip))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new JoinResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check secret key
        if (payload.secretKey != Program.Config.CollabVM.SecretKey)
        {
            context.Response.StatusCode = 401;
            return Results.Json(new JoinResponse
            {
                success = false,
                error = "Invalid secret key"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if session is valid
        var session = await Program.Database.GetSession(payload.sessionToken);
        if (session == null)
        {
            return Results.Json(new JoinResponse
            {
                success = true,
                clientSuccess = false,
                error = "Invalid session",
            }, Utilities.JsonSerializerOptions);
        }
        // Check if session is expired
        if (DateTime.Now > session.LastUsed.AddDays(Program.Config.Accounts.SessionExpiryDays))
        {
            return Results.Json(new JoinResponse
            {
                success = true,
                clientSuccess = false,
                error = "Invalid session",
            }, Utilities.JsonSerializerOptions);
        }
        // Check if banned
        var user = await Program.Database.GetUser(session.Username) 
            ?? throw new Exception("User not found in database (something is very wrong)");
        if (user.Banned)
        {
            return Results.Json(new JoinResponse
            {
                success = true,
                clientSuccess = false,
                error = "You are banned",
            }, Utilities.JsonSerializerOptions);
        }
        // Update session
        await Program.Database.UpdateSessionLastUsed(session.Token, IPAddress.Parse(payload.ip));
        return Results.Json(new JoinResponse
        {
            success = true,
            clientSuccess = true,
            username = session.Username,
            rank = user.Rank
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleLogin(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new LoginResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        var payload = await context.Request.ReadFromJsonAsync<LoginPayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.username) || string.IsNullOrWhiteSpace(payload.password))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new LoginResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        var ip = Utilities.GetIP(context);
        if (ip == null)
        {
            context.Response.StatusCode = 403;
            return Results.Empty;
        }
        // Check captcha response
        if (Program.Config.hCaptcha.Enabled)
        {
            if (string.IsNullOrWhiteSpace(payload.captchaToken))
            {
                context.Response.StatusCode = 400;
                return Results.Json(new LoginResponse
                {
                    success = false,
                    error = "Missing hCaptcha token"
                }, Utilities.JsonSerializerOptions);
            }
            var result =
                await Program.hCaptcha!.Verify(payload.captchaToken, ip.ToString());
            if (!result.success)
            {
                context.Response.StatusCode = 400;
                return Results.Json(new LoginResponse
                {
                    success = false,
                    error = "Invalid captcha response"
                }, Utilities.JsonSerializerOptions);
            }
        }
        // Validate username and password
        var user = await Program.Database.GetUser(payload.username);
        if (user == null || !Argon2.Verify(user.Password, payload.password))
        {
            context.Response.StatusCode = 403;
            return Results.Json(new LoginResponse
            {
                success = false,
                error = "Invalid username or password"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if account is verified
        if (!user.EmailVerified)
        {
            return Results.Json(new LoginResponse
            {
                success = true,
                verificationRequired = true,
                email = user.Email,
                username = user.Username,
            });
        }
        // Check max sessions
        var sessions = await Program.Database.GetSessions(user.Username);
        if (sessions.Length >= Program.Config.Accounts.MaxSessions)
        {
            var oldest = sessions.OrderBy(s => s.LastUsed).First();
            await Program.Database.RevokeSession(oldest.Token);
        }
        // Generate token
        var token = Utilities.RandomString(32);
        await Program.Database.CreateSession(user.Username, token, ip);
        return Results.Json(new LoginResponse
        {
            success = true,
            token = token,
            username = user.Username,
            email = user.Email
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleVerify(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new VerifyResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }

        var payload = await context.Request.ReadFromJsonAsync<VerifyPayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.username) ||
            string.IsNullOrWhiteSpace(payload.password) || string.IsNullOrWhiteSpace(payload.password))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new VerifyResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Validate username and password
        var user = await Program.Database.GetUser(payload.username);
        if (user == null || !Argon2.Verify(user.Password, payload.password))
        {
            context.Response.StatusCode = 403;
            return Results.Json(new VerifyResponse
            {
                success = false,
                error = "Invalid username or password"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if account is verified
        if (user.EmailVerified)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new VerifyResponse
            {
                success = false,
                error = "Account is already verified"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if code is correct
        if (user.EmailVerificationCode != payload.code)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new VerifyResponse
            {
                success = false,
                error = "Invalid verification code"
            }, Utilities.JsonSerializerOptions);
        }
        // Verify the account
        await Program.Database.SetUserVerified(payload.username, true);
        // Create a session
        var token = Utilities.RandomString(32);
        var ip = Utilities.GetIP(context);
        if (ip == null)
        {
            context.Response.StatusCode = 403;
            return Results.Empty;
        }
        await Program.Database.CreateSession(user.Username, token, ip);
        return Results.Json(new VerifyResponse
        {
            success = true,
            sessionToken = token,
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleRegister(HttpContext context)
    {
        var ip = Utilities.GetIP(context);
        if (ip == null)
        {
            context.Response.StatusCode = 403;
            return Results.Empty;
        }
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        var payload = await context.Request.ReadFromJsonAsync<RegisterPayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.username) || string.IsNullOrWhiteSpace(payload.password) || string.IsNullOrWhiteSpace(payload.email))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check captcha response
        if (Program.Config.hCaptcha.Enabled)
        {
            if (string.IsNullOrWhiteSpace(payload.captchaToken))
            {
                context.Response.StatusCode = 400;
                return Results.Json(new RegisterResponse
                {
                    success = false,
                    error = "Missing hCaptcha token"
                }, Utilities.JsonSerializerOptions);
            }
            var result =
                await Program.hCaptcha!.Verify(payload.captchaToken, ip.ToString());
            if (!result.success)
            {
                context.Response.StatusCode = 400;
                return Results.Json(new RegisterResponse
                {
                    success = false,
                    error = "Invalid captcha response"
                }, Utilities.JsonSerializerOptions);
            }
        }
        // Make sure username isn't taken
        var user = await Program.Database.GetUser(payload.username);
        if (user != null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That username is taken."
            }, Utilities.JsonSerializerOptions);
        }
        // Check if E-Mail is in use
        user = await Program.Database.GetUser(email: payload.email);
        if (user != null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That E-Mail is already in use."
            }, Utilities.JsonSerializerOptions);
        }
        // Validate username
        if (!Utilities.ValidateUsername(payload.username))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Usernames can contain only numbers, letters, spaces, dashes, underscores, and dots, and must be between 3 and 20 characters."
            }, Utilities.JsonSerializerOptions);
        }
        // Validate E-Mail
        if (!new EmailAddressAttribute().IsValid(payload.email))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Malformed E-Mail address."
            }, Utilities.JsonSerializerOptions);
        }
        if (Program.Config.Registration.EmailDomainWhitelist &&
            !Program.Config.Registration.AllowedEmailDomains.Contains(payload.email.Split("@")[1]))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That E-Mail domain is not allowed."
            }, Utilities.JsonSerializerOptions);
        }
        // Validate password
        if (!Utilities.ValidatePassword(payload.password))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Passwords must be at least 8 characters and must contain an uppercase and lowercase letter, a number, and a symbol."
            }, Utilities.JsonSerializerOptions);
        }
        if (Program.BannedPasswords.Contains(payload.password))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "That password is commonly used and is not allowed."
            }, Utilities.JsonSerializerOptions);
        }
        // Create the account
        if (Program.Config.Registration.EmailVerificationRequired)
        {
            var code = Program.Random.Next(10000000, 99999999).ToString();
            await Program.Database.RegisterAccount(payload.username, payload.email, payload.password, false, ip,code);
            await Program.Mailer.SendVerificationCode(payload.username, payload.email, code);
            return Results.Json(new RegisterResponse
            {
                success = true,
                verificationRequired = true,
                email = payload.email,
                username = payload.username
            }, Utilities.JsonSerializerOptions);
        }
        else
        {
            await Program.Database.RegisterAccount(payload.username, payload.email, payload.password, true, null);
            var token = Utilities.RandomString(32);
            await Program.Database.CreateSession(user.Username, token, ip);
            return Results.Json(new RegisterResponse
            {
                success = true,
                verificationRequired = false,
                email = payload.email,
                username = payload.username,
                sessionToken = token
            }, Utilities.JsonSerializerOptions);
        }
    }

    private static IResult HandleInfo(HttpContext context)
    {
        return Results.Json(new AuthServerInformation
        {
            // TODO: Implement registration closure
            registrationOpen = true,
            hcaptcha =
            new() {
                required = Program.Config.hCaptcha.Enabled,
                siteKey = Program.Config.hCaptcha.Enabled ? Program.Config.hCaptcha.SiteKey : null
            }
        });
    }
}