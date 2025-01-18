using System.Net;
using System.Text.Json;
using Computernewb.CollabVMAuthServer.HTTP.Payloads;
using Computernewb.CollabVMAuthServer.HTTP.Responses;

namespace Computernewb.CollabVMAuthServer.HTTP;

public static class AdminRoutes
{
    public static void RegisterRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/admin/users", (Delegate)HandleAdminUsers);
        app.MapPost("/api/v1/admin/updateuser", (Delegate)HandleAdminUpdateUser);
        app.MapPost("/api/v1/admin/updatebot", (Delegate)HandleAdminUpdateBot);
        app.MapPost("/api/v1/admin/ban", (Delegate)HandleBanUser);
        app.MapPost("/api/v1/admin/ipban", (Delegate)HandleIPBan);
    }

    private static async Task<IResult> HandleIPBan(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        IPBanPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<IPBanPayload>();
        }
        catch (JsonException ex)
        {
            Utilities.Log(LogLevel.DEBUG, $"Failed to parse JSON: {ex.Message}");
            context.Response.StatusCode = 400;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        if (payload == null || string.IsNullOrWhiteSpace(payload.session) || string.IsNullOrWhiteSpace(payload.ip) || (payload.banned && string.IsNullOrWhiteSpace(payload.reason)) || payload.banned == null || !IPAddress.TryParse(payload.ip, out var ip))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check token
        var user = await Utilities.GetStaffByToken(payload.session);
        if (user == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        if (user.Rank != Rank.Admin && user.Rank != Rank.Moderator)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        // Set ban
        if (payload.banned)
        {
            await Program.Database.BanIP(ip, payload.reason, user.Username);
        }
        else
        {
            await Program.Database.UnbanIP(ip);
        }
        return Results.Json(new IPBanResponse
        {
            success = true
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleBanUser(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new BanUserResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        BanUserPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<BanUserPayload>();
        }
        catch (JsonException ex)
        {
            Utilities.Log(LogLevel.DEBUG, $"Failed to parse JSON: {ex.Message}");
            context.Response.StatusCode = 400;
            return Results.Json(new BanUserResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        if (payload == null || string.IsNullOrWhiteSpace(payload.token) || string.IsNullOrWhiteSpace(payload.username) || (payload.banned && string.IsNullOrWhiteSpace(payload.reason)) || payload.banned == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new BanUserResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check token
        var user = await Utilities.GetStaffByToken(payload.token);
        if (user == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        if (user.Rank != Rank.Admin && user.Rank != Rank.Moderator)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        // Check target user
        var targetUser = await Program.Database.GetUser(payload.username);
        if (targetUser == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new BanUserResponse
            {
                success = false,
                error = "User not found"
            }, Utilities.JsonSerializerOptions);
        }
        // Set ban
        await Program.Database.SetBanned(targetUser.Username, payload.banned, payload.banned ? payload.reason : null);
        return Results.Json(new BanUserResponse
        {
            success = true
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleAdminUpdateBot(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        AdminUpdateBotPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<AdminUpdateBotPayload>();
        }
        catch (JsonException ex)
        {
            Utilities.Log(LogLevel.DEBUG, $"Failed to parse JSON: {ex.Message}");
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        if (payload == null || string.IsNullOrWhiteSpace(payload.token) || string.IsNullOrWhiteSpace(payload.username))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check token
        var user = await Utilities.GetStaffByToken(payload.token);
        if (user == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        if (user.Rank != Rank.Admin && user.Rank != Rank.Moderator)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        // Check target bot
        var targetBot = await Program.Database.GetBot(payload.username);
        if (targetBot == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "Bot not found"
            }, Utilities.JsonSerializerOptions);
        }
        // Make sure at least one field is being updated
        if (payload.rank == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "No fields to update"
            }, Utilities.JsonSerializerOptions);
        }
        // Moderators cannot promote bots to admin, and can only promote their own bots to moderator
        else if ((Rank)payload.rank == Rank.Admin && user.Rank == Rank.Moderator)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        if (targetBot.Owner != user.Username && user.Rank == Rank.Moderator)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        int? rank = payload.rank;
        if (rank != null && rank < 1 || rank > 3)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "Invalid rank"
            }, Utilities.JsonSerializerOptions);
        }
        // Update rank
        await Program.Database.UpdateBot(targetBot.Username, newRank: payload.rank);
        return Results.Json(new AdminUpdateBotResponse
        {
            success = true
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleAdminUpdateUser(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateUserResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        AdminUpdateUserPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<AdminUpdateUserPayload>();
        }
        catch (JsonException ex)
        {
            Utilities.Log(LogLevel.DEBUG, $"Failed to parse JSON: {ex.Message}");
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateUserResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        if (payload == null || string.IsNullOrWhiteSpace(payload.token) || string.IsNullOrWhiteSpace(payload.username))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateUserResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check token
        var user = await Utilities.GetStaffByToken(payload.token);
        if (user == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        if (user.Rank != Rank.Admin && user.Rank != Rank.Moderator)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        // Check target user
        var targetUser = await Program.Database.GetUser(payload.username);
        if (targetUser == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateUserResponse
            {
                success = false,
                error = "User not found"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        int? rank = payload.rank;
        if (rank != null && rank < 1 || rank > 3)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateUserResponse
            {
                success = false,
                error = "Invalid rank"
            }, Utilities.JsonSerializerOptions);
        }
        // Moderators cannot change ranks
        if (user.Rank == Rank.Moderator && rank != null)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new AdminUpdateUserResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        // Check developer
        bool? developer = payload.developer;
        // Update rank
        await Program.Database.UpdateUser(targetUser.Username, newRank: payload.rank, developer: developer);
        if (developer == false)
        {
            await Program.Database.DeleteBots(targetUser.Username);
        }
        return Results.Json(new AdminUpdateUserResponse
        {
            success = true
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleAdminUsers(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUsersResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        AdminUsersPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<AdminUsersPayload>();
        }
        catch (JsonException ex)
        {
            Utilities.Log(LogLevel.DEBUG, $"Failed to parse JSON: {ex.Message}");
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUsersResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        if (payload == null || string.IsNullOrWhiteSpace(payload.token) || payload.page < 1 || payload.resultsPerPage < 1)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUsersResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        // Check token
        var user = await Utilities.GetStaffByToken(payload.token);
        if (user == null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        if (user.Rank != Rank.Admin && user.Rank != Rank.Moderator)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new IPBanResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        // Validate orderBy
        if (payload.orderBy != null && !new string[] { "id", "username", "email", "date_of_birth", "cvm_rank", "banned", "created" }.Contains(payload.orderBy))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUsersResponse
            {
                success = false,
                error = "Invalid orderBy"
            }, Utilities.JsonSerializerOptions);
        }
        // Get users
        string? filterUsername = null;
        if (payload.filterUsername != null)
        {
            filterUsername = "%" + payload.filterUsername
                .Replace("%", "!%")
                .Replace("!", "!!")
                .Replace("_", "!_")
                .Replace("[", "![") + "%";
        }
        IPAddress? filterIp = null;
        if (payload.filterIp != null)
        {
            filterIp = IPAddress.Parse(payload.filterIp);
        }

        var users = (await Program.Database.ListUsers(filterUsername, filterIp, payload.orderBy ?? "id", payload.orderByDescending)).Select(user => new AdminUser
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            rank = (int)user.Rank,
            banned = user.Banned,
            banReason = user.BanReason ?? "",
            dateOfBirth = user.DateOfBirth.ToString("yyyy-MM-dd"),
            dateJoined = user.Joined.ToString("yyyy-MM-dd HH:mm:ss"),
            registrationIp = user.RegistrationIP.ToString(),
            developer = user.Developer
        }).ToArray();
        var page = users.Skip((payload.page - 1) * payload.resultsPerPage).Take(payload.resultsPerPage).ToArray();
        return Results.Json(new AdminUsersResponse
        {
            success = true,
            users = page,
            totalPageCount = (int)Math.Ceiling(users.Length / (double)payload.resultsPerPage)
        }, Utilities.JsonSerializerOptions);
    }
}