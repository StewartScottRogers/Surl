using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Decides whether a <c>/v2/</c> request may proceed, and as whom (ADR-0115, decisions 4.1 and
/// 4.4): by default it needs <c>Authorization: Bearer</c> with a live token this server issued
/// that grants the repository the action; under <c>--allow-anonymous</c> a read with no
/// <c>Authorization</c> is served as the anonymous caller.
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><description>No token, where one is needed: <c>401 UNAUTHORIZED</c>, the Bearer challenge with the scope needed.</description></item>
/// <item><description>An unknown or expired token, or another scheme: <c>401 UNAUTHORIZED</c>, the challenge with <c>error="invalid_token"</c>.</description></item>
/// <item><description>A token not granting the action: <c>403 DENIED</c>, the challenge with <c>error="insufficient_scope"</c>.</description></item>
/// </list>
/// A request that carries a token is checked even under <c>--allow-anonymous</c>; a bad token is
/// never served as anonymous.
/// </remarks>
/// <param name="policy">Says whether anonymous logins are allowed.</param>
/// <param name="tokens">The server's tokens.</param>
/// <param name="tlsSession">The connection's TLS session, or <see langword="null"/> on plain text.</param>
/// <param name="context">The exchange: the realm's listen URL and local endpoint.</param>
internal sealed class OciAccessCheck(IAuthenticationPolicy policy, OciTokenBook tokens, TlsSession? tlsSession, ExchangeContext context)
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Checks a request that needs <paramref name="required"/> in <paramref name="name"/>.
    /// </summary>
    /// <param name="reply">The reply to the request.</param>
    /// <param name="request">The request.</param>
    /// <param name="name">The repository, or empty for <c>/v2/</c>.</param>
    /// <param name="required">What the request needs; <see cref="OciActions.None"/> for <c>/v2/</c>.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The caller to serve the request as, or the refusal to answer it with.</returns>
    public async Task<OciAccess> CheckAsync(OciReply reply, HttpRequest request, string name, OciActions required, CancellationToken cancellationToken)
    {
        var scope = required == OciActions.None ? null : OciTokenScope.Format(name, required);
        var authorization = OciRequestFields.Value(request, "Authorization");
        if (authorization is null)
        {
            return required <= OciActions.Pull && await AllowsAnonymousAsync(cancellationToken)
                ? new OciAccess(null, null)
                : Refused(reply, OciError.Unauthorized, scope, null);
        }

        var token = authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? tokens.Find(authorization[BearerPrefix.Length..].Trim())
            : null;
        if (token is null)
        {
            return Refused(reply, OciError.Unauthorized, scope, "invalid_token");
        }

        return required == OciActions.None || token.Allows(name, required)
            ? new OciAccess(token, null)
            : Refused(reply, OciError.Denied, scope, "insufficient_scope");
    }

    private async Task<bool> AllowsAnonymousAsync(CancellationToken cancellationToken) =>
        await policy.CheckPasswordLoginAsync(new PasswordLogin(OciRegistryProtocolServer.Scheme, null, null, tlsSession), cancellationToken)
            == PasswordLoginVerdict.AcceptedUnchecked;

    private OciAccess Refused(OciReply reply, OciError error, string? scope, string? challengeError) =>
        new(null, reply.Error(error, new KeyValuePair<string, string>("WWW-Authenticate", OciChallenge.Bearer(context, tlsSession is not null, scope, challengeError))));
}
