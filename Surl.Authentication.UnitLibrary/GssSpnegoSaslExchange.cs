using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// LDAP's <c>GSS-SPNEGO</c> bind (ADR-0072, decision 4, and its Amendment 1). A first token that
/// is a SPNEGO <c>NegTokenInit</c> selecting Kerberos - its first mechanism surl supports is
/// Kerberos under either OID, as <c>WinLDAP</c> sends for a host name (measured) - is answered in
/// one leg when <c>--keytab</c> gave a Kerberos acceptor: Kerberos must be the client's first
/// mechanism and its optimistic token the AP-REQ, checked for the service <c>ldap</c>; a client
/// <c>mechListMIC</c> is checked and answered with surl's. The login is the ticket's client
/// principal in display form, such as <c>user@EXAMPLE.COM</c>, which must name an account unless
/// <c>--allow-anonymous</c>; success carries an <c>accept-completed</c> <c>negTokenResp</c>
/// (<c>supportedMech</c> echoing the client's OID, the AP-REP when the client asked for mutual
/// authentication) as <see cref="SaslLoginStep.AdditionalSuccessData"/> and, when the client asked
/// for confidentiality or integrity, a <see cref="KerberosSaslSecurityLayer"/>. A ticket that never
/// decrypted names no user and notes <c>Kerberos: &lt;reason&gt;</c>; every later refusal names the
/// principal. Every other bind - bare NTLM, SPNEGO naming NTLM, or Kerberos with no keytab - is
/// <see cref="NtlmSaslExchange"/>'s, by ADR-0040's rule.
/// </summary>
internal sealed class GssSpnegoSaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    // ADR-0072 Amendment 1: the service WinLDAP names, ldap/<host>:<port>.
    private const string LdapService = "ldap";

    private NtlmSaslExchange? ntlm;

    /// <inheritdoc/>
    protected override ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (ntlm is not null)
        {
            // A later step is only reached after a challenge, so a response was sent.
            return ntlm.ContinueAsync(response!.Value, cancellationToken);
        }

        if (response is not { } token)
        {
            return ValueTask.FromResult(Challenge(ReadOnlyMemory<byte>.Empty));
        }

        if (SelectedKerberos(token.ToArray()) is { } selected)
        {
            return AnswerKerberosAsync(selected.NegTokenInit, selected.KerberosOid, cancellationToken);
        }

        ntlm = new NtlmSaslExchange(Context with { InitialResponse = token });
        return ntlm.BeginAsync(cancellationToken);
    }

    private (SpnegoNegTokenInit NegTokenInit, string KerberosOid)? SelectedKerberos(byte[] token)
    {
        if (Context.Policy.Settings.KerberosAcceptor is null
            || !SpnegoToken.IsInitialContextToken(token)
            || SpnegoToken.ReadNegTokenInit(token) is not { } negTokenInit)
        {
            return null;
        }

        return NegotiateKerberosLogin.SelectedKerberosOid(negTokenInit) is { } kerberosOid
            ? (negTokenInit, kerberosOid)
            : null;
    }

    private ValueTask<SaslLoginStep> AnswerKerberosAsync(
        SpnegoNegTokenInit negTokenInit, string kerberosOid, CancellationToken cancellationToken)
    {
        if (negTokenInit.MechTypes[0] != kerberosOid || negTokenInit.MechToken is not { } apRequest)
        {
            return RefuseAsync(null, cancellationToken, "Kerberos: no optimistic AP-REQ");
        }

        var result = Context.KerberosAcceptor.AcceptInsideSpnego(apRequest, LdapService);
        if (result.Context is not { } accepted)
        {
            return RefuseAsync(null, cancellationToken, "Kerberos: " + result.RefusalReason);
        }

        return ConcludeKerberosAsync(accepted, negTokenInit, kerberosOid, cancellationToken);
    }

    // RFC 4178 section 5: a client MIC is checked over the mechTypes DER and answered with surl's
    // own over the same bytes; no client MIC, no reply MIC.
    private ValueTask<SaslLoginStep> ConcludeKerberosAsync(
        KerberosSecurityContext accepted, SpnegoNegTokenInit negTokenInit, string kerberosOid, CancellationToken cancellationToken)
    {
        var name = accepted.ClientPrincipal.ToString();
        var isMicRight = negTokenInit.MechListMic is not { } clientMic || accepted.VerifyMic(negTokenInit.MechTypesDer, clientMic);
        var isServed = isMicRight && (Context.IsUnchecked || Context.Accounts.HasNamedAccount(name));

        return isServed
            ? ValueTask.FromResult(Serve(accepted, negTokenInit, kerberosOid, name))
            : RefuseAsync(name, cancellationToken);
    }

    // ADR-0057 decision 10: the final token is made only for a login that is served.
    private SaslLoginStep Serve(KerberosSecurityContext accepted, SpnegoNegTokenInit negTokenInit, string kerberosOid, string name)
    {
        var accepting = Context.IsUnchecked ? AcceptedUnchecked : Accept(name, name);
        var apReply = accepted.IsMutualAuthenticationRequested ? accepted.CreateApRepToken() : null;
        var serverMic = negTokenInit.MechListMic is null ? null : accepted.GetMic(negTokenInit.MechTypesDer);

        return accepting with
        {
            AdditionalSuccessData = SpnegoToken.WriteNegTokenResp(SpnegoNegState.AcceptCompleted, kerberosOid, apReply, serverMic),
            SecurityLayer = KerberosSaslSecurityLayer.IsNegotiated(accepted) ? new KerberosSaslSecurityLayer(accepted) : null,
        };
    }
}
