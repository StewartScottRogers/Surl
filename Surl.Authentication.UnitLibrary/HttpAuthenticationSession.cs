using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// One HTTP connection's authentication, from <see cref="AuthenticationPolicy.StartHttpConnection"/>.
/// It answers each request in ADR-0032 section 4's order and holds each accepted method's
/// per-connection verifier, so NTLM's and Negotiate's handshakes die with the connection. The
/// account NTLM or Negotiate accepts is remembered for the connection's later requests without an
/// <c>Authorization</c> (ADR-0041, ADR-0044). A login that binds the body is answered with an
/// <see cref="IHttpRequestBodyCheck"/> the HTTP server asks once the body is read (ADR-0045).
/// </summary>
internal sealed class HttpAuthenticationSession : IHttpAuthenticationSession
{
    private static readonly HttpAuthenticationVerdict Forbidden =
        new(HttpAuthenticationOutcome.Forbidden, [], null);

    private static readonly HttpAuthenticationVerdict ProceedAnonymously =
        new(HttpAuthenticationOutcome.Proceed, [], null);

    private readonly AuthenticationPolicy policy;
    private readonly bool isEncrypted;
    private readonly Dictionary<AuthenticationMethod, IHttpCredentialVerifier> verifiers;

    // The account a connection-authenticating method (NTLM, Negotiate) last accepted on this connection.
    private string? connectionAccountName;

    public HttpAuthenticationSession(AuthenticationPolicy policy, bool isEncrypted)
    {
        this.policy = policy;
        this.isEncrypted = isEncrypted;
        verifiers = policy.HttpMethods.ToDictionary(method => method.Method, method => method.StartConnection());
    }

    public ValueTask<HttpAuthenticationVerdict> JudgeAsync(
        HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (policy.Settings.AllowAnonymous)
        {
            return ValueTask.FromResult(ProceedAnonymously);
        }

        var authorization = HttpAuthorization.Find(request.Fields);
        if (authorization is not null && SendsRefusedPlaintextSecret(authorization.Method))
        {
            return ValueTask.FromResult(Forbidden);
        }

        return authorization is not null && verifiers.TryGetValue(authorization.Method, out var verifier)
            ? VerifyAsync(verifier, authorization, request, cancellationToken)
            : ValueTask.FromResult(JudgeWithoutCredentials(request));
    }

    private bool SendsRefusedPlaintextSecret(AuthenticationMethod method) =>
        AuthenticationMethods.SendsPlaintextSecret(method) && !policy.OffersPlaintextSecrets(isEncrypted);

    // A connection NTLM or Negotiate logged in serves its later requests as that account, checking nothing,
    // so they carry no login note (ADR-0041, ADR-0044).
    private HttpAuthenticationVerdict JudgeWithoutCredentials(HttpAuthenticationRequest request)
    {
        if (connectionAccountName is not null)
        {
            return new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], connectionAccountName);
        }

        return policy.Settings.Accounts.HasAccounts || request.IsWrite ? Challenge() : ProceedAnonymously;
    }

    private async ValueTask<HttpAuthenticationVerdict> VerifyAsync(
        IHttpCredentialVerifier verifier,
        HttpAuthorization authorization,
        HttpAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        var check = await verifier.VerifyAsync(authorization.Credentials, request, cancellationToken)
            .ConfigureAwait(false);

        // A new handshake on the connection replaces its login, whatever it comes to.
        if (AuthenticationMethods.AuthenticatesConnection(authorization.Method))
        {
            connectionAccountName = check.Outcome == HttpCredentialOutcome.Accepted ? check.AccountName : null;
        }

        return await AnswerCheckAsync(check, authorization.Method, cancellationToken).ConfigureAwait(false);
    }

    // Accepted and refused credentials carry the login note (ADR-0032, section 8); a
    // continuation step, and a login waiting for the body, checked nothing yet, so they carry none.
    private ValueTask<HttpAuthenticationVerdict> AnswerCheckAsync(
        HttpCredentialCheck check, AuthenticationMethod method, CancellationToken cancellationToken)
    {
        // A continuation step with no value to send would be a 401 without a challenge, which
        // RFC 9110 section 11.6.1 forbids; it is answered as a refusal, as is a login waiting
        // for a body with nothing to check it.
        switch (check.Outcome)
        {
            case HttpCredentialOutcome.Accepted:
                return ValueTask.FromResult(Accepted(check, method));
            case HttpCredentialOutcome.Continue when check.WwwAuthenticateValues.Count > 0:
                return ValueTask.FromResult(new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, check.WwwAuthenticateValues, null));
            case HttpCredentialOutcome.AwaitingBody when check.CheckBody is not null:
                return ValueTask.FromResult(new HttpAuthenticationVerdict(
                    HttpAuthenticationOutcome.Proceed, [], null, BodyCheck: new RequestBodyCheck(this, method, check.CheckBody)));
            default:
                return RefuseAsync(check, method, cancellationToken);
        }
    }

    private static HttpAuthenticationVerdict Accepted(HttpCredentialCheck check, AuthenticationMethod method) =>
        new(HttpAuthenticationOutcome.Proceed, check.WwwAuthenticateValues, check.AccountName, LoginChecked(method, check, true));

    private async ValueTask<HttpAuthenticationVerdict> RefuseAsync(
        HttpCredentialCheck check, AuthenticationMethod method, CancellationToken cancellationToken)
    {
        await policy.WaitRefusalDelayAsync(cancellationToken).ConfigureAwait(false);

        return Challenge() with { CheckedLogin = LoginChecked(method, check, false) };
    }

    private static CheckedLogin LoginChecked(AuthenticationMethod method, HttpCredentialCheck check, bool isAccepted) =>
        new(AuthenticationMethods.AuthorizationSchemeOf(method), check.UserAsSent, isAccepted);

    private HttpAuthenticationVerdict Challenge()
    {
        var values = policy.HttpMethods
            .Where(method => !AuthenticationMethods.SendsPlaintextSecret(method.Method)
                || policy.OffersPlaintextSecrets(isEncrypted))
            .SelectMany(method => method.CreateChallenges())
            .ToList();

        return values.Count == 0
            ? Forbidden
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, values, null);
    }

    // The rest of a login that binds the body (ADR-0045): the verifier's answer for the body's
    // SHA-256, served with the login note when accepted and refused as any refusal is otherwise,
    // so a body that is not the one signed costs the refusal delay too.
    private sealed class RequestBodyCheck(
        HttpAuthenticationSession session,
        AuthenticationMethod method,
        Func<ReadOnlyMemory<byte>, HttpCredentialCheck> checkBody) : IHttpRequestBodyCheck
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(
            ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var check = checkBody(bodySha256);

            return check.Outcome == HttpCredentialOutcome.Accepted
                ? ValueTask.FromResult(Accepted(check, method))
                : session.RefuseAsync(check, method, cancellationToken);
        }
    }
}
