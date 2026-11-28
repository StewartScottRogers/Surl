namespace Surl.Protocol.Abstractions;

/// <summary>
/// A datagram flow under a <see cref="FaultKind.Drop"/> rule with <see cref="FaultRule.AfterBytes"/> and
/// <see cref="FaultClose.Graceful"/>, handed only to an <see cref="IGracefulCutDatagramProtocolServer"/>
/// on a listen URL it closes gracefully (ADR-0188, amendments 5 and 6):
/// <see cref="IDatagramFlow.SendAsync"/> sends whole datagrams while the bytes sent stay within the rule's
/// count, and the first that would pass it cuts the flow, so that datagram and every later one sent through
/// <see cref="IDatagramFlow.SendAsync"/> is discarded. The server then closes its connection with no error.
/// Every datagram that closes the connection, then or otherwise, goes through
/// <see cref="SendClosingDatagramAsync"/>.
/// </summary>
public interface IGracefullyCutDatagramFlow : IDatagramFlow
{
    /// <summary>
    /// Whether a send has reached the count, so <see cref="IDatagramFlow.SendAsync"/> discards what it is given.
    /// </summary>
    bool IsCut { get; }

    /// <summary>
    /// Sends one datagram that closes the connection, such as QUIC's <c>CONNECTION_CLOSE</c> and each repeat
    /// of it, whether the flow is cut or the connection closed of itself. The datagram is not counted, never
    /// cuts the flow and is never discarded. Call it only with datagrams that close the connection.
    /// </summary>
    /// <param name="datagram">The whole datagram.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram has been sent.</returns>
    ValueTask SendClosingDatagramAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken);
}
