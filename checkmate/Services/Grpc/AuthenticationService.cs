using System.Security.Cryptography;
using Google.Protobuf;
using Grpc.Core;
using Sqlmaster.Protobuf;

namespace checkmate.Services.Grpc;

public class AuthenticationService : Authentication.AuthenticationBase
{
    private const int TokenLength = 24;
    private readonly IAuthenticatorService _authenticator;
    private readonly ILogger _logger;
    private static readonly ICollection<string?> TemporaryPassword = [];

    public AuthenticationService(
        IAuthenticatorService authenticatorService,
        IDatabaseService databaseService,
        ILogger<AuthenticationService> logger)
    {
        _authenticator = authenticatorService;
        _logger = logger;

        if (TemporaryPassword.Count > 0) return;
        var anyUser = databaseService.DataSource.CreateCommand("select id from users").ExecuteScalar();
        if (anyUser != null) return;
        var pwdGen = RandomNumberGenerator.GetHexString(TokenLength);
        TemporaryPassword.Add(pwdGen);
        logger.LogWarning($"No user present in database. Temporary password generated: {pwdGen}");
    }

    public override async Task<AuthorizationResponse> Authorize(AuthorizationRequest request, ServerCallContext context)
    {
        var userId = await _authenticator.GetUserIdOrNull(request.Password, request.DeviceName);
        if (userId == null)
        {
            if (TemporaryPassword.Contains(request.Password))
            {
                userId = await _authenticator.AddToUser(request.Password, request.DeviceName);
            }
            else
            {
                return new AuthorizationResponse
                {
                    Allowed = false
                };
            }
        }

        var token = await _authenticator.AddToAuth(request.Os, userId.Value);
        return new AuthorizationResponse
        {
            Allowed = true,
            Token = ByteString.CopyFrom(token)
        };
    }

    public override async Task<RevokeTokenResponse> Revoke(RevokeTokenRequest request, ServerCallContext context)
    {
        var id = await _authenticator.GetUserIdFromToken(request.Token.ToByteArray());
        if (id == null)
        {
            return new RevokeTokenResponse
            {
                Ok = false
            };
        }
        var deviceName = await _authenticator.RevokeAuthOrNull(request.Token.ToByteArray());

        return new RevokeTokenResponse
        {
            Ok = true,
            DeviceName = deviceName
        };
    }

    public override async Task<AuthenticationResponse> Authenticate(AuthenticationRequest request, ServerCallContext context)
    {
        return new AuthenticationResponse
        {
            Allowed = await _authenticator.GetUserIdFromToken(request.Token.ToByteArray()) != null
        };
    }

    public override async Task AddUser(AddUserRequest request, IServerStreamWriter<AddUserResponse> responseStream, ServerCallContext context)
    {
        
    }
}