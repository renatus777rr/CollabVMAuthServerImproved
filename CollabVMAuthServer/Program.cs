// Program.cs
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Computernewb.CollabVMAuthServer;

public class Program
{
    public static IConfig Config { get; private set; } = null!;
    public static hCaptchaClient? hCaptcha { get; private set; }
    public static Mailer? Mailer { get; private set; }
    public static string[] BannedPasswords { get; private set; } = null!;
    public static readonly Random Random = new();

    private static readonly ILogger<Program> _logger = LoggerFactory.Create(Utilities.ConfigureLogging).CreateLogger<Program>();

    public static async Task<int> Main(string[] args)
    {
        if (EF.IsDesignTime)
            return 0;

        var rootCommand = new RootCommand("CollabVM Authentication Server");

        var configPathOption = new Option<string>(
            "--config-path",
            () => "./config.toml",
            "Configuration file to use");
        rootCommand.Add(configPathOption);

        var migrateDbCommand = new Command("migrate-db", "Runs all pending database migrations");
        rootCommand.Add(migrateDbCommand);

        rootCommand.SetHandler(RunAuthServer, new AuthServerCliOptionsBinder(configPathOption));
        migrateDbCommand.SetHandler(MigrateDatabase, new AuthServerCliOptionsBinder(configPathOption));

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> MigrateDatabase(AuthServerContext context)
    {
        _logger.LogInformation("Running database migrations");

        var dbOptions = context.Config.MySQL.Configure().Options;
        await using var db = new CollabVMAuthDbContext(dbOptions);

        await LegacyDbMigrator.CheckAndMigrate(db);

        var pendingCount = await db.Database.GetPendingMigrationsAsync();
        _logger.LogInformation("Applying {Count} migrations", pendingCount.Count());

        await db.Database.MigrateAsync();

        _logger.LogInformation("Finished migrations");
        return 0;
    }

    private static async Task<int> RunAuthServer(AuthServerContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version!;
        _logger.LogInformation("CollabVM Authentication Server v{Version} starting up", version.ToString(3));

        Config = context.Config;

        var dbOptionsBuilder = Config.MySQL.Configure();
        var dbOptions = dbOptionsBuilder.Options;
        await using var db = new CollabVMAuthDbContext(dbOptions);

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            _logger.LogCritical("Database schema out of date. Please run 'migrate-db'.");
            return 1;
        }

        var userCount = await db.Users.CountAsync();
        _logger.LogInformation("Found {UserCount} users in database", userCount);
        if (userCount == 0)
            _logger.LogWarning("No users found. First user will be promoted to admin");

        var cron = new Cron(dbOptions);
        await cron.Start();

        if (!Config.SMTP.Enabled && Config.Registration.EmailVerificationRequired)
        {
            _logger.LogCritical("Email verification required but SMTP disabled");
            return 1;
        }

        Mailer = Config.SMTP.Enabled ? new Mailer(Config.SMTP) : null;

        if (Config.hCaptcha.Enabled)
        {
            hCaptcha = new hCaptchaClient(Config.hCaptcha.Secret, Config.hCaptcha.SiteKey);
            _logger.LogInformation("hCaptcha enabled");
        }
        else
        {
            _logger.LogInformation("hCaptcha disabled");
        }

        BannedPasswords = await File.ReadAllLinesAsync("rockyou.txt");

        var builder = WebApplication.CreateBuilder();
        Utilities.ConfigureLogging(builder.Logging);

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = 
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

        builder.Services.AddDbContext<CollabVMAuthDbContext>(Config.MySQL.Configure);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in Config.HTTP.TrustedProxies)
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

        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, CollabVMAuthorizationMiddlewareResultHandler>();

        var authorizationBuilder = builder.Services.AddAuthorizationBuilder();
        authorizationBuilder.AddPolicy("User", policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("type", "user"));
        authorizationBuilder.AddPolicy("Staff", policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("rank", "2", "3"));
        authorizationBuilder.AddPolicy("Developer", policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("developer", "1"));

        builder.WebHost.UseKestrel(serverOptions =>
        {
            serverOptions.Listen(IPAddress.Parse(Config.HTTP.Host), Config.HTTP.Port);
        });

        builder.Services.AddCors();

        var app = builder.Build();

        if (Config.HTTP.UseXForwardedFor)
        {
            app.UseForwardedHeaders();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

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
