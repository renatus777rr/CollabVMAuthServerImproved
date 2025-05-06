using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Computernewb.CollabVMAuthServer.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Computernewb.CollabVMAuthServer;

public class Cron
{
    private readonly DbContextOptions<CollabVMAuthDbContext> _dbContextOptions;
    private readonly ILogger _logger;
    public Cron(DbContextOptions<CollabVMAuthDbContext> dbContextOptions) {
        this._dbContextOptions = dbContextOptions;
        this._logger = LoggerFactory.Create(Utilities.ConfigureLogging).CreateLogger<Cron>();
    }

    private Timer timer = new Timer();
    
    public async Task Start()
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
    
    public void Stop()
    {
        timer.Stop();
        timer.Interval = 1000 * 60 * 10;
    }
    
    public async Task RunAll()
    {
       _logger.LogDebug("Running all cron jobs");
        var t = new List<Task>();
        t.Add(PurgeOldSessions());
        if (Program.Config.Registration!.EmailVerificationRequired) t.Add(ExpireAccounts());
        await Task.WhenAll(t);
        _logger.LogDebug("Finished running all cron jobs");
    }
    // Expire unverified accounts after 2 days. Don't purge if the code is null
    public async Task ExpireAccounts()
    {
        _logger.LogDebug("Purging unverified accounts");
        using var dbContext = new CollabVMAuthDbContext(_dbContextOptions);
        dbContext.Users.RemoveRange(dbContext.Users.Where(u => (!u.EmailVerified) && u.EmailVerificationCode != null && (u.Created < DateTime.UtcNow.AddDays(-2))));
        var a = await dbContext.SaveChangesAsync();
        _logger.LogInformation("Purged {a} unverified accounts", a);
    }
    
    public async Task PurgeOldSessions()
    {
        _logger.LogDebug("Purging old sessions");
        using var dbContext = new CollabVMAuthDbContext(_dbContextOptions);
        dbContext.Sessions.RemoveRange(dbContext.Sessions.Where(s => s.LastUsed < DateTime.UtcNow - TimeSpan.FromDays(Program.Config.Accounts!.SessionExpiryDays)));
        var a = await dbContext.SaveChangesAsync();
        _logger.LogInformation("Purged {a} old sessions", a);
    }
}