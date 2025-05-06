using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Computernewb.CollabVMAuthServer.Database;
using Computernewb.CollabVMAuthServer.Database.Schema;
using Computernewb.CollabVMAuthServer.HTTP.Payloads;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Computernewb.CollabVMAuthServer.HTTP;

public partial class CollabVMAuthenticationHandler : SignInAuthenticationHandler<CollabVMAuthenticationSchemeOptions>
{
    public CollabVMAuthenticationHandler(IOptionsMonitor<CollabVMAuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
    }


    [GeneratedRegex("^Session (?<token>.+)$")]
    private static partial Regex AuthorizationHeaderRegex();

    private string? GetSessionTokenFromAuthorizationHeader() {
        // Check for Authorization header
        var authorizationHeader = Context.Request.Headers.Authorization.ToString();
        if (AuthorizationHeaderRegex().Match(authorizationHeader).Groups.TryGetValue("token", out var match)) {
            return match.Value;
        } else {
            return null;
        }
    }

    private string? GetSessionTokenFromCookie() {
        if (Context.Request.Cookies.TryGetValue("collabvm_session", out var token)) {
            return token;
        } else {
            return null;
        }
    }

    private async Task<string?> GetSessionTokenFromBody() {
        // This is how the current webapp sends the token.
        // I really do not like this. Should be changed to use authorization or cookie and then we can eventually axe this
        if (Context.Request.ContentType != "application/json") {
            return null;
        }
        Context.Request.EnableBuffering();
        var payload = await Context.Request.ReadFromJsonAsync<RequestBodyAuthenticationPayload>();
        // sigh
        Context.Request.Body.Position = 0;
        // This can be two different keys because I was on crack cocaine when I wrote the original API
        return
            payload?.Session ??
            payload?.Token;
    }

    
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // There are multiple ways the client can send a session token. We check them in order of preference
        var sessionToken =
            GetSessionTokenFromAuthorizationHeader() ??
            GetSessionTokenFromCookie() ??
            await GetSessionTokenFromBody();
        
        // If no session token was provided, fail
        if (sessionToken == null) {
            return AuthenticateResult.NoResult();
        }

        // Open db and find session
        using var dbContext = new CollabVMAuthDbContext(Options.DbContextOptions);
        Claim[] claims = [];

        if (sessionToken.Length == 32) { // User
            var session = await dbContext.Sessions.Include(s => s.UserNavigation).FirstOrDefaultAsync(s => s.Token == sessionToken);

            // Fail if invalid token or expired
            if (session == null || DateTime.UtcNow > session.LastUsed.AddDays(Program.Config.Accounts!.SessionExpiryDays)) {
                return AuthenticateResult.NoResult();
            }

            claims = [
                new("type", "user"),
                new("id", session.UserNavigation.Id.ToString()),
                new("username", session.UserNavigation.Username),
                new("rank", session.UserNavigation.CvmRank.ToString()),
                new("developer", session.UserNavigation.Developer ? "1" : "0")
            ];
        } else if (sessionToken.Length == 64) { // Bot
            var bot = await dbContext.Bots.FirstOrDefaultAsync(b => b.Token == sessionToken);

            // Fail if unknown bot
            if (bot == null) {
                return AuthenticateResult.NoResult();
            }

            claims = [
                new("type", "bot"),
                new("id", bot.Id.ToString()),
                new("username", bot.Username),
                new("rank", bot.CvmRank.ToString())
            ];
        } else {
            return AuthenticateResult.NoResult();
        }

        // Return success result
        return AuthenticateResult.Success(
            new AuthenticationTicket(
                new ClaimsPrincipal(
                    new ClaimsIdentity(claims, Scheme.Name)
                ),
                Scheme.Name
            )
        );
    }

    protected override async Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        var token = Utilities.RandomString(32);
        using var dbContext = new CollabVMAuthDbContext(Options.DbContextOptions);
        // Add to database
        await dbContext.Sessions.AddAsync(new Session {
            Token = token,
            UserId = uint.Parse(user.FindFirstValue("id")
                ?? throw new InvalidOperationException("User ID claim was null")),
            Created = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            LastIp = Context.Connection.RemoteIpAddress!.GetAddressBytes(),
        });
        await dbContext.SaveChangesAsync();
        // Add claim
        user.Identities.First().AddClaim(new("token", token));
        // Set cookie
        Context.Response.Cookies.Append("collabvm_session", token);
    }

    protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
    {
        throw new System.NotImplementedException();
    }
}