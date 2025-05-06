using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Computernewb.CollabVMAuthServer.Database;

public class DesignTimeCollabVMAuthDbContextFactory : IDesignTimeDbContextFactory<CollabVMAuthDbContext> {
    public CollabVMAuthDbContext CreateDbContext(string[] args) {
        return new CollabVMAuthDbContext(
            new DbContextOptionsBuilder<CollabVMAuthDbContext>()
                .UseMySql(MariaDbServerVersion.LatestSupportedServerVersion).Options
        );
    }
}