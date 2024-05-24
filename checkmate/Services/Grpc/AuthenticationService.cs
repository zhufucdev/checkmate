using checkmate.Models;
using Google.Protobuf;
using Grpc.Core;
using Sqlmaster.Protobuf;
using Session = Sqlmaster.Protobuf.Session;

namespace checkmate.Services.Grpc;

public class AuthenticationService : Authentication.AuthenticationBase
{
    private readonly IAccountService _account;
    private readonly ILogger _logger;
    private readonly ITemporaryPasswordService _creation;

    public AuthenticationService(
        IAccountService accountService,
        IDatabaseService databaseService,
        ITemporaryPasswordService creation,
        ILogger<AuthenticationService> logger)
    {
        _account = accountService;
        _logger = logger;
        _creation = creation;

        if (creation.TemporaryPasswords.Count > 0) return;
        var anyUser = databaseService.DataSource.CreateCommand("select id from users").ExecuteScalar();
        if (anyUser != null) return;
        var pwdGen = creation.AddOneUsePassword();
        pwdGen.Tag = new UserCreator(pwdGen.Value, null, UserRole.RoleAdmin, null);
        logger.LogWarning($"No user present in database. Temporary password generated: {pwdGen.Value}");
    }

    public override async Task<AuthorizationResponse> Authorize(AuthorizationRequest request, ServerCallContext context)
    {
        var userId = (await _account.GetUserOrNull(request.Password, request.DeviceName))?.Id;
        if (userId == null)
        {
            var pwd = _creation.GetValidPassword(request.Password);
            if (pwd != null)
            {
                pwd.Invalidate();
                var model = pwd.Tag as UserCreator;
                userId = await _account.AddUser(new UserCreator(model?.Password ?? request.Password,
                    model?.DeviceName ?? request.DeviceName, model?.Role ?? UserRole.RoleLibrarian, model?.ReaderId));
                _logger.LogInformation(
                    $"New user ({request.DeviceName}) was created via access with temporary password.");
            }
            else
            {
                return new AuthorizationResponse
                {
                    Allowed = false
                };
            }
        }

        var token = await _account.BeginSession(request.Os, userId.Value);
        return new AuthorizationResponse
        {
            Allowed = true,
            Token = ByteString.CopyFrom(token)
        };
    }

    public override async Task<RevokeTokenResponse> Revoke(RevokeTokenRequest request, ServerCallContext context)
    {
        var id = await _account.GetUserIdFromToken(request.Token.ToByteArray());
        if (id == null)
        {
            return new RevokeTokenResponse
            {
                Allowed = false,
                Ok = false
            };
        }

        var session = await _account.GetSessionOrNull(request.SessionId);
        if (session == null)
        {
            return new RevokeTokenResponse
            {
                Allowed = true,
                Ok = false
            };
        }

        if (session.UserId != id)
        {
            return new RevokeTokenResponse
            {
                Allowed = false,
                Ok = false
            };
        }

        await _account.RevokeSessionOrNull(session.Id);
        return new RevokeTokenResponse
        {
            Allowed = true,
            Ok = true,
            Os = session.Os
        };
    }

    public override async Task<AuthenticationResponse> Authenticate(AuthenticationRequest request,
        ServerCallContext context)
    {
        var user = await _account.GetUserFromToken(request.Token.ToByteArray());
        return new AuthenticationResponse
        {
            Allowed = user != null,
            Role = user?.Role ?? UserRole.RoleUnspecific
        };
    }

    public override async Task GetSessions(GetRequest request, IServerStreamWriter<Session> responseStream,
        ServerCallContext context)
    {
        var userId = await _account.GetUserIdFromToken(request.Token.ToByteArray());
        if (userId == null)
        {
            return;
        }

        await foreach (var session in _account.GetSessions(userId.Value))
        {
            await responseStream.WriteAsync(new Session
            {
                Id = session.Id,
                Os = session.Os
            });
        }
    }

    public override async Task<GetUserResponse> GetUser(GetRequest request, ServerCallContext context)
    {
        var user = await _account.GetUserFromToken(request.Token.ToByteArray());
        return new GetUserResponse
        {
            Allowed = user != null,
            User = user
        };
    }

    public override async Task<UpdateResponse> ChangePassword(ChangePasswordRequest request, ServerCallContext context)
    {
        var user = await _account.GetUserFromToken(request.Token.ToByteArray());
        if (user == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }
        user = await _account.GetUserOrNull(request.Password, user.DeviceName);
        if (user == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var found = await _account.UpdatePassword(user.Id, request.NewPassword);
        return new UpdateResponse
        {
            Effect = found ? UpdateEffect.EffectOk : UpdateEffect.EffectNotFound
        };
    }
}