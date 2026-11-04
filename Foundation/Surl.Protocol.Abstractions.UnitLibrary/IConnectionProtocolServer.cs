namespace Surl.Protocol.Abstractions;

/// <summary>
/// A protocol server that answers stream-oriented connections (ADR-0004, section 4).
/// </summary>
public interface IConnectionProtocolServer : IProtocolServer
{
    /// <summary>
    /// Answers one exchange on <paramref name="connection"/>, from its first byte to its last
    /// write. The serving engine owns the connection's lifetime and disposes it afterwards.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    Task ServeAsync(IConnection connection, ExchangeContext context);
}
