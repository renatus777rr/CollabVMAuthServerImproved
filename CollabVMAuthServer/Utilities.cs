using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Computernewb.CollabVMAuthServer;

public static class Utilities
{
    private static readonly Regex UsernameRegex = new("^[a-zA-Z0-9 \\-_\\.]+$", RegexOptions.Compiled);
    private static readonly Regex LowercaseRegex = new("[a-z]", RegexOptions.Compiled);
    private static readonly Regex UppercaseRegex = new("[A-Z]", RegexOptions.Compiled);
    private static readonly Regex SymbolRegex = new("[!@#$%^&*()\\-_=+\\\\|\\[\\];:'\\\",<.>/?`~]", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new("[0-9]", RegexOptions.Compiled);
    private const string RandomChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

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
               UsernameRegex.IsMatch(username);
    }

    public static bool ValidatePassword(string password)
    {
        return password.Length > 8 &&
               LowercaseRegex.IsMatch(password) &&
               UppercaseRegex.IsMatch(password) &&
               SymbolRegex.IsMatch(password) &&
               NumberRegex.IsMatch(password);
    }

    public static string RandomString(int length)
    {
        StringBuilder str = new(length);
        for (int i = 0; i < length; i++)
        {
            str.Append(RandomChars[RandomNumberGenerator.GetInt32(RandomChars.Length)]);
        }
        return str.ToString();
    }
}