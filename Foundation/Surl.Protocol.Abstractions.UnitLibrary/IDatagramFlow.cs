using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Everything one remote endpoint sends to one local endpoint, starting with the datagram
/// that opened it: the transport a TFTP server answers (ADR-0004, section 3).
/// </summary>
/// <remarks>
/// <see cref="IAsyncDisposable.DisposeAsync"/> releases any port the flow bound; the listen
/// port stays with the listener.
/// </remarks>
public interface IDatagramFlow : IAsyncDisposable
{
    /// <summary>
    /// The flow's current local endpoint: the listen port until
    /// <see cref="MoveToNewLocalPortAsync"/> is called.
    /// </summary>
    EndPoint LocalEndPoint { get; }

    /// <summary>
    /// The endpoint of the client the flow belongs to.
    /// </summary>
    EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// The datagram that opened the flow on the listen port, such as a TFTP <c>RRQ</c> or <c>WRQ</c>.
    /// </summary>
    ReadOnlyMemory<byte> FirstDatagram { get; }

    /// <summary>
    /// Waits for the next datagram from <see cref="RemoteEndPoint"/> to the current
    /// <see cref="LocalEndPoint"/>. Datagrams from any other endpoint never reach the flow.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>One whole datagram.</returns>
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends one datagram to <see cref="RemoteEndPoint"/> from the current <see cref="LocalEndPoint"/>.
    /// </summary>
    /// <param name="datagram">The whole datagram.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram has been sent.</returns>
    ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken);

    /// <summary>
    /// Binds a fresh ephemeral port on the same local address and makes it the flow's
    /// <see cref="LocalEndPoint"/>, so every later send leaves from it and every later receive
    /// arrives on it: RFC 1350's new transfer identifier.
    /// </summary>
    /// <param name="cancellationToken">Cuts the bind off.</param>
    /// <returns>A task that completes once the new port is bound.</returns>
    ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken);
}
