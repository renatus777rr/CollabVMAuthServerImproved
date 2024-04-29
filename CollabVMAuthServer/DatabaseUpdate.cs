using System.Collections.ObjectModel;

namespace Computernewb.CollabVMAuthServer;

public static class DatabaseUpdate
{
    public const int CurrentVersion = 1;

    private static ReadOnlyDictionary<int, Func<Database, Task>> Updates = new Dictionary<int, Func<Database, Task>>()
    {
        { 1, async db =>
        {
           // Update to version 1
           // Add ban_reason column to users table
           await db.ExecuteNonQuery("ALTER TABLE users ADD COLUMN ban_reason TEXT DEFAULT NULL");
        }},
    }.AsReadOnly();

    public async static Task Update(Database db)
    {
        var version = await db.GetDatabaseVersion();
        if (version == -1) throw new InvalidOperationException("Uninitialized database cannot be updated");
        if (version == CurrentVersion) return;
        if (version > CurrentVersion) throw new InvalidOperationException("Database version is newer than the server supports");
        Utilities.Log(LogLevel.INFO, $"Updating database from version {version} to {CurrentVersion}");
        for (int i = version + 1; i <= CurrentVersion; i++)
        {
            if (!Updates.TryGetValue(i, out var update)) throw new InvalidOperationException($"No update available for version {i}");
            await update(db);
        }
        await db.SetDatabaseVersion(CurrentVersion);
    }
}