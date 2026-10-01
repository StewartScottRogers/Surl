using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Decides SASL and Sicily binds (ADR-0072 decision 4) through the SASL contract: the server
/// frames each step and the <see cref="ISaslAuthenticationPolicy"/> decides it. A SASL bind is
/// one exchange of <see cref="ISaslExchange"/> steps, each challenge answered
/// <c>saslBindInProgress</c> (14) with it as <c>serverSaslCreds</c>; Sicily's <c>[10]</c> and
/// <c>[11]</c> are the <c>NTLM</c> exchange, its challenge answered <c>success</c> with it as the
/// matched DN. It holds the exchange in progress between binds.
/// </summary>
/// <param name="saslAuthenticationPolicy">Offers the mechanisms and runs each exchange.</param>
/// <param name="connection">The connection the binds come on; its TLS state goes to the policy.</param>
/// <param name="context">The exchange: its scheme, log and cancellation.</param>
internal sealed class LdapSaslBindJudge(ISaslAuthenticationPolicy saslAuthenticationPolicy, IConnection connection, ExchangeContext context)
{
    /// <summary>The mechanism Sicily's binds run as, and the package its discovery names.</summary>
    public const string SicilyMechanism = "NTLM";

    private ISaslExchange? exchangeInProgress;
    private string? mechanismInProgress;
    private bool isSicilyInProgress;

    /// <summary>
    /// Whether a SASL or Sicily bind is waiting for the client's next step.
    /// </summary>
    public bool IsBindInProgress => exchangeInProgress is not null;

    /// <summary>
    /// Drops the exchange in progress, as any bind that does not continue it does (RFC 4513 section 5.2.1.2).
    /// </summary>
    public void Abandon() => exchangeInProgress = null;

    /// <summary>
    /// Decides a SASL bind: the next step of the exchange in progress when it names the same
    /// mechanism, otherwise the first step of a new one.
    /// </summary>
    /// <param name="sasl">The bind's mechanism and credentials.</param>
    /// <returns>The answer.</returns>
    public ValueTask<LdapBindAnswer> JudgeAsync(LdapSaslAuthentication sasl)
    {
        if (exchangeInProgress is { } exchange && !isSicilyInProgress
            && string.Equals(mechanismInProgress, sasl.Mechanism, StringComparison.OrdinalIgnoreCase))
        {
            return ContinueAsync(exchange, sasl.Credentials ?? [], isSicily: false);
        }

        return StartAsync(sasl.Mechanism, sasl.Credentials, isSicily: false);
    }

    /// <summary>
    /// Decides a Sicily bind: package discovery names <c>NTLM</c> when the policy offers it,
    /// negotiate starts the <c>NTLM</c> exchange, and response continues it.
    /// </summary>
    /// <param name="sicily">The bind's choice and token.</param>
    /// <returns>The answer.</returns>
    public ValueTask<LdapBindAnswer> JudgeAsync(LdapSicilyAuthentication sicily) => sicily.Choice switch
    {
        LdapSicilyChoice.PackageDiscovery => ValueTask.FromResult(AnswerPackageDiscovery()),
        LdapSicilyChoice.Negotiate => StartAsync(SicilyMechanism, sicily.Token, isSicily: true),
        _ => exchangeInProgress is { } exchange && isSicilyInProgress
            ? ContinueAsync(exchange, sicily.Token, isSicily: true)
            : ValueTask.FromResult(Refuse(LdapResultCode.ProtocolError, "sicilyResponse without sicilyNegotiate")),
    };

    private LdapBindAnswer AnswerPackageDiscovery()
    {
        Abandon();
        var offered = saslAuthenticationPolicy.GetSaslMechanisms(new SaslOfferRequest(context.Scheme, connection.TlsSession));
        return offered.Contains(SicilyMechanism, StringComparer.OrdinalIgnoreCase)
            ? new LdapBindAnswer(Bound, IsBound: false, SicilyMatchedDn: "NTLM"u8.ToArray())
            : RefuseMechanism();
    }

