using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;
using Surl.Tls13Stream;
using Surl.TlsSrp;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IConnection"/> over a <see cref="Stream"/> and the transport control beside
/// it (ADR-0004, section 2). It holds the connection's state rules - no write after
/// half-close, no call after abort or dispose, an empty read buffer refused - and turns
/// transport failures into <see cref="IOException"/>; the transport is the caller's, so
/// this runs without a socket in the fast tests. With a <see cref="ServerTlsHandshake"/> it
/// can be upgraded to TLS (ADR-0010, section 1), after which it reads and writes through the
/// secured stream the handshake returns - a <see cref="TlsSrpStream"/>, a
/// <see cref="Tls13ServerStream"/> or an <see cref="SslStream"/> - and so still carries plaintext.
/// </summary>
/// <remarks>Not safe for concurrent reads, or concurrent writes; one read and one write may run at once.</remarks>
internal sealed class StreamConnection : IConnection
{
    /// <summary>
    /// The longest <see cref="DisposeAsync"/> goes on reading and discarding what the client
    /// sends after FIN before it closes the transport anyway (ADR-0021).
    /// </summary>
    public static readonly TimeSpan LingeringCloseTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The longest <see cref="DisposeAsync"/> waits for the graceful close - the TLS
    /// close_notify, the flush and FIN - before it disposes the transport anyway; the same
    /// bound ADR-0006 section 5 gives a refusal's write.
    /// </summary>
    public static readonly TimeSpan GracefulCloseTime = TimeSpan.FromSeconds(1);

    private const int DiscardBufferBytes = 4096;

    private readonly Stream transportStream;
    private readonly IConnectionTransportControl transportControl;
    private readonly ServerTlsHandshake? tlsHandshake;
    private readonly TimeProvider timeProvider;
    private Stream stream;
    private Stream? securedStream;
    private bool writesCompleted;
    private bool aborted;
    private bool disposed;
    private bool upgradeUnfinished;
    private bool readPending;
    private bool writePending;
    private Task<int>? inflightRead;
    private byte[] inflightBuffer = [];
    private byte[] readAhead = [];
    private int readAheadStart;
    private int readAheadCount;
    private bool peerEnded;
    private ITlsLoginJudge? tlsLoginJudge;

