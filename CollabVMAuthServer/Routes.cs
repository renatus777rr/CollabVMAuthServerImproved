using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Isopoh.Cryptography.Argon2;

namespace Computernewb.CollabVMAuthServer;

public static class Routes
{
    public static void RegisterRoutes(WebApplication app)
    {
        app.MapGet("/api/v1/info", HandleInfo);
        app.MapPost("/api/v1/register", (Delegate) HandleRegister);
        app.MapPost("/api/v1/verify", (Delegate) HandleVerify);
    }

    private static async Task<IResult> HandleVerify(HttpContext context)
    {
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

        var payload = await context.Request.ReadFromJsonAsync<VerifyPayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.username) ||
            string.IsNullOrWhiteSpace(payload.password) || string.IsNullOrWhiteSpace(payload.password))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
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
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Invalid username or password"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if account is verified
        if (user.EmailVerified)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Account is already verified"
            }, Utilities.JsonSerializerOptions);
        }
        // Check if code is correct
        if (user.EmailVerificationCode != payload.code)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new RegisterResponse
            {
                success = false,
                error = "Invalid verification code"
            }, Utilities.JsonSerializerOptions);
        }
        // Verify the account
        await Program.Database.SetUserVerified(payload.username, true);
        return Results.Json(new RegisterResponse
        {
            success = true
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleRegister(HttpContext context)
    {
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
                await Program.hCaptcha!.Verify(payload.captchaToken, context.Connection.RemoteIpAddress!.ToString());
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
            await Program.Database.RegisterAccount(payload.username, payload.email, payload.password, false, code);
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
            return Results.Json(new RegisterResponse
            {
                success = true,
                verificationRequired = false,
                email = payload.email,
                username = payload.username
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