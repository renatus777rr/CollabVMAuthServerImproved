using Isopoh.Cryptography.Argon2;
using MySqlConnector;

namespace Computernewb.CollabVMAuthServer;

public class Database
{
    private readonly string connectionString;

    public Database(MySQLConfig config)
    {
        connectionString = new MySqlConnectionStringBuilder
            {
                Server = config.Host,
                UserID = config.Username,
                Password = config.Password,
                Database = config.Database
            }.ToString();
    }

    public async Task Init()
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS users (
                              id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                              username VARCHAR(20) NOT NULL UNIQUE KEY,
                              password TEXT NOT NULL,
                              email TEXT NOT NULL UNIQUE KEY,
                              email_verified BOOLEAN NOT NULL DEFAULT 0,
                              email_verification_code CHAR(8) DEFAULT NULL,
                              cvm_rank INT UNSIGNED NOT NULL DEFAULT 0,
                              banned BOOLEAN NOT NULL DEFAULT 0
                          );
                          """;
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS sessions (
                              token CHAR(32) NOT NULL PRIMARY KEY,
                              username VARCHAR(20) NOT NULL,
                              created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                              last_used TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                              FOREIGN KEY (username) REFERENCES users(username) ON UPDATE CASCADE ON DELETE CASCADE
                          )
                          """;
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task<User?> GetUser(string? username = null, string? email = null)
    {
        if (username == null && email == null)
            throw new ArgumentException("username or email must be provided");
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        if (username != null)
        {
            cmd.CommandText = "SELECT * FROM users WHERE username = @username";
            cmd.Parameters.AddWithValue("@username", username);
        }
        else if (email != null)
        {
            cmd.CommandText = "SELECT * FROM users WHERE email = @email";
            cmd.Parameters.AddWithValue("@email", email);
        }
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return new User
        {
            Id = reader.GetUInt32("id"),
            Username = reader.GetString("username"),
            Password = reader.GetString("password"),
            Email = reader.GetString("email"),
            EmailVerified = reader.GetBoolean("email_verified"),
            EmailVerificationCode = reader.GetString("email_verification_code"),
            Rank = (Rank)reader.GetUInt32("cvm_rank"),
            Banned = reader.GetBoolean("banned")
        };
    }

    public async Task RegisterAccount(string username, string email, string password, bool verified,
        string? verificationcode = null)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO users
                            (username, password, email, email_verified, email_verification_code)
                            VALUES
                            (@username, @password, @email, @email_verified, @email_verification_code)
                          """;
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@password", Argon2.Hash(password));
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@email_verified", verified);
        cmd.Parameters.AddWithValue("@email_verification_code", verificationcode);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetUserVerified(string username, bool verified)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE users SET email_verified = @verified WHERE username = @username";
        cmd.Parameters.AddWithValue("@verified", verified);
        cmd.Parameters.AddWithValue("@username", username);
        await cmd.ExecuteNonQueryAsync();
    }
}