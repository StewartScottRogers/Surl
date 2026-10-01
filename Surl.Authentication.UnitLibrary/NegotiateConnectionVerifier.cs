using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// One HTTP connection's Negotiate handshake (RFC 4559, ADR-0040), carrying NTLM and, once
/// <c>--keytab</c> gave a Kerberos acceptor, Kerberos (ADR-0057 decision 8). The connection's
/// <see cref="NtlmHandshake"/> answers the NTLM messages, which arrive either bare
/// (<c>NTLMSSP</c>, as Windows' Negotiate package sends when it falls back to NTLM) and are
/// answered bare, or inside SPNEGO (RFC 4178) and are answered inside a <c>negTokenResp</c>. A
/// Kerberos AP-REQ, bare or as the optimistic token of a <c>NegTokenInit</c> whose first supported
/// mechanism is Kerberos, is answered in one leg by <see cref="NegotiateKerberosLogin"/>. Without
/// an acceptor, a <c>NegTokenInit</c> that does not offer NTLM, a Kerberos token and a malformed
/// token are refused (ADR-0032 decision 11), never thrown. Under <c>--allow-anonymous</c> every
/// token but a Kerberos one is <see cref="HttpCredentialOutcome.AcceptedUnchecked"/>.
/// </summary>
internal sealed class NegotiateConnectionVerifier(
    AccountBook accounts,
    INtlmServerChallengeSource serverChallenges,
    NegotiateKerberosLogin? kerberosLogin,
    bool isUnchecked)
    : IHttpCredentialVerifier
{
    private static readonly HttpCredentialCheck Refused = new(HttpCredentialOutcome.Refused, null, []);

    private static readonly HttpCredentialCheck AcceptedUnchecked = new(HttpCredentialOutcome.AcceptedUnchecked, null, []);

    private static readonly string AcceptCompleted =
        Negotiate(SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptCompleted, null, null));

    private readonly NtlmHandshake handshake = new(accounts, serverChallenges);

    // Whether this connection's last reply was a negTokenResp, so a negTokenResp may follow.
    private bool isSpnegoStarted;

    /// <summary>
    /// Answers one leg of the handshake.
    /// </summary>
    /// <param name="credentials">The base64 token after <c>Negotiate</c>.</param>
    /// <param name="request">The request the field arrived on.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The next token, accepted as the account (with SPNEGO's final token), or refused.</returns>
    public ValueTask<HttpCredentialCheck> VerifyAsync(
        string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var token = Base64Credentials.Decode(credentials);
        var wasSpnegoStarted = isSpnegoStarted;
        isSpnegoStarted = false;

        return ValueTask.FromResult(Answer(token, wasSpnegoStarted));
    }

    /// <summary>
    /// The <c>WWW-Authenticate</c> value carrying <paramref name="token"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns><c>Negotiate</c> and the token in base64.</returns>
    internal static string Negotiate(byte[] token) => $"Negotiate {Convert.ToBase64String(token)}";

    private HttpCredentialCheck Answer(byte[] token, bool wasSpnegoStarted)
    {
        if (kerberosLogin is not null && SpnegoToken.IsInitialContextToken(token))
        {
            return AnswerInitialContextToken(kerberosLogin, token);
        }

        return isUnchecked ? AcceptedUnchecked : AnswerNtlmToken(token, wasSpnegoStarted);
    }

    private HttpCredentialCheck AnswerNtlmToken(byte[] token, bool wasSpnegoStarted)
    {
        return token switch
        {
            _ when SpnegoToken.IsInitialContextToken(token) => AnswerNegTokenInit(token),
            _ when SpnegoToken.IsNegTokenResp(token) && wasSpnegoStarted => AnswerNegTokenResp(token),
            _ when SpnegoToken.IsNegTokenResp(token) => RefuseAndForget(),
            _ => AnswerBareNtlm(token),
        };
    }

    // With an acceptor, an InitialContextToken that is not SPNEGO is a bare Kerberos token, and a
    // NegTokenInit selecting Kerberos is answered by Kerberos; a NegTokenInit selecting NTLM runs
    // NTLM as ADR-0040 decides, and is served unchecked under --allow-anonymous.
    private HttpCredentialCheck AnswerInitialContextToken(NegotiateKerberosLogin kerberos, byte[] token)
    {
        var negTokenInit = SpnegoToken.ReadNegTokenInit(token);
        if (negTokenInit is null)
        {
            handshake.Forget();
            return kerberos.AnswerBare(token);
        }

        if (NegotiateKerberosLogin.SelectedKerberosOid(negTokenInit) is { } kerberosOid)
        {
            handshake.Forget();
            return kerberos.AnswerSpnego(negTokenInit, kerberosOid);
        }

        return isUnchecked ? AcceptedUnchecked : AnswerNegTokenInit(negTokenInit);
    }

    private HttpCredentialCheck AnswerBareNtlm(byte[] token)
    {
        var step = handshake.Answer(token);
        IReadOnlyList<string> values = step.ChallengeMessage is { } challengeMessage ? [Negotiate(challengeMessage)] : [];

        return new HttpCredentialCheck(step.Outcome, step.AccountName, values, step.UserAsSent);
    }

    private HttpCredentialCheck AnswerNegTokenInit(byte[] token) =>
        SpnegoToken.ReadNegTokenInit(token) is { } negTokenInit ? AnswerNegTokenInit(negTokenInit) : RefuseAndForget();

    // RFC 4178 section 3.2: the server picks NTLM from the client's list. When NTLM is the
    // client's first choice its optimistic token is NTLM's first message and is answered now;
    // otherwise that token belongs to another mechanism, so the reply only names NTLM and the
    // client starts it in its next token.
    private HttpCredentialCheck AnswerNegTokenInit(SpnegoNegTokenInit negTokenInit)
    {
        if (!negTokenInit.MechTypes.Contains(SpnegoToken.NtlmOid))
        {
            return RefuseAndForget();
        }

        if (negTokenInit.MechTypes[0] != SpnegoToken.NtlmOid || negTokenInit.MechToken is null)
        {
            handshake.Forget();
            isSpnegoStarted = true;

            return Continue(SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptIncomplete, SpnegoToken.NtlmOid, null));
        }

        return AnswerSpnegoNtlm(negTokenInit.MechToken, SpnegoToken.NtlmOid);
    }

    private HttpCredentialCheck AnswerNegTokenResp(byte[] token) =>
        SpnegoToken.ReadNegTokenResp(token) is { } negTokenResp
            ? AnswerSpnegoNtlm(negTokenResp.ResponseToken, null)
            : RefuseAndForget();

    private HttpCredentialCheck AnswerSpnegoNtlm(byte[] ntlmMessage, string? supportedMech)
    {
        var step = handshake.Answer(ntlmMessage);
        if (step.ChallengeMessage is { } challengeMessage)
        {
            isSpnegoStarted = true;

            return Continue(SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptIncomplete, supportedMech, challengeMessage));
        }

        IReadOnlyList<string> values = step.Outcome == HttpCredentialOutcome.Accepted ? [AcceptCompleted] : [];

        return new HttpCredentialCheck(step.Outcome, step.AccountName, values, step.UserAsSent);
    }

    private static HttpCredentialCheck Continue(byte[] negTokenResp) =>
        new(HttpCredentialOutcome.Continue, null, [Negotiate(negTokenResp)]);

    private HttpCredentialCheck RefuseAndForget()
    {
        handshake.Forget();

        return Refused;
    }
}
