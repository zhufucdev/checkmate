namespace checkmate.Services;

public interface IAuthenticatorService
{
    Task<int?> GetUserIdOrNull(string password, string deviceName);
    Task<int?> GetUserIdFromToken(byte[] token);
    Task<byte[]> AddToAuth(string osName, int userId, int tokenLength = 24);
    Task<int> AddToUser(string password, string deviceName);
    Task<string?> RevokeAuthOrNull(byte[] token);
}