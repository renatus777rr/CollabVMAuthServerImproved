using System.Net;
using System.Reflection;
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
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        Utilities.Log(LogLevel.INFO, $"CollabVM Authentication Server v{ver.Major}.{ver.Minor}.{ver.Revision} starting up");
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
        // Get version before initializing
        int dbversion = await Database.GetDatabaseVersion();
        Utilities.Log(LogLevel.INFO, "Connected to database");
        Utilities.Log(LogLevel.INFO, dbversion == -1 ? "Initializing tables..." : $"Database version: {dbversion}");
        await Database.Init();
        // If database was version 0, that should now be set, as versioning did not exist then
        if (dbversion == 0) await Database.SetDatabaseVersion(0);
        // If database was -1, that means it was just initialized and we should set it to the current version
        if (dbversion == -1) await Database.SetDatabaseVersion(DatabaseUpdate.CurrentVersion);
        // Perform any necessary database updates
        await DatabaseUpdate.Update(Database);
        var uc = await Database.CountUsers();
        Utilities.Log(LogLevel.INFO, $"{uc} users in database");
        if (uc == 0) Utilities.Log(LogLevel.WARN, "No users in database, first user will be promoted to admin");
        // Init cron
        await Cron.Start();
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