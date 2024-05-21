using System.Security.Cryptography;

namespace checkmate.Services;

public class DatabaseAuthenticatorService(IDatabaseService databaseService) : IAuthenticatorService
{
    public async Task<int?> GetUserIdOrNull(string password, string deviceName)
    {
        var passwordHash = PasswordCrypto.Hash(password);
        await using var verify =
            databaseService.DataSource.CreateCommand(
                "select id from users where password_hash = $1 and device_name = $2");
        verify.Parameters.Add(databaseService.CreateParameter(passwordHash));
        verify.Parameters.Add(databaseService.CreateParameter(deviceName));

        return (int?)await verify.ExecuteScalarAsync();
    }

    public async Task<byte[]> AddToAuth(string osName, int userId, int tokenLength)
    {
        await using var auth = databaseService.DataSource.CreateCommand(
            """
            insert into auth(token, os, user_id)
            values ($1, $2, $3)
            """
        );
        var token = RandomNumberGenerator.GetBytes(tokenLength);
        auth.Parameters.Add(databaseService.CreateParameter(token));
        auth.Parameters.Add(databaseService.CreateParameter(osName));
        auth.Parameters.Add(databaseService.CreateParameter(userId));
        return token;
    }

    public async Task<int?> GetUserIdFromToken(byte[] token)
    {
        await using var cmd = databaseService.DataSource.CreateCommand("select user_id from auth where token = $1");
        cmd.Parameters.Add(databaseService.CreateParameter(token));
        return (int?)await cmd.ExecuteScalarAsync();
    }

    public async Task<int> AddToUser(string password, string deviceName)
    {
        var hash = PasswordCrypto.Hash(password);
        await using var makeUser = databaseService.DataSource.CreateCommand(
            """
            insert into users(id, device_name, password_hash)
            values (1, $1, $2)
            returning id
            """
        );
        makeUser.Parameters.Add(databaseService.CreateParameter(deviceName));
        makeUser.Parameters.Add(databaseService.CreateParameter(hash));
        return (int)(await makeUser.ExecuteScalarAsync())!;
    }

    public async Task<string?> RevokeAuthOrNull(byte[] token)
    {
        await using var cmd =
            databaseService.DataSource.CreateCommand(
                "select device_name from auth join users on auth.user_id = users.id");
        return (string?)await cmd.ExecuteScalarAsync();
    }
}