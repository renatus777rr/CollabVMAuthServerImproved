using System.Net;
using Computernewb.CollabVMAuthServer.HTTP;
using Tomlet;

namespace Computernewb.CollabVMAuthServer;

public class Program
{
    public static IConfig Config { get; private set; }
    public static Database Database { get; private set; }
    public static hCaptchaClient? hCaptcha { get; private set; }
    public static Mailer? Mailer { get; private set; }
    public static string[] BannedPasswords { get; set; }
    public static readonly Random Random = new Random();
    public static async Task Main(string[] args)
    {
        Utilities.Log(LogLevel.INFO, "CollabVM Authentication Server starting up");
        // Read config.toml
        string configraw;
        try
        {
            configraw = File.ReadAllText("config.toml");
        }
        catch (Exception ex)
        {
            Utilities.Log(LogLevel.FATAL, "Failed to read config.toml: " + ex.Message);
            Environment.Exit(1);
            return;
        }
        // Parse config.toml to IConfig
        try
        {
            Config = TomletMain.To<IConfig>(configraw);
        } catch (Exception ex)
        {
            Utilities.Log(LogLevel.FATAL, "Failed to parse config.toml: " + ex.Message);
            Environment.Exit(1);
            return;
        }
        // Initialize database
        Database = new Database(Config.MySQL);
        await Database.Init();
        Utilities.Log(LogLevel.INFO, "Connected to database");
        // Create mailer
        if (!Config.SMTP.Enabled && Config.Registration.EmailVerificationRequired)
        {
            Utilities.Log(LogLevel.FATAL, "Email verification is required but SMTP is disabled");
            Environment.Exit(1);
            return;
        }
        Mailer = Config.SMTP.Enabled ? new Mailer(Config.SMTP) : null;
        // Create hCaptcha client
        if (Config.hCaptcha.Enabled)
        {
            hCaptcha = new hCaptchaClient(Config.hCaptcha.Secret!, Config.hCaptcha.SiteKey!);
            Utilities.Log(LogLevel.INFO, "hCaptcha enabled");
        }
        else
        {
            Utilities.Log(LogLevel.INFO, "hCaptcha disabled");
        }
        // load password list
        BannedPasswords = await File.ReadAllLinesAsync("rockyou.txt");
        // Configure web server
        var builder = WebApplication.CreateBuilder(args);
#if DEBUG
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
#else
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
#endif
        builder.WebHost.UseKestrel(k =>
        {
            k.Listen(IPAddress.Parse(Config.HTTP.Host), Config.HTTP.Port);
        });
        builder.Services.AddCors();
        var app = builder.Build();
        app.UseRouting();
        // TODO: Make this more strict
        app.UseCors(cors => cors.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        app.Lifetime.ApplicationStarted.Register(() => Utilities.Log(LogLevel.INFO, $"Webserver listening on {Config.HTTP.Host}:{Config.HTTP.Port}"));
        // Register routes
        Routes.RegisterRoutes(app);
        AdminRoutes.RegisterRoutes(app);
        DeveloperRoutes.RegisterRoutes(app);
        app.Run();
    }
}