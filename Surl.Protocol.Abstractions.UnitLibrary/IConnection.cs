using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// A stream-oriented connection a protocol server answers, handed to it by the listener seam
/// (ADR-0004, section 2). It always carries plaintext.
/// </summary>
/// <remarks>
/// <see cref="IAsyncDisposable.DisposeAsync"/> closes gracefully: it completes writes if the
/// server did not, then releases the transport. The serving engine disposes every connection
/// when the server's <c>ServeAsync</c> returns, so a server never has to. A reset by the peer
/// or any other transport failure surfaces as <see cref="IOException"/>.
/// </remarks>
public interface IConnection : IAsyncDisposable
{
    /// <summary>
    /// The local endpoint the connection was accepted on.
    /// </summary>
    EndPoint LocalEndPoint { get; }

    /// <summary>
    /// The endpoint of the client at the other end.
    /// </summary>
    EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Waits for at least one byte and copies what has arrived into <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">Where the bytes go; must not be empty.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>
    /// How many bytes were copied, never more than the buffer's length; 0 once the peer has
    /// half-closed and every byte before it has been read, and 0 on every later call.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Hands every byte of <paramref name="bytes"/> to the transport.
    /// </summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once every byte has been handed to the transport.</returns>
    /// <exception cref="InvalidOperationException"><see cref="CompleteWritesAsync"/> has already been called.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the write off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);

    /// <summary>
    /// Half-closes the connection: flushes and sends FIN, while the connection can still be
    /// read. Calling it twice is harmless.
    /// </summary>
    /// <param name="cancellationToken">Cuts the flush off.</param>
    /// <returns>A task that completes once FIN has been sent.</returns>
    ValueTask CompleteWritesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Drops the connection at once with a reset, discarding unsent bytes. Pending and later
    /// reads and writes throw <see cref="IOException"/>. Calling it twice is harmless.
    /// </summary>
    void Abort();
}
