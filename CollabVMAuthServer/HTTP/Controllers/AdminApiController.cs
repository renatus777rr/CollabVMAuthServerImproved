using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
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

[Route("api/v1/admin")]
[ApiController]
public class AdminApiController : ControllerBase
{
    private readonly CollabVMAuthDbContext _dbContext;
    public AdminApiController(CollabVMAuthDbContext dbContext) {
        this._dbContext = dbContext;
    }

    [HttpPost]
    [Route("ipban")]
    [Authorize("Staff")]
    public async Task<IResult> HandleIPBan(IPBanPayload payload)
    {
        var ip = IPAddress.Parse(payload.ip).GetAddressBytes();
        // Find or create ban
        var ban = await _dbContext.IpBans.FirstOrDefaultAsync(b => b.Ip == ip);

        if (payload.banned)
        {
            ban ??= new IpBan { Ip = ip };
            ban.Reason = payload.reason;
            ban.BannedBy = HttpContext.User.FindFirstValue("username");
            ban.BannedAt = DateTime.UtcNow;
        }
        else
        {
            if (ban == null) {
                return Results.Json(new ApiResponse {
                    success = false,
                    error = "IP is not banned."
                }, statusCode: 400);
            }
            _dbContext.IpBans.Remove(ban);
        }
        await _dbContext.SaveChangesAsync();
        return Results.Json(new ApiResponse
        {
            success = true
        });
    }

    [HttpPost]
    [Route("ban")]
    [Authorize("Staff")]
    public async Task<IResult> HandleBanUser(BanUserPayload payload)
    {
        // Check target user
        var targetUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == payload.username);
        if (targetUser == null)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "User not found"
            });
        }
        // Set ban
        targetUser.Banned = payload.banned;
        targetUser.BanReason = payload.reason;
        await _dbContext.SaveChangesAsync();

        return Results.Json(new ApiResponse
        {
            success = true
        });
    }

    [HttpPost]
    [Route("updatebot")]
    [Authorize("Staff")]
    public async Task<IResult> HandleAdminUpdateBot(AdminUpdateBotPayload payload)
    {
        // Check target bot
        var targetBot = await _dbContext.Bots.FirstOrDefaultAsync(b => b.Username == payload.username);
        if (targetBot == null)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Bot not found"
            });
        }
        // Make sure at least one field is being updated
        if (payload.rank == null)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "No fields to update"
            });
        }
        // Moderators cannot promote bots to admin, and can only promote their own bots to moderator
        if ((Rank)payload.rank == Rank.Admin && HttpContext.User.FindFirstValue("rank") == "3")
        {
            HttpContext.Response.StatusCode = 403;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Insufficient permissions"
            });
        }
        if (targetBot.Owner != uint.Parse(HttpContext.User.FindFirstValue("id")!) && HttpContext.User.FindFirstValue("rank") == "3")
        {
            HttpContext.Response.StatusCode = 403;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "Insufficient permissions"
            });
        }
        // Check rank
        uint? rank = payload.rank;
        if (rank != null) { 
            if (rank < 1 || rank > 3) {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new ApiResponse
                {
                    success = false,
                    error = "Invalid rank"
                });
            }
            targetBot.CvmRank = payload.rank.Value;
        }

        // Update
        await _dbContext.SaveChangesAsync();

        return Results.Json(new ApiResponse
        {
            success = true
        });
    }

    [HttpPost]
    [Route("updateuser")]
    [Authorize("Staff")]
    public async Task<IResult> HandleAdminUpdateUser(AdminUpdateUserPayload payload)
    {
        // Check target user
        var targetUser = await _dbContext.Users.Include(u => u.Bots).FirstOrDefaultAsync(u => u.Username == payload.username);
        if (targetUser == null)
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new ApiResponse
            {
                success = false,
                error = "User not found"
            });
        }
        // Check rank
        uint? rank = payload.rank;
        if (rank != null) {
            if (rank < 1 || rank > 3) {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new ApiResponse
                {
                    success = false,
                    error = "Invalid rank"
                });
            }

            // Moderators cannot change ranks
            if (HttpContext.User.FindFirstValue("rank") == "3") {
                HttpContext.Response.StatusCode = 403;
                return Results.Json(new ApiResponse
                {
                    success = false,
                    error = "Insufficient permissions"
                });
            }

            targetUser.CvmRank = rank.Value;
        }
        // Check developer
        if (payload.developer != null) {
            targetUser.Developer = payload.developer.Value;
        }

        if (targetUser.Developer == false) {
            targetUser.Bots.Clear();
        }

        // Update
        await _dbContext.SaveChangesAsync();

        return Results.Json(new ApiResponse
        {
            success = true
        });
    }

    [HttpPost]
    [Route("users")]
    [Authorize("Staff")]
    public async Task<IResult> HandleAdminUsers(AdminUsersPayload payload)
    {
        // Validate orderBy
        if (payload.orderBy != null && !new string[] { "id", "username", "email", "date_of_birth", "cvm_rank", "banned", "created" }.Contains(payload.orderBy))
        {
            HttpContext.Response.StatusCode = 400;
            return Results.Json(new AdminUsersResponse
            {
                success = false,
                error = "Invalid orderBy"
            });
        }
        // Filter IP
        IPAddress? filterIp = null;
        if (payload.filterIp != null)
        {
            if (!IPAddress.TryParse(payload.filterIp, out filterIp)) {
                HttpContext.Response.StatusCode = 400;
                return Results.Json(new AdminUsersResponse
                {
                    success = false,
                    error = "Invalid filterIp"
                });
            }
        }

        // Get users
        IQueryable<User> result = _dbContext.Users;

        if (payload.filterUsername != null) {
            result = result.Where(u => u.Username.Contains(payload.filterUsername));
        }

        if (filterIp != null) {
            result = result.Where(u => u.RegistrationIp == filterIp.GetAddressBytes());
        }

        var orderBy = payload.orderBy ?? "id";
        var order = (Expression<Func<User, object>> k) => {
            if (payload.orderByDescending) {
                result = result.OrderByDescending(k);
            } else {
                result = result.OrderBy(k);
            }
        };
        switch (orderBy) {
            case "id":
                order(u => u.Id);
                break;
            case "username":
                order(u => u.Username);
                break;
            case "email":
                order(u => u.Email);
                break;
            case "date_of_birth":
                order(u => u.DateOfBirth);
                break;
            case "cvm_rank":
                order(u => u.CvmRank);
                break;
            case "banned":
                order(u => u.Banned);
                break;
            case "created":
                order(u => u.Created);
                break;
        }

        result = result.Skip((payload.page - 1) * payload.resultsPerPage).Take(payload.resultsPerPage);
        
        var users = await result.Select(user => new AdminUser
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            rank = user.CvmRank,
            banned = user.Banned,
            banReason = user.BanReason ?? "",
            dateOfBirth = user.DateOfBirth.ToString("yyyy-MM-dd"),
            dateJoined = user.Created.ToString("yyyy-MM-dd HH:mm:ss"),
            registrationIp = new IPAddress(user.RegistrationIp).ToString(),
            developer = user.Developer
        }).ToArrayAsync();

        return Results.Json(new AdminUsersResponse
        {
            success = true,
            users = users,
            totalPageCount = (int)Math.Ceiling(await _dbContext.Users.CountAsync() / (double)payload.resultsPerPage)
        });
    }
}