using System.Text.Json;
using Computernewb.CollabVMAuthServer.HTTP.Payloads;
using Computernewb.CollabVMAuthServer.HTTP.Responses;

namespace Computernewb.CollabVMAuthServer.HTTP;

public static class DeveloperRoutes
{
    public static void RegisterRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/bots/create", (Delegate)HandleCreateBot);
        app.MapPost("/api/v1/bots/list", (Delegate)HandleListBots);
    }

    private static async Task<IResult> HandleListBots(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new ListBotsResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        ListBotsPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<ListBotsPayload>();
        }
        catch (JsonException ex)
        {
            Utilities.Log(LogLevel.DEBUG, $"Failed to parse JSON: {ex.Message}");
            context.Response.StatusCode = 400;
            return Results.Json(new ListBotsResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        if (payload == null || string.IsNullOrWhiteSpace(payload.token) || payload.resultsPerPage <= 0 ||
            payload.page <= 0)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new ListBotsResponse
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
            return Results.Json(new ListBotsResponse
            {
                success = false,
                error = "Invalid session"
            },  Utilities.JsonSerializerOptions);
        }
        // Check developer status
        var user = await Program.Database.GetUser(session.Username) ??
                   throw new Exception("Unable to get user from session");
        if (!user.Developer && user.Rank != Rank.Admin)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error = "You must be an approved developer to create and manage bots."
            }, Utilities.JsonSerializerOptions);
        }
        // owner can only be specified by admins
        if (payload.owner != null && user.Rank != Rank.Admin)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new ListBotsResponse
            {
                success = false,
                error = "Insufficient permissions"
            }, Utilities.JsonSerializerOptions);
        }
        // Get bots
        // If the user is not an admin, they can only see their own bots
        var bots = (await Program.Database.ListBots(payload.owner ?? (user.Rank == Rank.Admin ? null : user.Username))).Select(bot => new ListBot
        {
            id = (int)bot.Id,
            username = bot.Username,
            rank = (int)bot.Rank,
            owner = bot.Owner,
            created = bot.Created.ToString("yyyy-MM-dd HH:mm:ss")
            
        });
        var page = bots.Skip((payload.page - 1) * payload.resultsPerPage).Take(payload.resultsPerPage).ToArray();
        return Results.Json(new ListBotsResponse
        {
            success = true,
            totalPageCount = (int)Math.Ceiling(bots.Count() / (double)payload.resultsPerPage),
            bots = page
        }, Utilities.JsonSerializerOptions);
    }

    private static async Task<IResult> HandleCreateBot(HttpContext context)
    {
        // Check payload
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 400;
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        CreateBotPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<CreateBotPayload>();
        }
        catch (JsonException ex)
        {
            Utilities.Log(LogLevel.DEBUG, $"Failed to parse JSON: {ex.Message}");
            context.Response.StatusCode = 400;
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error = "Invalid request body"
            }, Utilities.JsonSerializerOptions);
        }
        if (payload == null || string.IsNullOrWhiteSpace(payload.token) || string.IsNullOrWhiteSpace(payload.username))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new CreateBotResponse
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
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error = "Invalid session"
            }, Utilities.JsonSerializerOptions);
        }
        // Check developer status
        var user = await Program.Database.GetUser(session.Username) ??
                   throw new Exception("Unable to get user from session");
        if (!user.Developer)
        {
            context.Response.StatusCode = 403;
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error = "You must be an approved developer to create and manage bots."
            }, Utilities.JsonSerializerOptions);
        }
        // Check bot username
        if (await Program.Database.GetBot(payload.username) != null ||
            await Program.Database.GetUser(payload.username) != null)
        {
            context.Response.StatusCode = 400;
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error = "That username is taken."
            }, Utilities.JsonSerializerOptions);
        }

        if (!Utilities.ValidateUsername(payload.username))
        {
            context.Response.StatusCode = 400;
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error =
                    "Usernames can contain only numbers, letters, spaces, dashes, underscores, and dots, and must be between 3 and 20 characters."
            }, Utilities.JsonSerializerOptions);
        }
        // Generate token
        string token = Utilities.RandomString(64);
        // Create bot
        await Program.Database.CreateBot(payload.username, token, user.Username);
        return Results.Json(new CreateBotResponse
        {
            success = true,
            token = token
        }, Utilities.JsonSerializerOptions);
    }
}