namespace Surl.Protocol.Abstractions;

/// <summary>
/// A protocol server that answers datagram flows, which is TFTP (ADR-0004, section 4).
/// </summary>
public interface IDatagramProtocolServer : IProtocolServer
{
    /// <summary>
    /// Answers one exchange on <paramref name="flow"/>. The serving engine owns the flow's
    /// lifetime and disposes it afterwards.
    /// </summary>
    /// <param name="flow">The flow opened by its first datagram.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    Task ServeAsync(IDatagramFlow flow, ExchangeContext context);
}
