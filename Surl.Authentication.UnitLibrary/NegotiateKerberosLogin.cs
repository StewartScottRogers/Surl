using Surl.Kerberos;

namespace Surl.Authentication;

/// <summary>
/// Kerberos inside HTTP Negotiate (ADR-0057 decisions 8 and 10), one leg: the client's AP-REQ,
/// bare or as the optimistic token of a SPNEGO <c>NegTokenInit</c> that selects Kerberos, checked
/// by the <c>--keytab</c> acceptor for the service <c>HTTP</c>. An accepted ticket logs in the
/// account named exactly as its client principal's display form, such as
/// <c>user@EXAMPLE.COM</c>, and is served with the final token: a <c>negTokenResp</c>
/// (<c>accept-completed</c>, <c>supportedMech</c> echoing the client's OID, the AP-REP token when
/// the client asked for mutual authentication, and surl's <c>mechListMIC</c> when the client sent
/// one), or for a bare token the bare AP-REP token when mutual authentication was asked. A ticket
/// that never decrypted names no user; every later refusal names the principal. Under
/// <c>--allow-anonymous</c> the ticket must still decrypt and only the account match is skipped.
/// </summary>
/// <param name="acceptor">The <c>--keytab</c> acceptor.</param>
/// <param name="accounts">The configured accounts.</param>
/// <param name="isUnchecked">Whether <c>--allow-anonymous</c> skips the account match.</param>
internal sealed class NegotiateKerberosLogin(KerberosAcceptor acceptor, AccountBook accounts, bool isUnchecked)
{
    // ADR-0057 decision 2: the service http and https answer.
    private const string HttpService = "HTTP";

    private static readonly HttpCredentialCheck RefusedUnnamed = new(HttpCredentialOutcome.Refused, null, []);

    /// <summary>
    /// The Kerberos OID a <c>NegTokenInit</c> selects: its first mechanism surl supports, when that
    /// is Kerberos under either OID rather than NTLM.
    /// </summary>
    /// <param name="negTokenInit">The client's <c>NegTokenInit</c>.</param>
    /// <returns>The OID as the client listed it, or <see langword="null"/> when Kerberos is not selected.</returns>
    public static string? SelectedKerberosOid(SpnegoNegTokenInit negTokenInit) =>
        negTokenInit.MechTypes.FirstOrDefault(IsSupported) is { } selected && selected != SpnegoToken.NtlmOid
            ? selected
            : null;

    /// <summary>
    /// Answers a bare Kerberos <c>InitialContextToken</c>.
    /// </summary>
    /// <param name="token">The decoded token.</param>
    /// <returns>Accepted with the bare AP-REP token when mutual authentication was asked, or refused.</returns>
    public HttpCredentialCheck AnswerBare(byte[] token) =>
        acceptor.Accept(token, HttpService).Context is { } context
            ? Decide(context, () => context.IsMutualAuthenticationRequested
                ? [NegotiateConnectionVerifier.Negotiate(context.CreateApRepToken())]
                : [])
            : RefusedUnnamed;

    /// <summary>
    /// Answers a <c>NegTokenInit</c> that selects Kerberos: its optimistic token must be the
    /// AP-REQ, so Kerberos must be its first mechanism; there is no fallback to NTLM.
    /// </summary>
    /// <param name="negTokenInit">The client's <c>NegTokenInit</c>.</param>
    /// <param name="kerberosOid">The OID <see cref="SelectedKerberosOid"/> gave.</param>
    /// <returns>Accepted with the final <c>negTokenResp</c>, or refused.</returns>
    public HttpCredentialCheck AnswerSpnego(SpnegoNegTokenInit negTokenInit, string kerberosOid)
    {
        if (negTokenInit.MechTypes[0] != kerberosOid
            || negTokenInit.MechToken is not { } apRequest
            || acceptor.AcceptInsideSpnego(apRequest, HttpService).Context is not { } context)
        {
            return RefusedUnnamed;
        }

        // RFC 4178 section 5: a client MIC is checked over the mechTypes DER (key usage 25) and
        // answered with surl's own over the same bytes (key usage 23); no client MIC, no reply MIC.
        var clientMic = negTokenInit.MechListMic;
        if (clientMic is not null && !context.VerifyMic(negTokenInit.MechTypesDer, clientMic))
        {
            return RefusedNaming(context);
        }

        return Decide(context, () =>
        [
            NegotiateConnectionVerifier.Negotiate(SpnegoToken.WriteNegTokenResp(
                SpnegoNegState.AcceptCompleted,
                kerberosOid,
                context.IsMutualAuthenticationRequested ? context.CreateApRepToken() : null,
                clientMic is null ? null : context.GetMic(negTokenInit.MechTypesDer))),
        ]);
    }

    private static bool IsSupported(string oid) =>
        oid is SpnegoToken.KerberosOid or SpnegoToken.MicrosoftKerberosOid or SpnegoToken.NtlmOid;

    private static HttpCredentialCheck RefusedNaming(KerberosSecurityContext context) =>
        new(HttpCredentialOutcome.Refused, null, [], context.ClientPrincipal.ToString());

    // ADR-0057 decision 10: the final token is made only for a login that is served.
    private HttpCredentialCheck Decide(KerberosSecurityContext context, Func<IReadOnlyList<string>> finalTokens)
    {
        var name = context.ClientPrincipal.ToString();
        if (isUnchecked)
        {
            return new HttpCredentialCheck(HttpCredentialOutcome.AcceptedUnchecked, null, finalTokens(), name);
        }

        return accounts.HasNamedAccount(name)
            ? new HttpCredentialCheck(HttpCredentialOutcome.Accepted, name, finalTokens(), name)
            : RefusedNaming(context);
    }
}
