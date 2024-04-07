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
        var session = await Program.Database.GetSession(payload.token);
        if (session == null || Utilities.IsSessionExpired(session))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateBotResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        var user = await Program.Database.GetUser(session.Username)
                   ?? throw new Exception("Could not lookup user from session");
        if (user.Rank != Rank.Admin)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new AdminUsersResponse
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
        var session = await Program.Database.GetSession(payload.token);
        if (session == null || Utilities.IsSessionExpired(session))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUpdateUserResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        var user = await Program.Database.GetUser(session.Username)
                   ?? throw new Exception("Could not lookup user from session");
        if (user.Rank != Rank.Admin)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new AdminUsersResponse
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
        var session = await Program.Database.GetSession(payload.token);
        if (session == null || Utilities.IsSessionExpired(session))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new AdminUsersResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check rank
        var user = await Program.Database.GetUser(session.Username)
                   ?? throw new Exception("Could not lookup user from session");
        if (user.Rank != Rank.Admin)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new AdminUsersResponse
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
        var users = (await Program.Database.ListUsers(filterUsername, payload.orderBy ?? "id", payload.orderByDescending)).Select(user => new AdminUser
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            rank = (int)user.Rank,
            banned = user.Banned,
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