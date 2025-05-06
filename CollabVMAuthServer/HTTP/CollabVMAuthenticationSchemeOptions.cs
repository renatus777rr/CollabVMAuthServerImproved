using Computernewb.CollabVMAuthServer.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Computernewb.CollabVMAuthServer.HTTP;

public class CollabVMAuthenticationSchemeOptions : AuthenticationSchemeOptions {
    public DbContextOptions<CollabVMAuthDbContext> DbContextOptions { get; set; } = new();
}