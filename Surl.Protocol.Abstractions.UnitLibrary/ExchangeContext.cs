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
}
