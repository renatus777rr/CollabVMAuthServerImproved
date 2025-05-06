using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Computernewb.CollabVMAuthServer.Database;
using Computernewb.CollabVMAuthServer.Database.Schema;
using Computernewb.CollabVMAuthServer.HTTP.Payloads;
using Computernewb.CollabVMAuthServer.HTTP.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Computernewb.CollabVMAuthServer.HTTP.Controllers;

[Route("api/v1/bots")]
[ApiController]
[Authorize("Developer")]
public class DeveloperApiController : ControllerBase {

    private readonly CollabVMAuthDbContext _dbContext;
    public DeveloperApiController(CollabVMAuthDbContext dbContext) {
        this._dbContext = dbContext;
    }

    [HttpPost]
    [Route("list")]
    public async Task<IResult> HandleListBots(ListBotsPayload payload)
    {
        // owner can only be specified by admins and moderators
        if (payload.owner != null && !(User.HasClaim("rank", "2") || User.HasClaim("rank", "3")))
        {
            HttpContext.Response.StatusCode = 403;
            return Results.Json(new ListBotsResponse
            {
                success = false,
                error = "Insufficient permissions"
            });
        }
        // Get bots
        // If the user is not an admin, they can only see their own bots
        IQueryable<Bot> result = _dbContext.Bots.Include(b => b.OwnerNavigation);

        if (payload.owner != null) {
            result = result.Where(b => b.OwnerNavigation.Username == payload.owner);
        } else if (!User.HasClaim("rank", "2") && !User.HasClaim("rank", "3")) {
            result = result.Where(b => b.OwnerNavigation.Username == User.FindFirstValue("username")!);
        }

        result = result.Skip((payload.page - 1) * payload.resultsPerPage).Take(payload.resultsPerPage);

        var bots = await result.Select(bot => new ListBot
        {
            id = (int)bot.Id,
            username = bot.Username,
            rank = bot.CvmRank,
            owner = bot.OwnerNavigation.Username,
            created = bot.Created.ToString("yyyy-MM-dd HH:mm:ss")
            
        }).ToArrayAsync();

        return Results.Json(new ListBotsResponse
        {
            success = true,
            totalPageCount = (int)Math.Ceiling(await _dbContext.Bots.CountAsync() / (double)payload.resultsPerPage),
            bots = bots
        });
    }

    [HttpPost]
    [Route("create")]
    public async Task<IResult> HandleCreateBot(CreateBotPayload payload)
    {
        // Check bot username
        if (await _dbContext.Users.AnyAsync(u => u.Username == payload.username) ||
            await _dbContext.Bots.AnyAsync(b => b.Username == payload.username))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error = "That username is taken."
            });
        }

        if (!Utilities.ValidateUsername(payload.username))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new CreateBotResponse
            {
                success = false,
                error =
                    "Usernames can contain only numbers, letters, spaces, dashes, underscores, and dots, and must be between 3 and 20 characters."
            });
        }
        // Generate token
        string token = Utilities.RandomString(64);
        // Create bot
        var bot = new Bot {
            Username = payload.username,
            Token = token,
            CvmRank = 1,
            Owner = uint.Parse(HttpContext.User.FindFirstValue("id")!),
            Created = DateTime.UtcNow
        };
        _dbContext.Bots.Add(bot);
        await _dbContext.SaveChangesAsync();

        return Results.Json(new CreateBotResponse
        {
            success = true,
            token = token
        });
    }
}