    private async ValueTask<LdapBindAnswer> StartAsync(string mechanism, byte[]? initialResponse, bool isSicily)
    {
        Abandon();

        // A conditional would turn null into empty memory through ReadOnlyMemory's conversion
        // from byte[], so absent credentials are left null by an if.
        ReadOnlyMemory<byte>? response = null;
        if (initialResponse is not null)
        {
            response = initialResponse;
        }

        var exchange = saslAuthenticationPolicy.StartSaslExchange(
            new SaslExchangeStart(context.Scheme, mechanism, response, connection.TlsSession, CanCarrySecurityLayer: true));
        return Answer(await exchange.BeginAsync(context.CancellationToken), exchange, mechanism, isSicily);
    }

    private async ValueTask<LdapBindAnswer> ContinueAsync(ISaslExchange exchange, byte[] response, bool isSicily)
    {
        var mechanism = mechanismInProgress!;
        Abandon();
        return Answer(await exchange.ContinueAsync(response, context.CancellationToken), exchange, mechanism, isSicily);
    }

    // The login note first, then the refusal's reason, before the answer (ADR-0038).
    private void NoteStep(SaslLoginStep step)
    {
        if (step.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        if (step.RefusalNote is { } refusalNote)
        {
            context.Log.Note(refusalNote);
        }
    }

    private LdapBindAnswer Answer(SaslLoginStep step, ISaslExchange exchange, string mechanism, bool isSicily)
    {
        NoteStep(step);
        return step.Outcome switch
        {
            SaslLoginOutcome.Challenge => AnswerChallenge(step, exchange, mechanism, isSicily),
            SaslLoginOutcome.Accepted or SaslLoginOutcome.AcceptedUnchecked => new LdapBindAnswer(
                Bound,
                IsBound: true,
                ServerSaslCredentials: step.AdditionalSuccessData.IsEmpty ? null : step.AdditionalSuccessData.ToArray(),
                SecurityLayer: step.SecurityLayer,
                Mechanism: mechanism),
            SaslLoginOutcome.RefusedCredentials => LdapBindAnswer.Refused(new LdapResult(LdapResultCode.InvalidCredentials, string.Empty, string.Empty)),
            SaslLoginOutcome.RefusedPlaintext => Refuse(LdapResultCode.ConfidentialityRequired, "SASL mechanism needs TLS or --allow-plaintext-auth"),
            _ => RefuseMechanism(),
        };
    }

    // The exchange waits for the client's next step: SASL's saslBindInProgress, or Sicily's
    // success carrying the challenge as its matched DN (MS-ADTS section 5.1.1.1.3).
    private LdapBindAnswer AnswerChallenge(SaslLoginStep step, ISaslExchange exchange, string mechanism, bool isSicily)
    {
        exchangeInProgress = exchange;
        mechanismInProgress = mechanism;
        isSicilyInProgress = isSicily;
        var challenge = step.Challenge.ToArray();
        return isSicily
            ? new LdapBindAnswer(Bound, IsBound: false, SicilyMatchedDn: challenge)
            : new LdapBindAnswer(new LdapResult(LdapResultCode.SaslBindInProgress, string.Empty, string.Empty), IsBound: false, ServerSaslCredentials: challenge);
    }

    private LdapBindAnswer RefuseMechanism() => Refuse(LdapResultCode.AuthMethodNotSupported, "authentication method not accepted");

    private LdapBindAnswer Refuse(LdapResultCode code, string diagnostic)
    {
        context.Log.Note($"LDAP bind refused: {LdapLogText.NameOf(code)}: {diagnostic}");
        return LdapBindAnswer.Refused(new LdapResult(code, string.Empty, diagnostic));
    }

    private static LdapResult Bound => new(LdapResultCode.Success, string.Empty, string.Empty);
}
