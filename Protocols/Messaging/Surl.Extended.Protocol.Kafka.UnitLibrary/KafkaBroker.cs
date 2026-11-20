using System.Collections.Frozen;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The Kafka broker (ADR-0105): reads size-prefixed requests and their headers, and answers each
/// in turn, in the order sent, on one connection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Framing</b> (ADR-0105 decisions 1 and 7). A request is an INT32 size and that many bytes.
/// A size of zero or less, or over <see cref="ExchangeLimits.MaxMessageBytes"/>, closes the
/// connection with no bytes, the body never read. The first request must be complete within
/// <see cref="ExchangeLimits.HeadTimeout"/>, or the connection closes with no bytes.
/// </para>
/// <para>
/// <b>Outside the ranges</b> (ADR-0105 decision 1). An ApiVersions request above the served
/// range is answered with <see cref="KafkaApiVersionsFallback"/> under response header v0. Any
/// other API key, a served API at a version outside <see cref="KafkaServedApis"/>, a request too
/// short for its header, and a served API no <see cref="IKafkaApiAnswerer"/> answers yet close
/// the connection with no bytes, and the exchange log notes why.
/// </para>
/// <para>
/// <b>Before a login</b> (ADR-0105 decision 5). Unless the policy accepts an anonymous caller,
/// a request other than ApiVersions, SaslHandshake and SaslAuthenticate closes the connection
/// with no bytes until a SASL login is accepted. After a v0 SaslHandshake the login travels in
/// raw tokens, each an INT32 size and the bytes, answered the same way; a refusal there closes
/// with no bytes.
/// </para>
/// <para>
/// <b>In range.</b> The header is read at the version <see cref="KafkaApiVersionRange.RequestHeaderVersion"/>
/// gives, the request goes to its API's answerer, and the response is written under the response
/// header <see cref="KafkaApiVersionRange.ResponseHeaderVersion"/> gives, echoing the request's
/// <c>correlation_id</c>.
/// </para>
/// </remarks>
public sealed class KafkaBroker : IConnectionProtocolServer, IExchangeBytesRedactingServer
{
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer> answerers;

    /// <summary>
    /// Creates a broker whose served APIs are answered by <paramref name="answerers"/>, one per API.
    /// </summary>
    /// <param name="authenticationPolicy">
    /// Asked once a connection whether an anonymous caller is let in (<c>--allow-anonymous</c>);
    /// when not, only ApiVersions, SaslHandshake and SaslAuthenticate are served before a login.
    /// </param>
    /// <param name="answerers">The answerers; a served API none answers closes the connection.</param>
    /// <exception cref="ArgumentException">Two answerers answer the same API.</exception>
    /// <param name="state">
    /// The producer states and consumer groups the answerers share, which whole-instance reset, snapshot and
    /// restore act on (ADR-0159); when null the broker holds an empty state no answerer uses.
    /// </param>
    public KafkaBroker(IAuthenticationPolicy authenticationPolicy, IEnumerable<IKafkaApiAnswerer> answerers, KafkaBrokerState? state = null)
    {
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(answerers);

        this.authenticationPolicy = authenticationPolicy;
        State = state ?? new KafkaBrokerState(new KafkaProducerStates(), new KafkaGroupCoordinator(TimeProvider.System));

        this.answerers = answerers.ToFrozenDictionary(answerer => answerer.ApiKey);
    }

    /// <summary>The producer states and consumer groups this broker holds beside the message bus (ADR-0159).</summary>
    public KafkaBrokerState State { get; }

    /// <summary>
    /// The schemes answered: <c>kafka</c>, and <c>kafkas</c>, the same exchange over implicit
    /// TLS, whose handshake the engine performs (ADR-0105 decision 7).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["kafka", "kafkas"]);

    /// <summary>
    /// Creates the redactor that logs the SASL PLAIN password of a SaslAuthenticate request (or of a v0
    /// handshake's raw token) as <c>****</c> and every other byte as it arrived (ADR-0182, decision 6).
    /// </summary>
    /// <param name="context">The exchange the redactor is for.</param>
    /// <returns>A redactor for the connection, used by that exchange alone.</returns>
    public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context) => new KafkaSaslPlainBytesRedactor();

    /// <summary>
    /// Answers every request on <paramref name="connection"/> until the connection ends.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new KafkaBrokerConnection(connection, context, authenticationPolicy, answerers).ServeAsync();
    }
}
