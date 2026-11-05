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
/// <b>In range.</b> The header is read at the version <see cref="KafkaApiVersionRange.RequestHeaderVersion"/>
/// gives, the request goes to its API's answerer, and the response is written under the response
/// header <see cref="KafkaApiVersionRange.ResponseHeaderVersion"/> gives, echoing the request's
/// <c>correlation_id</c>.
/// </para>
/// </remarks>
public sealed class KafkaBroker : IConnectionProtocolServer
{
    private readonly FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer> answerers;

    /// <summary>
    /// Creates a broker whose served APIs are answered by <paramref name="answerers"/>, one per API.
    /// </summary>
    /// <param name="answerers">The answerers; a served API none answers closes the connection.</param>
    /// <exception cref="ArgumentException">Two answerers answer the same API.</exception>
    public KafkaBroker(IEnumerable<IKafkaApiAnswerer> answerers)
    {
        ArgumentNullException.ThrowIfNull(answerers);

        this.answerers = answerers.ToFrozenDictionary(answerer => answerer.ApiKey);
    }

    /// <summary>
    /// The schemes answered: <c>kafka</c>, and <c>kafkas</c>, the same exchange over implicit
    /// TLS, whose handshake the engine performs (ADR-0105 decision 7).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["kafka", "kafkas"]);

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

        return new KafkaBrokerConnection(connection, context, answerers).ServeAsync();
    }
}
