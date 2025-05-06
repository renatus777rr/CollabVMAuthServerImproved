using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Computernewb.CollabVMAuthServer.HTTP.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Computernewb.CollabVMAuthServer.HTTP;

public class CollabVMAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden) {
            context.Response.StatusCode = 403;
            var requirement = authorizeResult.AuthorizationFailure!.FailedRequirements.First();
            if (requirement is ClaimsAuthorizationRequirement req) {
                if (req.ClaimType == "rank") {
                    await context.Response.WriteAsJsonAsync(new ApiResponse {
                        success = false,
                        error = "You do not have the correct rank to do that."
                    });
                    return;
                } else if (req.ClaimType == "developer") {
                    await context.Response.WriteAsJsonAsync(new ApiResponse {
                        success = false,
                        error = "You must be a developer to do that."
                    });
                    return;
                }
            }
            await context.Response.WriteAsJsonAsync(new ApiResponse {
                success = false,
                error = "Access forbidden."
            });
            return;
        } else if (authorizeResult.Challenged) {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new ApiResponse {
                success = false,
                error = "You need to login to do that."
            });
            return;
        }

        // Fall back to default handler
        await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}