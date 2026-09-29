using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IConnection"/> over a <see cref="Stream"/> and the transport control beside
/// it (ADR-0004, section 2). It holds the connection's state rules - no write after
/// half-close, no call after abort or dispose, an empty read buffer refused - and turns
/// transport failures into <see cref="IOException"/>; the transport is the caller's, so
/// this runs without a socket in the fast tests. With a <see cref="ServerTlsHandshake"/> it
/// can be upgraded to TLS (ADR-0010, section 1), after which it reads and writes through the
/// <see cref="SslStream"/> and so still carries plaintext.
/// </summary>
/// <remarks>Not safe for concurrent reads, or concurrent writes; one read and one write may run at once.</remarks>
internal sealed class StreamConnection : IConnection
{
    private readonly Stream transportStream;
    private readonly IConnectionTransportControl transportControl;
    private readonly ServerTlsHandshake? tlsHandshake;
    private Stream stream;
    private SslStream? securedStream;
    private bool writesCompleted;
    private bool aborted;
    private bool disposed;
    private bool upgradeUnfinished;
    private bool readPending;
    private bool writePending;

    /// <summary>
    /// Creates a connection over <paramref name="stream"/>, which it owns and disposes.
    /// </summary>
    /// <param name="stream">Carries the bytes both ways.</param>
    /// <param name="localEndPoint">The local endpoint the connection was accepted on.</param>
    /// <param name="remoteEndPoint">The client's endpoint.</param>
    /// <param name="transportControl">Half-closes and resets the transport under <paramref name="stream"/>.</param>
    /// <param name="tlsHandshake">The listener's TLS handshake, or <see langword="null"/> when the process has no TLS settings.</param>
    public StreamConnection(
        Stream stream,
        EndPoint localEndPoint,
        EndPoint remoteEndPoint,
        IConnectionTransportControl transportControl,
        ServerTlsHandshake? tlsHandshake = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(localEndPoint);
        ArgumentNullException.ThrowIfNull(remoteEndPoint);
        ArgumentNullException.ThrowIfNull(transportControl);

        transportStream = stream;
        this.stream = stream;
        LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
        this.transportControl = transportControl;
        this.tlsHandshake = tlsHandshake;
    }

    /// <inheritdoc/>
    public EndPoint LocalEndPoint { get; }

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint { get; }

    /// <inheritdoc/>
    public TlsSession? TlsSession { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (buffer.IsEmpty)
        {
            throw new ArgumentException("A read needs a buffer of at least one byte.", nameof(buffer));
        }

        readPending = true;

        try
        {
            return await stream.ReadAsync(buffer, cancellationToken);
        }
        catch (ObjectDisposedException exception) when (aborted)
        {
            throw AbortedException(exception);
        }
        finally
        {
            readPending = false;
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

        writePending = true;

        try
        {
            await stream.WriteAsync(bytes, cancellationToken);
        }
        catch (ObjectDisposedException exception) when (aborted)
        {
            throw AbortedException(exception);
        }
        finally
        {
            writePending = false;
        }
    }

    /// <summary>
    /// Half-closes the connection: on a secured connection it first sends the TLS
    /// close_notify alert, then flushes and sends FIN, while the connection can still be read.
    /// Calling it twice is harmless.
    /// </summary>
    /// <param name="cancellationToken">Cuts the flush off.</param>
    /// <returns>A task that completes once FIN has been sent.</returns>
    public async ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (writesCompleted)
        {
            return;
        }

        if (securedStream is not null)
        {
            await securedStream.ShutdownAsync();
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

    /// <summary>
    /// Runs the server side of a TLS handshake with the listener's <see cref="ServerTlsHandshake"/>
    /// (ADR-0010, section 1). A failed or cancelled handshake leaves the connection unusable:
    /// every later call throws <see cref="IOException"/>, and disposing it writes nothing more.
    /// </summary>
    /// <param name="cancellationToken">Cuts the handshake off.</param>
    /// <returns>The negotiated session, also on <see cref="TlsSession"/> from then on.</returns>
    /// <exception cref="InvalidOperationException">
    /// The listener has no TLS settings, the connection is already secured,
    /// <see cref="CompleteWritesAsync"/> has been called, or a read or write is pending.
    /// </exception>
    /// <exception cref="TlsHandshakeException">The handshake failed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the handshake off.</exception>
    /// <exception cref="IOException">The connection was aborted, or an earlier handshake did not finish.</exception>
    public async ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);
        ThrowIfCannotUpgrade();

        upgradeUnfinished = true;

        try
        {
            (securedStream, var session) = await tlsHandshake!.AuthenticateAsync(transportStream, cancellationToken);
            stream = securedStream;
            TlsSession = session;
            upgradeUnfinished = false;

            return session;
        }
        catch (ObjectDisposedException exception) when (aborted)
        {
            throw AbortedException(exception);
        }
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
    /// Closes the connection: completes writes unless it was aborted or its handshake did not
    /// finish, then disposes the TLS stream, if any, and the transport stream. A peer already
    /// gone does not stop the close. Calling it twice is harmless.
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

        if (securedStream is not null)
        {
            await securedStream.DisposeAsync();
        }

        await transportStream.DisposeAsync();
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
            // The peer reset the connection first, or the handshake did not finish; there is
            // nothing left to close gracefully.
        }
    }

    private void ThrowIfCannotUpgrade()
    {
        if (tlsHandshake is null)
        {
            throw new InvalidOperationException("This listener has no TLS settings, so its connections cannot be secured.");
        }

        if (TlsSession is not null)
        {
            throw new InvalidOperationException("The connection is already secured.");
        }

        if (writesCompleted)
        {
            throw new InvalidOperationException("The connection was half-closed; it cannot be secured.");
        }

        if (readPending || writePending)
        {
            throw new InvalidOperationException("A read or write is pending; the connection cannot be secured now.");
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

        if (upgradeUnfinished)
        {
            throw new IOException("The TLS handshake did not finish; the connection is unusable.");
        }
    }
}
