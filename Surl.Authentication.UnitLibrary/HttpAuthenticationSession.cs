using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// One HTTP connection's authentication, from <see cref="AuthenticationPolicy.StartHttpConnection"/>.
/// It answers each request in ADR-0032 section 4's order and holds each accepted method's
/// per-connection verifier, so NTLM's and Negotiate's handshakes die with the connection.
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

    private HttpAuthenticationVerdict JudgeWithoutCredentials(HttpAuthenticationRequest request) =>
        policy.Settings.Accounts.HasAccounts || request.IsWrite ? Challenge() : ProceedAnonymously;

    private async ValueTask<HttpAuthenticationVerdict> VerifyAsync(
        IHttpCredentialVerifier verifier,
        HttpAuthorization authorization,
        HttpAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        var check = await verifier.VerifyAsync(authorization.Credentials, request, cancellationToken)
            .ConfigureAwait(false);

        return await AnswerCheckAsync(check, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<HttpAuthenticationVerdict> AnswerCheckAsync(
        HttpCredentialCheck check, CancellationToken cancellationToken)
    {
        // A continuation step with no value to send would be a 401 without a challenge, which
        // RFC 9110 section 11.6.1 forbids; it is answered as a refusal.
        switch (check.Outcome)
        {
            case HttpCredentialOutcome.Accepted:
                return new HttpAuthenticationVerdict(
                    HttpAuthenticationOutcome.Proceed, check.WwwAuthenticateValues, check.AccountName);
            case HttpCredentialOutcome.Continue when check.WwwAuthenticateValues.Count > 0:
                return new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, check.WwwAuthenticateValues, null);
            default:
                await policy.WaitRefusalDelayAsync(cancellationToken).ConfigureAwait(false);

                return Challenge();
        }
    }

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
}
