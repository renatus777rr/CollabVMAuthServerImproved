using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private readonly Timer _timer = new();
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public Cron(DbContextOptions<CollabVMAuthDbContext> dbContextOptions) {
        this._dbContextOptions = dbContextOptions;
        this._logger = LoggerFactory.Create(Utilities.ConfigureLogging).CreateLogger<Cron>();
    }
    
    public async Task Start()
    {
        _timer.Interval =
#if DEBUG
            1000 * 60;
#else
            1000 * 60 * 10;
#endif
        _timer.AutoReset = true;
        _timer.Elapsed += async (_, _) => await RunAll();
        await RunAll();
        _timer.Start();
    }
    
    public void Stop()
    {
        _timer.Stop();
        _timer.Interval = 1000 * 60 * 10;
    }
    
    public async Task RunAll()
    {
        if (!await _runLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            _logger.LogDebug("Running all cron jobs");
            var jobs = new List<Task> { PurgeOldSessions() };
            if (Program.Config.Registration!.EmailVerificationRequired)
            {
                jobs.Add(ExpireAccounts());
            }

            await Task.WhenAll(jobs);
            _logger.LogDebug("Finished running all cron jobs");
        }
        finally
        {
            _runLock.Release();
        }
    }

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