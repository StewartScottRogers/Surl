using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// One HTTP connection's Negotiate handshake (RFC 4559, ADR-0040), carrying NTLM: the
/// connection's <see cref="NtlmHandshake"/> answers the NTLM messages, which arrive either bare
/// (<c>NTLMSSP</c>, as Windows' Negotiate package sends when it falls back to NTLM) and are
/// answered bare, or inside SPNEGO (RFC 4178) and are answered inside a <c>negTokenResp</c>. A
/// <c>NegTokenInit</c> that does not offer NTLM, a Kerberos token and a malformed token are
/// refused (ADR-0032 decision 11), never thrown.
/// </summary>
internal sealed class NegotiateConnectionVerifier(AccountBook accounts, INtlmServerChallengeSource serverChallenges)
    : IHttpCredentialVerifier
{
    private static readonly HttpCredentialCheck Refused = new(HttpCredentialOutcome.Refused, null, []);

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

        return ValueTask.FromResult(token switch
        {
            _ when SpnegoToken.IsInitialContextToken(token) => AnswerNegTokenInit(token),
            _ when SpnegoToken.IsNegTokenResp(token) && wasSpnegoStarted => AnswerNegTokenResp(token),
            _ when SpnegoToken.IsNegTokenResp(token) => RefuseAndForget(),
            _ => AnswerBareNtlm(token),
        });
    }

    private static string Negotiate(byte[] token) => $"Negotiate {Convert.ToBase64String(token)}";

    private HttpCredentialCheck AnswerBareNtlm(byte[] token)
    {
        var step = handshake.Answer(token);
        IReadOnlyList<string> values = step.ChallengeMessage is { } challengeMessage ? [Negotiate(challengeMessage)] : [];

        return new HttpCredentialCheck(step.Outcome, step.AccountName, values, step.UserAsSent);
    }

    // RFC 4178 section 3.2: the server picks NTLM from the client's list. When NTLM is the
    // client's first choice its optimistic token is NTLM's first message and is answered now;
    // otherwise that token belongs to another mechanism, so the reply only names NTLM and the
    // client starts it in its next token.
    private HttpCredentialCheck AnswerNegTokenInit(byte[] token)
    {
        var negTokenInit = SpnegoToken.ReadNegTokenInit(token);
        if (negTokenInit is null || !negTokenInit.MechTypes.Contains(SpnegoToken.NtlmOid))
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
        SpnegoToken.ReadNegTokenResp(token) is { } responseToken
            ? AnswerSpnegoNtlm(responseToken, null)
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
