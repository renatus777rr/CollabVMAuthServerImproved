using System.Data;
using System.Net;
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
                              date_of_birth DATE NOT NULL,
                              email_verified BOOLEAN NOT NULL DEFAULT 0,
                              email_verification_code CHAR(8) DEFAULT NULL,
                              password_reset_code CHAR(8) DEFAULT NULL,
                              cvm_rank INT UNSIGNED NOT NULL DEFAULT 1,
                              banned BOOLEAN NOT NULL DEFAULT 0,
                              registration_ip VARBINARY(16) NOT NULL,
                              created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                              developer BOOLEAN NOT NULL DEFAULT 0
                          );
                          """;
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS sessions (
                              token CHAR(32) NOT NULL PRIMARY KEY,
                              username VARCHAR(20) NOT NULL,
                              created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                              last_used TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                              last_ip VARBINARY(16) NOT NULL,
                              FOREIGN KEY (username) REFERENCES users(username) ON UPDATE CASCADE ON DELETE CASCADE
                          )
                          """;
        await cmd.ExecuteNonQueryAsync();
        // banned_by being NULL means the ban was automatic
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS ip_bans (
                              ip VARBINARY(16) NOT NULL PRIMARY KEY,
                              reason TEXT NOT NULL,
                              banned_by VARCHAR(20) DEFAULT NULL,
                              banned_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                          )
                          """;
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS bots (
                              id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                              username VARCHAR(20) NOT NULL UNIQUE KEY,
                              token CHAR(64) NOT NULL UNIQUE KEY,
                              cvm_rank INT UNSIGNED NOT NULL DEFAULT 1,
                              owner VARCHAR(20) NOT NULL,
                              created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                              FOREIGN KEY (owner) REFERENCES users(username) ON UPDATE CASCADE ON DELETE CASCADE
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
            DateOfBirth = reader.GetDateOnly("date_of_birth"),
            EmailVerified = reader.GetBoolean("email_verified"),
            EmailVerificationCode = reader.IsDBNull("email_verification_code") ? null : reader.GetString("email_verification_code"),
            PasswordResetCode = reader.IsDBNull("password_reset_code") ? null : reader.GetString("password_reset_code"),
            Rank = (Rank)reader.GetUInt32("cvm_rank"),
            Banned = reader.GetBoolean("banned"),
            RegistrationIP = new IPAddress(reader.GetFieldValue<byte[]>("registration_ip")),
            Joined = reader.GetDateTime("created"),
            Developer = reader.GetBoolean("developer")
        };
    }

    public async Task RegisterAccount(string username, string email, DateOnly dateOfBirth, string password, bool verified, IPAddress ip,
        string? verificationcode = null)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO users
                            (username, password, email, date_of_birth, email_verified, email_verification_code, registration_ip)
                            VALUES
                            (@username, @password, @email, @date_of_birth, @email_verified, @email_verification_code, @registration_ip)
                          """;
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@password", Argon2.Hash(password));
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.Add("@date_of_birth", MySqlDbType.Date).Value = dateOfBirth;
        cmd.Parameters.AddWithValue("@email_verified", verified);
        cmd.Parameters.AddWithValue("@email_verification_code", verificationcode);
        cmd.Parameters.AddWithValue("@registration_ip", ip.GetAddressBytes());
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
    
    public async Task SetVerificationCode(string username, string code)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE users SET email_verification_code = @code WHERE username = @username";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@username", username);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CreateSession(string username, string token, IPAddress ip)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions (token, username, last_ip) VALUES (@token, @username, @ip)";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@ip", ip.GetAddressBytes());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Session[]> GetSessions(string username)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT * FROM sessions WHERE username = @username";
        cmd.Parameters.AddWithValue("@username", username);
        await using var reader = await cmd.ExecuteReaderAsync();
        var sessions = new List<Session>();
        while (await reader.ReadAsync())
        {
            sessions.Add(new Session
            {
                Token = reader.GetString("token"),
                Username = reader.GetString("username"),
                Created = reader.GetDateTime("created"),
                LastUsed = reader.GetDateTime("last_used"),
                LastIP = new IPAddress(reader.GetFieldValue<byte[]>("last_ip"))
            });
        }
        return sessions.ToArray();
    }

    public async Task<Session?> GetSession(string token)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT * FROM sessions WHERE token = @token";
        cmd.Parameters.AddWithValue("@token", token);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return new Session
        {
            Token = reader.GetString("token"),
            Username = reader.GetString("username"),
            Created = reader.GetDateTime("created"),
            LastUsed = reader.GetDateTime("last_used"),
            LastIP = new IPAddress(reader.GetFieldValue<byte[]>("last_ip"))
        };
    }

    public async Task RevokeSession(string token)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE token = @token";
        cmd.Parameters.AddWithValue("@token", token);
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task RevokeAllSessions(string username)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE username = @username";
        cmd.Parameters.AddWithValue("@username", username);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateSessionLastUsed(string token, IPAddress ip)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET last_used = CURRENT_TIMESTAMP, last_ip = @ip WHERE token = @token";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@ip", ip.GetAddressBytes());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateUser(string username, string? newUsername = null, string? newPassword = null, string? newEmail = null, int? newRank = null, bool? developer = null)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        var updates = new List<string>();
        if (newUsername != null)
        {
            updates.Add("username = @newUsername");
            cmd.Parameters.AddWithValue("@newUsername", newUsername);
        }
        if (newPassword != null)
        {
            updates.Add("password = @newPassword");
            cmd.Parameters.AddWithValue("@newPassword", Argon2.Hash(newPassword));
        }
        if (newEmail != null)
        {
            updates.Add("email = @newEmail");
            cmd.Parameters.AddWithValue("@newEmail", newEmail);
        }

        if (newRank != null)
        {
            updates.Add("cvm_rank = @newRank");
            cmd.Parameters.AddWithValue("@newRank", newRank);
        }
        if (developer != null)
        {
            updates.Add("developer = @developer");
            cmd.Parameters.AddWithValue("@developer", developer);
        }
        cmd.CommandText = $"UPDATE users SET {string.Join(", ", updates)} WHERE username = @username";
        cmd.Parameters.AddWithValue("@username", username);
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task BanIP(IPAddress ip, string reason, string? bannedBy = null)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO ip_bans (ip, reason, banned_by) VALUES (@ip, @reason, @bannedBy)";
        cmd.Parameters.AddWithValue("@ip", ip.GetAddressBytes());
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@bannedBy", bannedBy);
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task UnbanIP(IPAddress ip)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM ip_bans WHERE ip = @ip";
        cmd.Parameters.AddWithValue("@ip", ip.GetAddressBytes());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IPBan?> CheckIPBan(IPAddress ip)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT * FROM ip_bans WHERE ip = @ip";
        cmd.Parameters.AddWithValue("@ip", ip.GetAddressBytes());
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return new IPBan
        {
            IP = new IPAddress(reader.GetFieldValue<byte[]>("ip")),
            Reason = reader.GetString("reason"),
            BannedBy = reader.IsDBNull("banned_by") ? null : reader.GetString("banned_by"),
            BannedAt = reader.GetDateTime("banned_at")
        };
    }
    
    public async Task SetPasswordResetCode(string username, string? code)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE users SET password_reset_code = @code WHERE username = @username";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@username", username);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<User[]> ListUsers(string? filterUsername = null, string orderBy = "id", bool descending = false)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        var where = new List<string>();
        if (filterUsername != null)
        {
            where.Add("username LIKE @filterUsername");
            cmd.Parameters.AddWithValue("@filterUsername", filterUsername);
        }
        cmd.CommandText = $"SELECT * FROM users {(where.Count > 0 ? "WHERE" : "")} {string.Join(" AND ", where)} ORDER BY {orderBy} {(descending ? "DESC" : "ASC")}";
        await using var reader = await cmd.ExecuteReaderAsync();
        var users = new List<User>();
        while (await reader.ReadAsync())
        {
            users.Add(new User
            {
                Id = reader.GetUInt32("id"),
                Username = reader.GetString("username"),
                Password = reader.GetString("password"),
                Email = reader.GetString("email"),
                DateOfBirth = reader.GetDateOnly("date_of_birth"),
                EmailVerified = reader.GetBoolean("email_verified"),
                EmailVerificationCode = reader.IsDBNull("email_verification_code") ? null : reader.GetString("email_verification_code"),
                PasswordResetCode = reader.IsDBNull("password_reset_code") ? null : reader.GetString("password_reset_code"),
                Rank = (Rank)reader.GetUInt32("cvm_rank"),
                Banned = reader.GetBoolean("banned"),
                RegistrationIP = new IPAddress(reader.GetFieldValue<byte[]>("registration_ip")),
                Joined = reader.GetDateTime("created"),
                Developer = reader.GetBoolean("developer")
            });
        }
        return users.ToArray();
    }
    
    public async Task CreateBot(string username, string token, string owner)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO bots (username, token, owner) VALUES (@username, @token, @owner)";
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@owner", owner);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Bot[]> ListBots(string? owner = null)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        var where = new List<string>();
        if (owner != null)
        {
            where.Add("owner = @owner");
            cmd.Parameters.AddWithValue("@owner", owner);
        }
        cmd.CommandText = $"SELECT * FROM bots {(where.Count > 0 ? "WHERE" : "")} {string.Join(" AND ", where)}";
        await using var reader = await cmd.ExecuteReaderAsync();
        var bots = new List<Bot>();
        while (await reader.ReadAsync())
        {
            bots.Add(new Bot
            {
                Id = reader.GetUInt32("id"),
                Username = reader.GetString("username"),
                Token = reader.GetString("token"),
                Rank = (Rank)reader.GetUInt32("cvm_rank"),
                Owner = reader.GetString("owner"),
                Created = reader.GetDateTime("created")
            });
        }
        return bots.ToArray();
    }

    public async Task UpdateBot(string username, string? newUsername = null, string? newToken = null, int? newRank = null)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        var updates = new List<string>();
        if (newUsername != null)
        {
            updates.Add("username = @username");
            cmd.Parameters.AddWithValue("@username", newUsername);
        }
        if (newToken != null)
        {
            updates.Add("token = @token");
            cmd.Parameters.AddWithValue("@token", newToken);
        }
        if (newRank != null)
        {
            updates.Add("cvm_rank = @rank");
            cmd.Parameters.AddWithValue("@rank", newRank);
        }
        cmd.CommandText = $"UPDATE bots SET {string.Join(", ", updates)} WHERE username = @username";
        cmd.Parameters.AddWithValue("@username", username);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteBots(string owner)
    {
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM bots WHERE owner = @owner";
        cmd.Parameters.AddWithValue("@owner", owner);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Bot?> GetBot(string? username = null, string? token = null)
    {
        if (username == null && token == null)
            throw new ArgumentException("username or token must be provided");
        await using var db = new MySqlConnection(connectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        if (username != null)
        {
            cmd.CommandText = "SELECT * FROM bots WHERE username = @username";
            cmd.Parameters.AddWithValue("@username", username);
        }
        else if (token != null)
        {
            cmd.CommandText = "SELECT * FROM bots WHERE token = @token";
            cmd.Parameters.AddWithValue("@token", token);
        }
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return new Bot
        {
            Id = reader.GetUInt32("id"),
            Username = reader.GetString("username"),
            Token = reader.GetString("token"),
            Rank = (Rank)reader.GetUInt32("cvm_rank"),
            Owner = reader.GetString("owner"),
            Created = reader.GetDateTime("created")
        };
    }
}