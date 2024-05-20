using System.Security.Cryptography;
using Google.Protobuf;
using Grpc.Core;
using Sqlmaster.Protobuf;

namespace checkmate.Services;

public class AuthenticationService : Authentication.AuthenticationBase
{
    private const int TokenLength = 24;
    private readonly IAuthenticatorService _authenticator;
    private readonly ILogger _logger;
    private static string? _temporaryPassword;

    public AuthenticationService(
        IAuthenticatorService authenticatorService,
        IDatabaseService databaseService,
        ILogger<AuthenticationService> logger)
    {
        _authenticator = authenticatorService;
        _logger = logger;

        if (_temporaryPassword != null) return;
        var anyUser = databaseService.DataSource.CreateCommand("select id from users").ExecuteScalar();
        if (anyUser != null) return;
        _temporaryPassword = RandomNumberGenerator.GetHexString(TokenLength);
        logger.LogWarning($"No user present in database. Temporary password generated: {_temporaryPassword}");
    }

    public override async Task<AuthorizationResponse> Authorize(AuthorizationRequest request, ServerCallContext context)
    {
        var userId = await _authenticator.GetUserIdOrNull(request.Password, request.DeviceName);
        if (userId == null)
        {
            if (_temporaryPassword != null && request.Password == _temporaryPassword)
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
}