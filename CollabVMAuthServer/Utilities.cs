using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Computernewb.CollabVMAuthServer;

public enum LogLevel
{
    DEBUG,
    INFO,
    WARN,
    ERROR,
    FATAL
}


public static class Utilities
{
    public static JsonSerializerOptions JsonSerializerOptions => new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public static void Log(LogLevel level, string msg)
    {
#if !DEBUG
        if (level == LogLevel.DEBUG)
            return;
#endif
        StringBuilder logstr = new StringBuilder();
        logstr.Append("[");
        logstr.Append(DateTime.Now.ToString("G"));
        logstr.Append("] [");
        switch (level)
        {
            case LogLevel.DEBUG:
                logstr.Append("DEBUG");
                break;
            case LogLevel.INFO:
                logstr.Append("INFO");
                break;
            case LogLevel.WARN:
                logstr.Append("WARN");
                break;
            case LogLevel.ERROR:
                logstr.Append("ERROR");
                break;
            case LogLevel.FATAL:
                logstr.Append("FATAL");
                break;
            default:
                throw new ArgumentException("Invalid log level");
        }
        logstr.Append("] ");
        logstr.Append(msg);
        switch (level)
        {
            case LogLevel.DEBUG:
            case LogLevel.INFO:
                Console.WriteLine(logstr.ToString());
                break;
            case LogLevel.WARN:
            case LogLevel.ERROR:
            case LogLevel.FATAL:
                Console.Error.Write(logstr.ToString());
                break;
        }
    }

    public static bool ValidateUsername(string username)
    {
        return username.Length >= 3 &&
               username.Length <= 20 &&
               username[0] != ' ' &&
               username[^1] != ' ' &&
               new Regex("^[a-zA-Z0-9 \\-_\\.]+$").IsMatch(username);
    }

    public static bool ValidatePassword(string password)
    {
        return password.Length > 8 &&
               new Regex("[a-z]").IsMatch(password) &&
               new Regex("[A-Z]").IsMatch(password) &&
               new Regex("[!@#$%^&*()\\-_=+\\\\|\\[\\];:'\\\",<.>/?`~]").IsMatch(password) &&
               new Regex("[0-9]").IsMatch(password);
    }
}