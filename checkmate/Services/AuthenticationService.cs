using Grpc.Core;
using Sqlmaster.Protobuf;

namespace checkmate.Services;

public class AuthenticationService : Authentication.AuthenticationBase
{
    public override async Task<AuthorizationResponse> Authorize(AuthorizationRequest request, ServerCallContext context)
    {
        return await base.Authorize(request, context);
    }

    public override async Task<RevokeTokenResponse> Revoke(RevokeTokenRequest request, ServerCallContext context)
    {
        return await base.Revoke(request, context);
    }
}