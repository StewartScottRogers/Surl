using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// What a protocol server is told about one exchange. The serving engine creates a fresh one
/// for every exchange (ADR-0004, section 5).
/// </summary>
/// <param name="ExchangeId">
/// A number the engine gives each exchange, from 1, unique for the life of the process.
/// </param>
/// <param name="ListenUrl">The listen URL whose listener accepted the exchange, with its bound port set.</param>
/// <param name="LocalEndPoint">The connection's or flow's local endpoint when the exchange began.</param>
/// <param name="RemoteEndPoint">The connection's or flow's remote endpoint when the exchange began.</param>
/// <param name="Log">Where the exchange's events go.</param>
/// <param name="TimeProvider">The one clock: every timeout and every date a server writes comes from it.</param>
/// <param name="CancellationToken">
/// Cancelled when the engine gives up on the exchange. The server passes it to every
/// <see cref="IConnection"/> and <see cref="IDatagramFlow"/> call.
/// </param>
public sealed record ExchangeContext(
    long ExchangeId,
    ListenUrl ListenUrl,
    EndPoint LocalEndPoint,
    EndPoint RemoteEndPoint,
    IExchangeLog Log,
    TimeProvider TimeProvider,
    CancellationToken CancellationToken)
{
    /// <summary>
    /// Which of the server's schemes this exchange is: the listen URL's scheme.
    /// </summary>
    public string Scheme => ListenUrl.Scheme;

    /// <summary>
    /// The hardening limits the server enforces on this exchange (ADR-0006, section 6):
    /// <see cref="ExchangeLimits.Default"/> unless the engine sets others.
    /// </summary>
    public ExchangeLimits Limits { get; init; } = ExchangeLimits.Default;

    /// <summary>
    /// Opens the exchange's FTP data connections (ADR-0052, decision 9):
    /// <see cref="RefusingDataConnectionOpener.Instance"/>, which refuses every one, unless the
    /// engine sets another.
    /// </summary>
    public IDataConnectionOpener DataConnections { get; init; } = RefusingDataConnectionOpener.Instance;

    /// <summary>
    /// Where the server publishes what happened on its instance's event stream (ADR-0124, decision 1):
    /// <see cref="DiscardingSurlEventPublisher.Instance"/>, which publishes nothing, unless the engine
    /// sets another.
    /// </summary>
    public ISurlEventPublisher Events { get; init; } = DiscardingSurlEventPublisher.Instance;

    /// <summary>
    /// The fault plan the server asks whether to misbehave (ADR-0124, decision 4):
    /// <see cref="EmptyFaultPlan.Instance"/>, which finds no fault, unless the engine sets another.
    /// </summary>
    public IFaultPlan Faults { get; init; } = EmptyFaultPlan.Instance;

    /// <summary>
    /// The exchange's idle clock, which a server pauses while a command-stage fault delay holds the
    /// exchange (ADR-0124, decision 4; ADR-0141): <see cref="NoIdleClock.Instance"/>, which does
    /// nothing, unless the engine sets the exchange's own.
    /// </summary>
    public IIdleClock IdleClock { get; init; } = NoIdleClock.Instance;

    /// <summary>
    /// Where the server draws the bytes of non-secret generated identifiers for this exchange
    /// (ADR-0124, decision 6): a cryptographic source, which gives different identifiers every run,
    /// unless the engine sets a seeded one. Never draw anything secret from it; see
    /// <see cref="IIdentifierRandom"/>.
    /// </summary>
    public IIdentifierRandom Identifiers { get; init; } = RandomNumberGeneratorIdentifierRandom.Instance;

    /// <summary>
    /// Cancelled when the engine gives up on every exchange at shutdown (ADR-0059):
    /// <see cref="CancellationToken.None"/> unless the engine sets it. It cancels
    /// <see cref="CancellationToken"/> too; a server reads it only to tell shutdown from a limit.
    /// </summary>
    public CancellationToken ShutdownToken { get; init; }

    /// <summary>
    /// Whether the engine cancelled the exchange for a limit - the idle timeout or the maximum
    /// exchange duration - rather than at shutdown: <see cref="CancellationToken"/> is cancelled
    /// and <see cref="ShutdownToken"/> is not. A server that is so cancelled may write its
    /// protocol's farewell on a short deadline of its own, never on
    /// <see cref="CancellationToken"/> (ADR-0006, section 5; ADR-0059).
    /// </summary>
    public bool IsCancelledForALimit =>
        CancellationToken.IsCancellationRequested && !ShutdownToken.IsCancellationRequested;
}
