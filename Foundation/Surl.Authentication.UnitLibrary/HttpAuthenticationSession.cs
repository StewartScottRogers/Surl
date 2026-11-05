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

        return request.AwsSigV4Service is null
            ? JudgeHttpRequest(request, cancellationToken)
            : JudgeServiceSignature(request, cancellationToken);
    }

    // An HTTP request, judged by --auth's methods in ADR-0032 section 4's order.
    private ValueTask<HttpAuthenticationVerdict> JudgeHttpRequest(
        HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        var authorization = HttpAuthorization.Find(request.Fields);
        if (policy.Settings.AllowAnonymous)
        {
            return JudgeAnonymously(authorization, request, cancellationToken);
        }

        if (authorization is not null && SendsRefusedPlaintextSecret(authorization.Method))
        {
            return ValueTask.FromResult(Forbidden);
        }

        return authorization is not null && verifiers.TryGetValue(authorization.Method, out var verifier)
            ? VerifyAsync(verifier, authorization, request, cancellationToken)
            : ValueTask.FromResult(JudgeWithoutCredentials(request));
    }

    // A request whose only method is Signature Version 4 for a named service (S3, ADR-0108
    // decision 4): no Authorization is anonymous under --allow-anonymous and refused otherwise,
    // undelayed since nothing was checked; another scheme is refused unchecked; a signature is
    // checked even under --allow-anonymous; every refusal is Forbidden with its reason.
    private ValueTask<HttpAuthenticationVerdict> JudgeServiceSignature(
        HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        var hasAuthorization = request.Fields.Any(field => string.Equals(field.Key, "Authorization", StringComparison.OrdinalIgnoreCase));
        var authorization = HttpAuthorization.Find(request.Fields);
        if (authorization is { Method: AuthenticationMethod.AwsSigV4 })
        {
            return VerifyAsync(policy.ServiceSignatureVerifier, authorization, request, cancellationToken);
        }

        if (hasAuthorization)
        {
            return ValueTask.FromResult(Forbidden with { Refusal = HttpAuthenticationRefusal.UnsupportedScheme });
        }

        return ValueTask.FromResult(policy.Settings.AllowAnonymous
            ? ProceedAnonymously
            : Forbidden with { Refusal = HttpAuthenticationRefusal.MissingCredentials });
    }

    // Under --allow-anonymous every request is served unchecked (ADR-0032 section 1), except a
    // Negotiate token once --keytab gave a Kerberos acceptor: a Kerberos ticket must still decrypt,
    // since the final token needs its session key; only its account match is skipped (ADR-0057
    // decision 8). The Negotiate verifier answers every other token AcceptedUnchecked.
    private ValueTask<HttpAuthenticationVerdict> JudgeAnonymously(
        HttpAuthorization? authorization, HttpAuthenticationRequest request, CancellationToken cancellationToken) =>
        authorization is { Method: AuthenticationMethod.Negotiate }
            && policy.Settings.KerberosAcceptor is not null
            && verifiers.TryGetValue(authorization.Method, out var verifier)
            ? VerifyAsync(verifier, authorization, request, cancellationToken)
            : ValueTask.FromResult(ProceedAnonymously);

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

        return await AnswerCheckAsync(check, authorization.Method, request.AwsSigV4Service is not null, cancellationToken).ConfigureAwait(false);
    }

    // Accepted and refused credentials carry the login note (ADR-0032, section 8); a
    // continuation step, and a login waiting for the body, checked nothing yet, so they carry none.
    private ValueTask<HttpAuthenticationVerdict> AnswerCheckAsync(
        HttpCredentialCheck check, AuthenticationMethod method, bool isServiceSignature, CancellationToken cancellationToken)
    {
        // A continuation step with no value to send would be a 401 without a challenge, which
        // RFC 9110 section 11.6.1 forbids; it is answered as a refusal, as is a login waiting
        // for a body with nothing to check it.
        switch (check.Outcome)
        {
            case HttpCredentialOutcome.Accepted:
                return ValueTask.FromResult(Accepted(check, method));
            case HttpCredentialOutcome.AcceptedUnchecked:
                return ValueTask.FromResult(new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, check.WwwAuthenticateValues, null));
            case HttpCredentialOutcome.Continue when check.WwwAuthenticateValues.Count > 0:
                return ValueTask.FromResult(new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, check.WwwAuthenticateValues, null));
            case HttpCredentialOutcome.AwaitingBody when check.CheckBody is not null:
                return ValueTask.FromResult(new HttpAuthenticationVerdict(
                    HttpAuthenticationOutcome.Proceed, [], check.AccountName, BodyCheck: new RequestBodyCheck(this, method, isServiceSignature, check.CheckBody)));
            default:
                return RefuseAsync(check, method, isServiceSignature, cancellationToken);
        }
    }

    private static HttpAuthenticationVerdict Accepted(HttpCredentialCheck check, AuthenticationMethod method) =>
        new(HttpAuthenticationOutcome.Proceed, check.WwwAuthenticateValues, check.AccountName, LoginChecked(method, check, true));

    private async ValueTask<HttpAuthenticationVerdict> RefuseAsync(
        HttpCredentialCheck check, AuthenticationMethod method, bool isServiceSignature, CancellationToken cancellationToken)
    {
        await policy.WaitRefusalDelayAsync(cancellationToken).ConfigureAwait(false);

        var refusal = isServiceSignature ? Forbidden : Challenge();

        return refusal with { CheckedLogin = LoginChecked(method, check, false), Refusal = check.Refusal };
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
        bool isServiceSignature,
        Func<ReadOnlyMemory<byte>, HttpCredentialCheck> checkBody) : IHttpRequestBodyCheck
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(
            ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var check = checkBody(bodySha256);

            return check.Outcome == HttpCredentialOutcome.Accepted
                ? ValueTask.FromResult(Accepted(check, method))
                : session.RefuseAsync(check, method, isServiceSignature, cancellationToken);
        }
    }
}