    /// <summary>
    /// Creates a connection over <paramref name="stream"/>, which it owns and disposes.
    /// </summary>
    /// <param name="stream">Carries the bytes both ways.</param>
    /// <param name="localEndPoint">The local endpoint the connection was accepted on.</param>
    /// <param name="remoteEndPoint">The client's endpoint.</param>
    /// <param name="transportControl">Half-closes and resets the transport under <paramref name="stream"/>.</param>
    /// <param name="tlsHandshake">The TLS handshake an upgrade runs, or <see langword="null"/> for a connection that cannot be secured.</param>
    /// <param name="timeProvider">Times the graceful and lingering close; <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public StreamConnection(
        Stream stream,
        EndPoint localEndPoint,
        EndPoint remoteEndPoint,
        IConnectionTransportControl transportControl,
        ServerTlsHandshake? tlsHandshake = null,
        TimeProvider? timeProvider = null)
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
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
            return await ReadReadAheadOrTransportAsync(buffer, cancellationToken);
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

    /// <summary>
    /// Waits up to <paramref name="wait"/>, on the injected <see cref="TimeProvider"/>, for the
    /// client's next byte and keeps it for the next read or TLS handshake (ADR-0113, decision 6).
    /// The one-byte read it starts is never cancelled, so a byte that arrives after the wait is
    /// kept too: the next peek or read waits for that same read.
    /// </summary>
    /// <param name="wait">The longest to wait; must not be negative.</param>
    /// <param name="cancellationToken">Cuts the wait off; the read goes on.</param>
    /// <returns>The next byte, or <see langword="null"/> when none arrived in time or the peer half-closed.</returns>
    public async ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);
        ArgumentOutOfRangeException.ThrowIfLessThan(wait, TimeSpan.Zero);

        if (readAheadCount == 0 && !peerEnded)
        {
            inflightRead ??= StartReadAhead(1);

            try
            {
                await inflightRead.WaitAsync(wait, timeProvider, cancellationToken);
            }
            catch (TimeoutException)
            {
                return null;
            }

            await TakeFinishedReadAsync();
        }

        return readAheadCount == 0 ? null : readAhead[readAheadStart];
    }

    /// <summary>
    /// Reads ahead from the transport, into a buffer of at most <paramref name="maxBufferedBytes"/>,
    /// until the peer half-closes, resets, the buffer is full or the token ends the watch
    /// (ADR-0141, amendment 2, decision 2). Every byte read ahead is returned first, in order, by
    /// the next <see cref="ReadAsync"/>, or handed to the TLS handshake ahead of the transport.
    /// The transport read in flight is never cancelled, so ending the watch loses nothing.
    /// </summary>
    /// <param name="maxBufferedBytes">The most the watch keeps read ahead; must be positive.</param>
    /// <param name="cancellationToken">Ends the watch; the read goes on.</param>
    /// <returns>Why the watch ended.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBufferedBytes"/> is not positive.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> ended the watch.</exception>
    public async ValueTask<PeerWatchOutcome> WatchForPeerCloseAsync(int maxBufferedBytes, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBufferedBytes);

        readPending = true;

        try
        {
            return await WatchReadingAheadAsync(maxBufferedBytes, cancellationToken);
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

        await SendCloseNotifyAsync(cancellationToken);
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
            var handshakeTransport = await TakeReadAheadAheadOfTransportAsync();
            (securedStream, var session) = await tlsHandshake!.AuthenticateAsync(handshakeTransport, cancellationToken, tlsLoginJudge);
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

    /// <summary>
    /// Keeps the judge a later TLS-SRP handshake on this connection asks and tells (ADR-0165,
    /// decision 2). A handshake that is not TLS-SRP never calls it.
    /// </summary>
    /// <param name="judge">The judge to keep.</param>
    public void UseTlsLoginJudge(ITlsLoginJudge judge)
    {
        ArgumentNullException.ThrowIfNull(judge);

        tlsLoginJudge = judge;
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
    /// finish, giving up on them after <see cref="GracefulCloseTime"/> or on any failure, then
    /// lingers - reads and discards what the client still sends, below any TLS,
    /// until it half-closes or <see cref="LingeringCloseTime"/> passes - so bytes the server
    /// never read do not turn the close into a reset that destroys the reply (ADR-0021).
    /// Then it disposes the TLS stream, if any, and the transport stream. A peer already
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

            if (writesCompleted)
            {
                await DiscardUnreadBytesAsync();
            }
        }

        disposed = true;

        if (securedStream is not null)
        {
            await securedStream.DisposeAsync();
        }

        await transportStream.DisposeAsync();
    }

    // The TLS close_notify, from whichever secured stream the handshake returned (ADR-0122
    // decision 5, ADR-0157); an unsecured connection has none to send.
    private Task SendCloseNotifyAsync(CancellationToken cancellationToken) => securedStream switch
    {
        SslStream sslStream => sslStream.ShutdownAsync(),
        TlsSrpStream tlsSrpStream => tlsSrpStream.ShutdownAsync(cancellationToken),
        Tls13ServerStream tls13Stream => tls13Stream.ShutdownAsync(cancellationToken),
        _ => Task.CompletedTask,
    };

    private static IOException AbortedException(Exception? innerException) =>
        new("The connection was aborted.", innerException);

    private async ValueTask<PeerWatchOutcome> WatchReadingAheadAsync(int maxBufferedBytes, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (inflightRead is null)
            {
                if (peerEnded)
                {
                    return PeerWatchOutcome.PeerClosed;
                }

                if (readAheadCount >= maxBufferedBytes)
                {
                    return PeerWatchOutcome.BufferFull;
                }

                inflightRead = StartReadAhead(maxBufferedBytes - readAheadCount);
            }

            try
            {
                await inflightRead.WaitAsync(cancellationToken);
            }
            catch (IOException)
            {
                // The read stays, faulted: the server's next read sees the reset itself.
                return PeerWatchOutcome.PeerReset;
            }

            await TakeFinishedReadAsync();
        }
    }

    // The read-ahead bytes first, in order; else what the transport has, after waiting for a read already in flight.
    private async ValueTask<int> ReadReadAheadOrTransportAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (readAheadCount == 0 && inflightRead is not null)
        {
            await inflightRead.WaitAsync(cancellationToken);
            await TakeFinishedReadAsync();
        }

        if (readAheadCount > 0)
        {
            var served = Math.Min(buffer.Length, readAheadCount);

            readAhead.AsSpan(readAheadStart, served).CopyTo(buffer.Span);
            readAheadStart += served;
            readAheadCount -= served;

            return served;
        }

        return peerEnded ? 0 : await stream.ReadAsync(buffer, cancellationToken);
    }

    // Starts one read on the current stream that is never cancelled, so a byte arriving after a
    // peek's wait or a watch's end is still kept; it runs until bytes, the peer's half-close, a
    // reset, Abort or disposal ends it.
    private Task<int> StartReadAhead(int size)
    {
        inflightBuffer = new byte[size];

        return ReadAheadOnceAsync(inflightBuffer);
    }

    private async Task<int> ReadAheadOnceAsync(byte[] target)
    {
        try
        {
            return await stream.ReadAsync(target, CancellationToken.None);
        }
        catch (ObjectDisposedException exception) when (aborted)
        {
            throw AbortedException(exception);
        }
    }

    // Appends a finished read's bytes behind the ones already read ahead. A faulted read stays
    // in place, so every later call throws its exception again.
    private async ValueTask TakeFinishedReadAsync()
    {
        var count = await inflightRead!;

        inflightRead = null;

        if (count == 0)
        {
            peerEnded = true;

            return;
        }

        // Read-ahead is rare and small (a watch's bound is 64 KiB), so a new array per read keeps this simple.
        var grown = new byte[readAheadCount + count];

        readAhead.AsSpan(readAheadStart, readAheadCount).CopyTo(grown);
        inflightBuffer.AsSpan(0, count).CopyTo(grown.AsSpan(readAheadCount));
        readAhead = grown;
        readAheadStart = 0;
        readAheadCount = grown.Length;
    }

    // Only called once ThrowIfCannotUpgrade has made sure any read in flight has finished.
    private async Task<Stream> TakeReadAheadAheadOfTransportAsync()
    {
        if (inflightRead is not null)
        {
            await TakeFinishedReadAsync();
        }

        if (readAheadCount == 0)
        {
            return transportStream;
        }

        var handedOver = readAhead.AsSpan(readAheadStart, readAheadCount).ToArray();

        // The handshake reads them now, ahead of the transport; they are no longer the connection's to return.
        readAhead = [];
        readAheadStart = 0;
        readAheadCount = 0;

        return new ReadAheadPrefixStream(handedOver, transportStream);
    }

    private async Task TryCompleteWritesAsync()
    {
        using var closing = new CancellationTokenSource(GracefulCloseTime, timeProvider);

        try
        {
            // SslStream.ShutdownAsync takes no token, so the wait itself is cut off too.
            await CompleteWritesAsync(closing.Token).AsTask().WaitAsync(closing.Token);
        }
        catch (Exception)
        {
            // The peer reset the connection or stopped reading, the handshake did not finish,
            // or a cancelled write left the TLS stream unable to shut down; there is nothing
            // left to close gracefully, and the transport is disposed regardless.
        }
    }

    private async Task DiscardUnreadBytesAsync()
    {
        using var lingering = new CancellationTokenSource(LingeringCloseTime, timeProvider);
        var discarded = new byte[DiscardBufferBytes];

        try
        {
            // A read in flight may still be waiting on the transport; two reads must not race.
            if (peerEnded || (inflightRead is not null && await inflightRead.WaitAsync(lingering.Token) == 0))
            {
                return;
            }

            while (await transportStream.ReadAsync(discarded, lingering.Token) > 0)
            {
                // Discarded: the exchange is over, and only the client's half-close is awaited.
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            // The peer reset the connection, or the lingering time ran out; close it as it is.
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

        if (IsReadOrWritePending())
        {
            throw new InvalidOperationException("A read or write is pending; the connection cannot be secured now.");
        }
    }

    // A read ahead still waiting on the transport counts as a pending read.
    private bool IsReadOrWritePending() => readPending || writePending || inflightRead is { IsCompleted: false };

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
