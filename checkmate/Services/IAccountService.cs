using checkmate.Models;
using Sqlmaster.Protobuf;
using Session = Sqlmaster.Protobuf.Session;

namespace checkmate.Services;

public interface IAccountService
{
    Task<User?> GetUserOrNull(string password, string deviceName);
    Task<int?> GetUserIdFromToken(byte[] token);
    Task<User?> GetUserFromToken(byte[] token);
    Task<User?> GetUserOrNull(int userId);
    IAsyncEnumerable<User> GetUsers();
    Task<byte[]> BeginSession(string osName, int userId, int tokenLength = 24);
    Task<int> AddUser(UserCreator user);
    Task<bool> UpdateUser(int userId, User model);
    Task<bool> UpdatePassword(int userId, string password);
    Task<Models.Session?> RevokeSessionOrNull(int sessionId);
    Task<Models.Session?> GetSessionOrNull(int sessionId);
    IAsyncEnumerable<Session> GetSessions(int userId);

    public const int DefaultTokenLength = 24;
}