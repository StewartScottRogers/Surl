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
/// <item><description>A Bearer token over plain text without <c>--allow-plaintext-auth</c>: <c>403 DENIED</c>, "credentials over plain text are refused".</description></item>
/// <item><description>A Bearer token over plain text without <c>--allow-plaintext-auth</c>: <c>403 DENIED</c>, "credentials over plain text are refused".</description></item>
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
            return await CheckWithoutAuthorizationAsync(reply, required, scope, cancellationToken);
        }

        var bearer = BearerSecret(authorization);
        if (bearer is not null && await RefusesPlaintextAsync(cancellationToken))
        {
            return new OciAccess(null, reply.Error(OciError.PlaintextCredentialsRefused));
        }

        var token = bearer is null ? null : tokens.Find(bearer);
        if (token is null)
        {
            return Refused(reply, OciError.Unauthorized, scope, "invalid_token");
        }

        return required == OciActions.None || token.Allows(name, required)
            ? new OciAccess(token, null)
            : Refused(reply, OciError.Denied, scope, "insufficient_scope");
    }

    private async Task<OciAccess> CheckWithoutAuthorizationAsync(OciReply reply, OciActions required, string? scope, CancellationToken cancellationToken) =>
        required <= OciActions.Pull && await AllowsAnonymousAsync(cancellationToken)
            ? new OciAccess(null, null)
            : Refused(reply, OciError.Unauthorized, scope, null);

    private static string? BearerSecret(string authorization) =>
        authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) ? authorization[BearerPrefix.Length..].Trim() : null;

    // A Bearer token is a secret: over plain text the policy must allow plain-text secrets
    // (--allow-plaintext-auth, or --allow-anonymous). The policy's own verdict on a probe login
    // carrying a password says so, as it does for Basic at /token.
    private async Task<bool> RefusesPlaintextAsync(CancellationToken cancellationToken) =>
        tlsSession is null
            && await policy.CheckPasswordLoginAsync(new PasswordLogin(OciRegistryProtocolServer.Scheme, null, ReadOnlyMemory<byte>.Empty, null), cancellationToken)
                == PasswordLoginVerdict.RefusedPlaintext;

    private async Task<bool> AllowsAnonymousAsync(CancellationToken cancellationToken) =>
        await policy.CheckPasswordLoginAsync(new PasswordLogin(OciRegistryProtocolServer.Scheme, null, null, tlsSession), cancellationToken)
            == PasswordLoginVerdict.AcceptedUnchecked;

    private OciAccess Refused(OciReply reply, OciError error, string? scope, string? challengeError) =>
        new(null, reply.Error(error, new KeyValuePair<string, string>("WWW-Authenticate", OciChallenge.Bearer(context, tlsSession is not null, scope, challengeError))));
}
