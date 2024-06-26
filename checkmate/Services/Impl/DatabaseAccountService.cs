using System.Data.Common;
using System.Security.Cryptography;
using checkmate.Models;
using Sqlmaster.Protobuf;
using Session = checkmate.Models.Session;

namespace checkmate.Services.Impl;

public class DatabaseAccountService(IDatabaseService database) : IAccountService
{
    private static User _parseUser(DbDataReader reader)
    {
        var user = new User
        {
            Id = reader.GetInt32(0),
            DeviceName = reader.GetString(1),
            Role = (UserRole)reader.GetInt16(2),
        };
        if (!reader.IsDBNull(3))
        {
            user.ReaderId = reader.GetGuid(3).ToString();
        }

        return user;
    }

    public async Task<User?> GetUserOrNull(string password, string deviceName)
    {
        var passwordHash = PasswordCrypto.Hash(password);
        await using var cmd =
            database.DataSource.CreateCommand(
                """
                select id, device_name, role, reader_id from users
                where password_hash = $1 and device_name = $2
                """);
        cmd.Parameters.Add(database.CreateParameter(passwordHash));
        cmd.Parameters.Add(database.CreateParameter(deviceName));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return _parseUser(reader);
        }

        return null;
    }

    public async IAsyncEnumerable<User> GetUsers()
    {
        await using var cmd = database.DataSource.CreateCommand("select device_name, role, reader_id from users");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var user = new User
            {
                DeviceName = reader.GetString(0),
                Role = (UserRole)reader.GetInt16(1),
            };
            if (!reader.IsDBNull(2))
            {
                user.ReaderId = reader.GetGuid(2).ToString();
            }

            yield return user;
        }
    }

    public async Task<byte[]> BeginSession(string osName, int userId, int tokenLength)
    {
        await using var auth = database.DataSource.CreateCommand(
            """
            insert into auth(token, os, user_id)
            values ($1, $2, $3)
            """
        );
        var token = RandomNumberGenerator.GetBytes(tokenLength);
        auth.Parameters.Add(database.CreateParameter(token));
        auth.Parameters.Add(database.CreateParameter(osName));
        auth.Parameters.Add(database.CreateParameter(userId));
        await auth.ExecuteNonQueryAsync();
        return token;
    }

    public async Task UpdateSessionAccessTime(byte[] token)
    {
        await using var cmd = database.DataSource.CreateCommand("update auth set last_access = now() where token = $1");
        cmd.Parameters.Add(database.CreateParameter(token));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int?> GetUserIdFromToken(byte[] token)
    {
        await UpdateSessionAccessTime(token);
        await using var cmd = database.DataSource.CreateCommand("select user_id from auth where token = $1");
        cmd.Parameters.Add(database.CreateParameter(token));
        return (int?)await cmd.ExecuteScalarAsync();
    }

    public async Task<User?> GetUserFromToken(byte[] token)
    {
        await UpdateSessionAccessTime(token);
        await using var cmd = database.DataSource.CreateCommand(
            """
            select users.id, device_name, role, reader_id
            from users join auth on auth.user_id = users.id 
            where token = $1
            """);
        cmd.Parameters.Add(database.CreateParameter(token));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return _parseUser(reader);
        }

        return null;
    }

    public async Task<User?> GetUserOrNull(int userId)
    {
        await using var cmd =
            database.DataSource.CreateCommand("select id, device_name, role, reader_id from users where id = $1");
        cmd.Parameters.Add(database.CreateParameter(userId));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return _parseUser(reader);
        }

        return null;
    }

    public async Task<int> AddUser(UserCreator user)
    {
        var hash = PasswordCrypto.Hash(user.Password);
        await using var makeUser = database.DataSource.CreateCommand(
            """
            insert into users(device_name, password_hash, role, reader_id)
            values ($1, $2, $3, $4)
            returning id
            """
        );
        makeUser.Parameters.Add(database.CreateParameter(user.DeviceName));
        makeUser.Parameters.Add(database.CreateParameter(hash));
        makeUser.Parameters.Add(database.CreateParameter((short)user.Role));
        makeUser.Parameters.Add(user.ReaderId != null
            ? database.CreateParameter(user.ReaderId)
            : database.CreateParameter(DBNull.Value, "uuid"));

        return (int)(await makeUser.ExecuteScalarAsync())!;
    }

    public async Task<bool> UpdateUser(int userId, User model)
    {
        await using var cmd = database.DataSource.CreateCommand(
            """
                update users
                set (id, device_name, role, reader_id) = ($1, $2, $3, $4)
                where id = $5
            """);
        cmd.Parameters.Add(database.CreateParameter(model.Id));
        cmd.Parameters.Add(database.CreateParameter(model.DeviceName));
        cmd.Parameters.Add(database.CreateParameter((int)model.Role));
        cmd.Parameters.Add(database.CreateParameter(model.ReaderId));
        cmd.Parameters.Add(userId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UpdatePassword(int userId, string password)
    {
        await using var cmd = database.DataSource.CreateCommand("update users set password_hash = $1 where id = $2");
        var hash = PasswordCrypto.Hash(password);
        cmd.Parameters.Add(database.CreateParameter(hash));
        cmd.Parameters.Add(database.CreateParameter(userId));
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteUser(int userId)
    {
        await using var cmd = database.DataSource.CreateCommand("delete from users where id = $1");
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private Session _parseSession(DbDataReader reader)
    {
        var buf = new byte[IAccountService.DefaultTokenLength];
        reader.GetBytes(1, 0, buf, 0, buf.Length);
        return new Session(reader.GetInt32(0), buf, reader.GetString(2), reader.GetInt32(3), reader.GetDateTime(4));
    }

    public async Task<Session?> RevokeSessionOrNull(int sessionId)
    {
        await using var cmd =
            database.DataSource.CreateCommand(
                "delete from auth where id = $1 returning id, token, os, user_id, last_access");
        cmd.Parameters.Add(database.CreateParameter(sessionId));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return _parseSession(reader);
        }

        return null;
    }

    public async Task<Session?> GetSessionOrNull(int sessionId)
    {
        await using var cmd = database.DataSource.CreateCommand(
            "select id, token, os, user_id, last_access from auth where id = $1");
        cmd.Parameters.Add(database.CreateParameter(sessionId));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return _parseSession(reader);
        }

        return null;
    }

    public async IAsyncEnumerable<Session> GetSessions(int userId)
    {
        await using var cmd = database.DataSource.CreateCommand(
            "select id, token, os, user_id, last_access from auth where user_id = $1");
        cmd.Parameters.Add(database.CreateParameter(userId));
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            yield return _parseSession(reader);
        }
    }

    public async Task<Session?> GetSessionFromToken(byte[] token)
    {
        await using var cmd = database.DataSource.CreateCommand(
            "select id, token, os, user_id, last_access from auth where token = $1");
        cmd.Parameters.Add(database.CreateParameter(token));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return _parseSession(reader);
        }

        return null;
    }
}