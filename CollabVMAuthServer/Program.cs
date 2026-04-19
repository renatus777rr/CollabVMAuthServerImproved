using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Computernewb.CollabVMAuthServer.Database;
using Computernewb.CollabVMAuthServer.HTTP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Computernewb.CollabVMAuthServer;

public class Program
{
    #pragma warning disable CS8618
    public static IConfig Config { get; private set; }
    public static hCaptchaClient? hCaptcha { get; private set; }
    public static Mailer? Mailer { get; private set; }
    public static string[] BannedPasswords { get; set; }
    #pragma warning restore CS8618
    public static readonly Random Random = new();

    private static readonly ILogger _logger
        = LoggerFactory.Create(Utilities.ConfigureLogging).CreateLogger<Program>();

    public static async Task<int> Main(string[] args)
    {
        if (EF.IsDesignTime)
            return 0;

        var rootCommand = new RootCommand("CollabVM Authentication Server");

        var configPathOption = new Option<string>(
            name: "--config-path",
            description: "Configuration file to use",
            getDefaultValue: () => "./config.toml"
        );
        rootCommand.Add(configPathOption);

        var migrateDbCommand = new Command("migrate-db", "Runs all pending database migrations");
        rootCommand.Add(migrateDbCommand);

        rootCommand.SetHandler(RunAuthServer, new AuthServerCliOptionsBinder(configPathOption));
        migrateDbCommand.SetHandler(MigrateDatabase, new AuthServerCliOptionsBinder(configPathOption));

        return await rootCommand.InvokeAsync(args);
    }

    public static async Task<int> MigrateDatabase(AuthServerContext context)
    {
        _logger.LogInformation("Running database migrations");
        var dbOptions = context.Config.MySQL.Configure().Options;
        var db = new CollabVMAuthDbContext(dbOptions); // calls .Options here

        await LegacyDbMigrator.CheckAndMigrate(db);

        _logger.LogInformation("Applying {cnt} migrations now...", (await db.Database.GetPendingMigrationsAsync()).Count());
        await db.Database.MigrateAsync();
        _logger.LogInformation("Finished migrations.");
        return 0;
    }

    public static async Task<int> RunAuthServer(AuthServerContext context)
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        _logger.LogInformation("CollabVM Authentication Server v{major}.{minor}.{revision} starting up",
            ver!.Major, ver.Minor, ver.Revision);

        Config = context.Config;

        // Configure DB options once (assuming MySQLConfig.Configure returns DbContextOptionsBuilder)
        var dbOptionsBuilder = context.Config.MySQL.Configure();
        var dbOptions = dbOptionsBuilder.Options;
        var db = new CollabVMAuthDbContext(dbOptions);

        if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            _logger.LogCritical("Database schema out of date. Please run migrations.");
            return 1;
        }

        var uc = await db.Users.CountAsync();
        _logger.LogInformation("{uc} users in database", uc);
        if (uc == 0)
            _logger.LogWarning("No users in database, first user will be promoted to admin");

        var cron = new Cron(dbOptions);
        await cron.Start();

        if (!Config.SMTP.Enabled && Config.Registration.EmailVerificationRequired)
        {
            _logger.LogCritical("Email verification is required but SMTP is disabled");
            return 1;
        }
        Mailer = Config.SMTP.Enabled ? new Mailer(Config.SMTP) : null;

        if (Config.hCaptcha.Enabled)
        {
            hCaptcha = new hCaptchaClient(Config.hCaptcha.Secret!, Config.hCaptcha.SiteKey!);
            _logger.LogInformation("hCaptcha enabled");
        }
        else
        {
            _logger.LogInformation("hCaptcha disabled");
        }

        BannedPasswords = await File.ReadAllLinesAsync("rockyou.txt");

        var builder = WebApplication.CreateBuilder();
        Utilities.ConfigureLogging(builder.Logging);

        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // Correct pattern: Configure returns DbContextOptionsBuilder, we don‘t call .Options here
        builder.Services.AddDbContext<CollabVMAuthDbContext>((services, options) =>
            context.Config.MySQL.Configure(options));

        // Configure forwarded headers
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (string proxy in context.Config.HTTP.TrustedProxies!)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = "CollabVM";
            options.RequireAuthenticatedSignIn = false;
        })
        .AddScheme<CollabVMAuthenticationSchemeOptions, CollabVMAuthenticationHandler>("CollabVM", options =>
        {
            options.DbContextOptions = dbOptions;
        });

        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, CollabVMAuthenticationMiddlewareResultHandler>();

        var authorization = builder.Services.AddAuthorizationBuilder();
        authorization.AddPolicy("User", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("type", "user");
        });
        authorization.AddPolicy("Staff", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("rank", "2", "3");
        });
        authorization.AddPolicy("Developer", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("developer", "1");
        });


        builder.WebHost.UseUrls(
            $"http://{Config.HTTP.Host ?? "127.0.0.1"}:{Config.HTTP.Port}"
        );

        builder.Services.AddCors();
        var app = builder.Build();

        if (context.Config.HTTP.UseXForwardedFor)
            app.UseForwardedHeaders();

        app.UseRouting();
        app.UseCors(cors => cors
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
        return 0;
    }
}
