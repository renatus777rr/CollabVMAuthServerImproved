using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace Computernewb.CollabVMAuthServer.Database;

public static class LegacyDbMigrator {
    /// <summary>
    /// The initial database migration that a pre-EF database will already be compatible with
    /// </summary>
    public const string INITIAL_MIGRATION_NAME = "20250505224256_InitialDbModel";

    /// <summary>
    /// Checks if a database was initialized by the legacy pre-EF methods. If so, create the migrations table and manually add the initial migration
    /// </summary>
    public static async Task CheckAndMigrate(CollabVMAuthDbContext context) {
        var logger = LoggerFactory.Create(Utilities.ConfigureLogging).CreateLogger("Computernewb.CollabVMAuthServer.Database.LegacyDbMigrator");
        // Check if initial migration is pending
        if ((await context.Database.GetAppliedMigrationsAsync()).Contains(INITIAL_MIGRATION_NAME)) {
            logger.LogDebug("Initial migration already applied, skipping legacy db check");
            return;
        }
        var conn = context.Database.GetDbConnection();
        // Ensure the connection is open
        if (conn.State != ConnectionState.Open) {
            logger.LogDebug("Opening DB connection");
            await conn.OpenAsync();
        }
        // Create command
        using var cmd = conn.CreateCommand();
        // Check if meta table exists
        cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'meta'";
        // If meta table doesn't exist, db is uninitialized
        if ((long)(await cmd.ExecuteScalarAsync() ?? 0) == 0) {
            logger.LogDebug("Database is uninitialized");
            return;
        }
        // Check database version
        cmd.CommandText = "SELECT val FROM meta WHERE setting = 'db_version'";
        var dbVer = (string?) await cmd.ExecuteScalarAsync();
        if (dbVer != "1") {
            // 1 was the only version ever used in the old format, so this should not happen
            throw new InvalidOperationException($"Invalid database state, cannot automatically migrate (Expected DB version `1`, got `{dbVer}`)");
        }
        // Database can be migrated
        logger.LogDebug("Legacy database schema detected. Automatically initializing migrations table");
        // Manually create migrations table
        var historyRepo = context.Database.GetService<IHistoryRepository>();
        cmd.CommandText = historyRepo.GetCreateIfNotExistsScript();
        logger.LogDebug("Migrations table create script: {cmd}", cmd.CommandText);
        await cmd.ExecuteNonQueryAsync();
        // Insert row for initial migration
        cmd.CommandText = historyRepo.GetInsertScript(
            new HistoryRow(
                INITIAL_MIGRATION_NAME,
                typeof(DbContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion
            )
        );
        logger.LogDebug("Migrations table insert script: {cmd}", cmd.CommandText);
        await cmd.ExecuteNonQueryAsync();
        logger.LogInformation("Successfully initialized migrations table");
        // Drop meta table
        cmd.CommandText = "DROP TABLE meta;";
        await cmd.ExecuteNonQueryAsync();
        logger.LogInformation("Dropped legacy meta table");
    }
}