using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace Computernewb.CollabVMAuthServer;

public static class Cron
{
    private static Timer timer = new Timer();
    
    public static async Task Start()
    {
        #if DEBUG
        timer.Interval = 1000 * 60; // 60 seconds
        #else
        timer.Interval = 1000 * 60 * 10; // 10 minutes
        #endif
        timer.Elapsed += async (sender, e) => await RunAll();
        await RunAll();
        timer.Start();
    }
    
    public static void Stop()
    {
        timer.Stop();
        timer.Interval = 1000 * 60 * 10;
    }
    
    public static async Task RunAll()
    {
        Utilities.Log(LogLevel.INFO, "Running all cron jobs");
        var t = new List<Task>();
        t.Add(PurgeOldSessions());
        if (Program.Config.Registration.EmailVerificationRequired) t.Add(ExpireAccounts());
        await Task.WhenAll(t);
        Utilities.Log(LogLevel.INFO, "Finished running all cron jobs");
    }
    // Expire unverified accounts after 2 days. Don't purge if the code is null
    public static async Task ExpireAccounts()
    {
        Utilities.Log(LogLevel.INFO, "Purging unverified accounts");
        var minDate = DateTime.UtcNow - TimeSpan.FromDays(2);
        int a = await Program.Database.ExecuteNonQuery("DELETE FROM users WHERE email_verified = 0 AND created < @minDate AND email_verification_code IS NOT NULL", 
        [
            new KeyValuePair<string, object>("minDate", minDate)
        ]);
        Utilities.Log(LogLevel.INFO, $"Purged {a} unverified accounts");
    }
    
    public static async Task PurgeOldSessions()
    {
        Utilities.Log(LogLevel.INFO, "Purging old sessions");
        var expiryDate = DateTime.UtcNow - TimeSpan.FromDays(Program.Config.Accounts.SessionExpiryDays);
        int a = await Program.Database.ExecuteNonQuery("DELETE FROM sessions WHERE last_used < @expiryDate", 
        [
            new KeyValuePair<string, object>("expiryDate", expiryDate)
        ]);
        Utilities.Log(LogLevel.INFO, $"Purged {a} old sessions");
    }
}