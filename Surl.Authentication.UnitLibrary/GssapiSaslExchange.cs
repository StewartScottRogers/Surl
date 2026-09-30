using System.Text;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>GSSAPI</c>, RFC 4752 with Kerberos V5 (ADR-0049, section 4; ADR-0057, decisions 9 and
/// 10): one empty challenge when no initial response was sent, then the client's
/// <c>InitialContextToken</c> checked by the <c>--keytab</c> acceptor for the service the scheme
/// names (<c>smtp</c>, <c>imap</c> or <c>pop</c>); the AP-REP token when the client asked for
/// mutual authentication, which the client must answer empty; then the wrapped security-layer offer
/// <c>01 00 00 00</c>, no layer and no maximum size. The client's wrapped answer must choose no
/// layer, and the authorization identity after it must be empty or the ticket's client principal
/// in display form, such as <c>user@EXAMPLE.COM</c>, which is also the account name the login
/// needs. A refused ticket names no user; every later refusal names the principal. Under
/// <c>--allow-anonymous</c> every step still runs, since the tokens need the ticket's keys, and only
/// the account match is skipped.
/// </summary>
internal sealed class GssapiSaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    private const byte NoSecurityLayer = 0x01;

    private const int SecurityLayerLength = 4;

    private static readonly byte[] SecurityLayerOffer = [NoSecurityLayer, 0x00, 0x00, 0x00];

    private KerberosSecurityContext? securityContext;

    private bool isAwaitingEmptyAnswer;

    /// <inheritdoc/>
    protected override ValueTask<MailLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (response is not { } token)
        {
            return ValueTask.FromResult(Challenge(ReadOnlyMemory<byte>.Empty));
        }

        if (securityContext is not { } accepted)
        {
            return AcceptTicketAsync(token.Span, cancellationToken);
        }

        return isAwaitingEmptyAnswer
            ? AnswerApReplyAsync(accepted, token.Span, cancellationToken)
            : CheckSecurityLayerChoiceAsync(accepted, token.Span, cancellationToken);
    }

    // The Kerberos service each mail scheme answers (ADR-0057, decision 2); only the mail servers
    // start a SASL exchange, so what is not SMTP or IMAP is POP3.
    private static string ServiceOf(string scheme) => scheme.ToLowerInvariant() switch
    {
        "smtp" or "smtps" => "smtp",
        "imap" or "imaps" => "imap",
        _ => "pop",
    };

    private ValueTask<MailLoginStep> AcceptTicketAsync(ReadOnlySpan<byte> token, CancellationToken cancellationToken)
    {
        var result = Context.KerberosAcceptor.Accept(token, ServiceOf(Context.Scheme));
        if (result.Context is not { } accepted)
        {
            return RefuseAsync(null, cancellationToken);
        }

        securityContext = accepted;
        isAwaitingEmptyAnswer = accepted.IsMutualAuthenticationRequested;

        return ValueTask.FromResult(Challenge(
            isAwaitingEmptyAnswer ? accepted.CreateApRepToken() : accepted.Wrap(SecurityLayerOffer)));
    }

    // RFC 4752 section 3.1: the client answers the AP-REP with an empty response.
    private ValueTask<MailLoginStep> AnswerApReplyAsync(
        KerberosSecurityContext accepted, ReadOnlySpan<byte> answer, CancellationToken cancellationToken)
    {
        if (!answer.IsEmpty)
        {
            return RefuseAsync(accepted.ClientPrincipal.ToString(), cancellationToken);
        }

        isAwaitingEmptyAnswer = false;

        return ValueTask.FromResult(Challenge(accepted.Wrap(SecurityLayerOffer)));
    }

    private ValueTask<MailLoginStep> CheckSecurityLayerChoiceAsync(
        KerberosSecurityContext accepted, ReadOnlySpan<byte> token, CancellationToken cancellationToken)
    {
        var name = accepted.ClientPrincipal.ToString();
        if (!TryReadNoLayerChoice(accepted, token, out var authzid))
        {
            return RefuseAsync(name, cancellationToken);
        }

        if (Context.IsUnchecked)
        {
            return ValueTask.FromResult(AcceptedUnchecked);
        }

        var isAuthorized = authzid.Length == 0 || authzid.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(name));

        return Context.Accounts.HasNamedAccount(name) && isAuthorized
            ? ValueTask.FromResult(Accept(name, name))
            : RefuseAsync(name, cancellationToken);
    }

    // The client's answer unwraps to at least 4 bytes whose first is the no-layer bit alone; the
    // next 3 are ignored and the rest is the authorization identity (ADR-0057, decision 9).
    private static bool TryReadNoLayerChoice(KerberosSecurityContext accepted, ReadOnlySpan<byte> token, out byte[] authzid)
    {
        authzid = [];
        if (!accepted.TryUnwrap(token, out var choice) || choice.Length < SecurityLayerLength || choice[0] != NoSecurityLayer)
        {
            return false;
        }

        authzid = choice[SecurityLayerLength..];
        return true;
    }
}
