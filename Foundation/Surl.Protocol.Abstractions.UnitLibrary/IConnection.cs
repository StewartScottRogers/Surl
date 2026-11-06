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
    /// Waits up to <paramref name="wait"/> for the client's next byte and returns it without
    /// consuming it (ADR-0113, decision 6): the next <see cref="ReadAsync"/> still returns it
    /// first, and an <see cref="UpgradeToTlsAsync"/> hands it to the handshake ahead of the
    /// bytes after it, so a server can tell a TLS ClientHello (<c>0x16</c>) from plain text.
    /// </summary>
    /// <remarks>
    /// A peek that times out leaves the wait for that byte running, so a byte that arrives
    /// later is never lost: the next peek or read sees it. Calling it again without reading
    /// returns the same byte. The default implementation throws
    /// <see cref="NotSupportedException"/>, so a connection that has no peek says so.
    /// </remarks>
    /// <param name="wait">The longest to wait; <see cref="TimeSpan.Zero"/> only looks at what has already arrived.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>
    /// The next byte, or <see langword="null"/> when none arrived within <paramref name="wait"/>
    /// or the peer half-closed with nothing left unread.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="wait"/> is negative.</exception>
    /// <exception cref="NotSupportedException">This connection cannot peek.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the wait off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{GetType().Name} cannot peek at the client's next byte.");

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

    /// <summary>
    /// The negotiated TLS session once a handshake has completed, or <see langword="null"/>
    /// while the connection is plaintext (ADR-0010, section 1). It never changes back.
    /// </summary>
    TlsSession? TlsSession { get; }

    /// <summary>
    /// Runs the server side of a TLS handshake on the connection, with the listener's TLS
    /// settings (ADR-0010, section 1). From then on <see cref="ReadAsync"/> returns decrypted
    /// bytes and <see cref="WriteAsync"/> encrypts, so the connection still carries plaintext.
    /// </summary>
    /// <remarks>
    /// A server upgrading after <c>STARTTLS</c>, <c>STLS</c> or <c>AUTH TLS</c> discards every
    /// byte it has read beyond the end of that command line before calling this, and never
    /// serves them inside the TLS session.
    /// </remarks>
    /// <param name="cancellationToken">Cuts the handshake off; whoever passed the head-timeout token treats that as a timed-out handshake.</param>
    /// <returns>The negotiated session, also on <see cref="TlsSession"/> from then on.</returns>
    /// <exception cref="InvalidOperationException">
    /// The connection is already secured, <see cref="CompleteWritesAsync"/> has been called, or
    /// a read or write is pending.
    /// </exception>
    /// <exception cref="TlsHandshakeException">The handshake failed; the connection is unusable.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the handshake off; the connection is unusable.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken);
}
