using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Computernewb.CollabVMAuthServer;

public static class Utilities
{
    public static void ConfigureLogging(ILoggingBuilder builder) {
        builder.ClearProviders();
        builder.AddConsole();
        #if DEBUG
        builder.SetMinimumLevel(LogLevel.Debug);
        #else
        builder.SetMinimumLevel(LogLevel.Warning);
        #endif
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

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        StringBuilder str = new StringBuilder();
        Random rand = new Random();
        for (int i = 0; i < length; i++)
        {
            str.Append(chars[rand.Next(chars.Length)]);
        }
        return str.ToString();
    }
}