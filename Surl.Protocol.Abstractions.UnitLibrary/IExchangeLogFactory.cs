using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Hands the serving engine one <see cref="IExchangeLog"/> per exchange (ADR-0004, section 5).
/// </summary>
public interface IExchangeLogFactory
{
    /// <summary>
    /// Creates the log for one exchange.
    /// </summary>
    /// <param name="exchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
    /// <param name="remoteEndPoint">The client's endpoint.</param>
    /// <returns>The exchange's log.</returns>
    IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint);
}
