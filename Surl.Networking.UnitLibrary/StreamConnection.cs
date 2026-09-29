using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IConnection"/> over a <see cref="Stream"/> and the transport control beside
/// it (ADR-0004, section 2). It holds the connection's state rules - no write after
/// half-close, no call after abort or dispose, an empty read buffer refused - and turns
/// transport failures into <see cref="IOException"/>; the transport is the caller's, so
/// this runs without a socket in the fast tests.
/// </summary>
/// <remarks>Not safe for concurrent reads, or concurrent writes; one read and one write may run at once.</remarks>
internal sealed class StreamConnection : IConnection
{
    private readonly Stream stream;
    private readonly IConnectionTransportControl transportControl;
    private bool writesCompleted;
    private bool aborted;
    private bool disposed;

    /// <summary>
    /// Creates a connection over <paramref name="stream"/>, which it owns and disposes.
    /// </summary>
    /// <param name="stream">Carries the bytes both ways.</param>
    /// <param name="localEndPoint">The local endpoint the connection was accepted on.</param>
    /// <param name="remoteEndPoint">The client's endpoint.</param>
    /// <param name="transportControl">Half-closes and resets the transport under <paramref name="stream"/>.</param>
    public StreamConnection(
        Stream stream, EndPoint localEndPoint, EndPoint remoteEndPoint, IConnectionTransportControl transportControl)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(localEndPoint);
        ArgumentNullException.ThrowIfNull(remoteEndPoint);
        ArgumentNullException.ThrowIfNull(transportControl);

        this.stream = stream;
        LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
        this.transportControl = transportControl;
    }

    /// <inheritdoc/>
    public EndPoint LocalEndPoint { get; }

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint { get; }

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (buffer.IsEmpty)
        {
            throw new ArgumentException("A read needs a buffer of at least one byte.", nameof(buffer));
        }

        try
        {
            return await stream.ReadAsync(buffer, cancellationToken);
        }
        catch (ObjectDisposedException exception) when (aborted)
        {
            throw AbortedException(exception);
        }
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (writesCompleted)
        {
            throw new InvalidOperationException("The connection was half-closed; nothing more can be written.");
        }

        try
        {
            await stream.WriteAsync(bytes, cancellationToken);
        }
        catch (ObjectDisposedException exception) when (aborted)
        {
            throw AbortedException(exception);
        }
    }

    /// <inheritdoc/>
    public async ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (writesCompleted)
        {
            return;
        }

        await stream.FlushAsync(cancellationToken);

        try
        {
            transportControl.ShutdownSend();
        }
        catch (SocketException exception)
        {
            throw new IOException(exception.Message, exception);
        }
        catch (ObjectDisposedException exception) when (aborted)
        {
            throw AbortedException(exception);
        }

        writesCompleted = true;
    }

    /// <inheritdoc/>
    public void Abort()
    {
        if (aborted || disposed)
        {
            return;
        }

        aborted = true;
        transportControl.ResetAndClose();
    }

    /// <summary>
    /// Always <see langword="null"/> until BL-012 implements the TLS handshake (ADR-0010).
    /// </summary>
    public TlsSession? TlsSession => null;

    /// <summary>
    /// Refuses the upgrade until BL-012 implements the TLS handshake over <see cref="System.Net.Security.SslStream"/> (ADR-0010).
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The TLS handshake is not implemented yet (BL-012).");

    /// <summary>
    /// Closes the connection: completes writes unless it was aborted, then disposes the
    /// stream. A peer already gone does not stop the close. Calling it twice is harmless.
    /// </summary>
    /// <returns>A task that completes once the transport is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        if (!aborted)
        {
            await TryCompleteWritesAsync();
        }

        disposed = true;
        await stream.DisposeAsync();
    }

    private static IOException AbortedException(Exception? innerException) =>
        new("The connection was aborted.", innerException);

    private async Task TryCompleteWritesAsync()
    {
        try
        {
            await CompleteWritesAsync(CancellationToken.None);
        }
        catch (IOException)
        {
            // The peer reset the connection first; there is nothing left to close gracefully.
        }
    }

    private void ThrowIfUnusable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);

        if (aborted)
        {
            throw AbortedException(null);
        }
    }
}
