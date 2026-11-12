using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One enhanced authentication or re-authentication (section 4.12, ADR-0093 decision 2): the
/// Authentication Data of the <c>CONNECT</c> or <c>AUTH</c> <c>0x19</c> that starts it, and of each
/// <c>AUTH</c> <c>0x18</c> that continues it, goes to the policy's <see cref="ISaslExchange"/>, and
/// each step becomes the broker's answer. The methods are the SASL mechanisms
/// <c>SCRAM-SHA-256</c> and <c>SCRAM-SHA-1</c>, named as RFC 7677 and RFC 5802 name them.
/// </summary>
/// <remarks>
/// SCRAM's server-final message is the data that comes with success (RFC 4422, section 5), which
/// MQTT carries in the <c>CONNACK</c> or <c>AUTH</c> <c>0x00</c> that ends the authentication: the
/// client sends nothing after it. The policy issues it as a second challenge, as the mail servers
/// need, so this class answers that challenge itself with the empty response a mail client would
/// send, and returns the server-final with the outcome. Data that is absent is taken as empty: a
/// SCRAM client always sends its first message with the method.
/// </remarks>
internal sealed class Mqtt5EnhancedAuthentication
{
    private const int ServerFinalChallenge = 2;

    private static readonly string[] Methods = ["SCRAM-SHA-256", "SCRAM-SHA-1"];

    private readonly ISaslAuthenticationPolicy policy;
    private readonly ExchangeContext context;
    private readonly TlsSession? tlsSession;
    private ISaslExchange? exchange;
    private int challengesIssued;

    /// <summary>
    /// Prepares an authentication with <paramref name="method"/>.
    /// </summary>
    /// <param name="policy">The SASL policy that runs the method.</param>
    /// <param name="method">The Authentication Method; one <see cref="IsOffered"/> accepts.</param>
    /// <param name="context">The exchange's context: its scheme, log and cancellation token.</param>
    /// <param name="tlsSession">The connection's TLS session; <see langword="null"/> without TLS.</param>
    public Mqtt5EnhancedAuthentication(ISaslAuthenticationPolicy policy, string method, ExchangeContext context, TlsSession? tlsSession)
    {
        this.policy = policy;
        this.context = context;
        this.tlsSession = tlsSession;
        Method = method;
    }

    /// <summary>The Authentication Method.</summary>
    public string Method { get; }

    /// <summary>
    /// Whether <paramref name="method"/> is served: one of the two SCRAM methods, and offered by
    /// <paramref name="policy"/> for the connection. Any other is <c>0x8C</c> Bad authentication method.
    /// </summary>
    /// <param name="policy">The SASL policy; <see langword="null"/> when the broker has none, which serves no method.</param>
    /// <param name="method">The Authentication Method the client named.</param>
    /// <param name="scheme">The connection's scheme.</param>
    /// <param name="tlsSession">The connection's TLS session.</param>
    /// <returns><see langword="true"/> when it is served.</returns>
    public static bool IsOffered(ISaslAuthenticationPolicy? policy, string method, string scheme, TlsSession? tlsSession) =>
        policy is not null
        && Methods.Contains(method, StringComparer.Ordinal)
        && policy.GetSaslMechanisms(new SaslOfferRequest(scheme, tlsSession)).Contains(method, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hands the client's Authentication Data to the method: the first call starts the exchange,
    /// each later one continues it.
    /// </summary>
    /// <param name="data">The Authentication Data; <see langword="null"/> when the packet had none.</param>
    /// <returns>The step: a challenge to send, or how the authentication ended.</returns>
    public async ValueTask<Mqtt5AuthenticationStep> StepAsync(byte[]? data)
    {
        var response = data ?? [];
        SaslLoginStep step;
        if (exchange is null)
        {
            exchange = policy.StartSaslExchange(new SaslExchangeStart(context.Scheme, Method, response, tlsSession, Exchange: LoginExchange.Of(context)));
            step = await exchange.BeginAsync(context.CancellationToken);
        }
        else
        {
            step = await exchange.ContinueAsync(response, context.CancellationToken);
        }

        Note(step);
        if (step.Outcome != SaslLoginOutcome.Challenge)
        {
            return End(step, step.AdditionalSuccessData);
        }

        if (++challengesIssued < ServerFinalChallenge)
        {
            return new(Mqtt5AuthenticationOutcome.Continue, step.Challenge.ToArray(), null);
        }

        var final = await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, context.CancellationToken);
        Note(final);
        return End(final, step.Challenge);
    }

    private static Mqtt5AuthenticationStep End(SaslLoginStep step, ReadOnlyMemory<byte> data) => step.Outcome switch
    {
        SaslLoginOutcome.Accepted => new(Mqtt5AuthenticationOutcome.Accepted, data.ToArray(), step.AccountName),
        SaslLoginOutcome.AcceptedUnchecked => new(Mqtt5AuthenticationOutcome.Accepted, data.ToArray(), null),
        _ => new(Mqtt5AuthenticationOutcome.Refused, [], null),
    };

    private void Note(SaslLoginStep step)
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
}
