using System.Net;
using System.Net.Security;
using System.Security.Authentication;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IConnection"/> that replays an inbound byte script and records every byte
/// written, so a protocol server is tested with no network (ADR-0004, section 7). It opens
/// nothing and constructs no transport type.
/// </summary>
/// <remarks>
/// Each read returns bytes from the current chunk only, never joining two chunks, so a test
/// controls how a request is split across reads. Empty chunks are skipped, because a read of
/// 0 bytes means the peer half-closed. It is meant for one test at a time and is not safe for
/// concurrent calls.
/// </remarks>
public sealed class InMemoryConnection : IConnection
{
    private readonly ReadOnlyMemory<byte>[] inboundChunks;
    private readonly bool peerHalfClosesWhenExhausted;
    private readonly TlsSession upgradeTlsSession;
    private readonly bool upgradeFails;
    private readonly MemoryStream writtenBytes = new();
    private readonly TaskCompletionSource aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int chunkIndex;
    private int chunkOffset;
    private bool readPending;
    private bool handshakeFailed;

    /// <summary>
    /// Creates a connection that replays <paramref name="inboundChunks"/>.
    /// </summary>
    /// <param name="inboundChunks">The bytes the client sends, in order, one read's worth per chunk at most.</param>
    /// <param name="localEndPoint">The local endpoint; 127.0.0.1:80 when <see langword="null"/>.</param>
    /// <param name="remoteEndPoint">The client's endpoint; 127.0.0.1:50000 when <see langword="null"/>.</param>
    /// <param name="peerHalfClosesWhenExhausted">
    /// Whether the client half-closes once the script is exhausted, so reads return 0; when
    /// <see langword="false"/>, a read after the script waits until it is cancelled or the
    /// connection is aborted.
    /// </param>
    /// <param name="initialTlsSession">
    /// The session the connection already has, standing in for implicit TLS; plaintext when
    /// <see langword="null"/>.
    /// </param>
    /// <param name="upgradeTlsSession">
    /// The session <see cref="UpgradeToTlsAsync"/> hands out; <see cref="DefaultUpgradeTlsSession"/>
    /// when <see langword="null"/>.
    /// </param>
    /// <param name="upgradeFails">Whether <see cref="UpgradeToTlsAsync"/> throws <see cref="TlsHandshakeException"/> instead.</param>
    public InMemoryConnection(
        IEnumerable<ReadOnlyMemory<byte>> inboundChunks,
        EndPoint? localEndPoint = null,
        EndPoint? remoteEndPoint = null,
        bool peerHalfClosesWhenExhausted = true,
        TlsSession? initialTlsSession = null,
        TlsSession? upgradeTlsSession = null,
        bool upgradeFails = false)
    {
        ArgumentNullException.ThrowIfNull(inboundChunks);

        this.inboundChunks = inboundChunks.Where(chunk => !chunk.IsEmpty).ToArray();
        LocalEndPoint = localEndPoint ?? new IPEndPoint(IPAddress.Loopback, 80);
        RemoteEndPoint = remoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 50000);
        this.peerHalfClosesWhenExhausted = peerHalfClosesWhenExhausted;
        TlsSession = initialTlsSession;
        this.upgradeTlsSession = upgradeTlsSession ?? DefaultUpgradeTlsSession;
        this.upgradeFails = upgradeFails;
    }

    /// <summary>
    /// The session an upgrade hands out unless the test names another: TLS 1.3 with
    /// <see cref="TlsCipherSuite.TLS_AES_128_GCM_SHA256"/>, no ALPN, no SNI, no client certificate.
    /// </summary>
    public static TlsSession DefaultUpgradeTlsSession { get; } =
        new(SslProtocols.Tls13, TlsCipherSuite.TLS_AES_128_GCM_SHA256, null, null, null);

    /// <inheritdoc/>
    public TlsSession? TlsSession { get; private set; }

    /// <summary>
    /// Whether the server called <see cref="UpgradeToTlsAsync"/> on a connection in a state
    /// that allows it, whether or not the handshake then succeeded.
    /// </summary>
    public bool UpgradeRequested { get; private set; }

    /// <inheritdoc/>
    public EndPoint LocalEndPoint { get; }

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// A copy of every byte written, in order.
    /// </summary>
    public byte[] WrittenBytes => writtenBytes.ToArray();

    /// <summary>
    /// Whether the server half-closed the connection with <see cref="CompleteWritesAsync"/>,
    /// or disposed it without aborting it first.
    /// </summary>
    public bool WritesCompleted { get; private set; }

    /// <summary>
    /// Whether <see cref="Abort"/> was called.
    /// </summary>
    public bool Aborted => aborted.Task.IsCompleted;

    /// <summary>
    /// Whether <see cref="DisposeAsync"/> was called.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (buffer.IsEmpty)
        {
            throw new ArgumentException("A read needs a buffer of at least one byte.", nameof(buffer));
        }

        if (chunkIndex < inboundChunks.Length)
        {
            return CopyFromCurrentChunk(buffer.Span);
        }

        return peerHalfClosesWhenExhausted ? 0 : await WaitForCancellationOrAbortAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        if (WritesCompleted)
        {
            throw new InvalidOperationException("The connection was half-closed; nothing more can be written.");
        }

        writtenBytes.Write(bytes.Span);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        WritesCompleted = true;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Abort() => aborted.TrySetResult();

    /// <summary>
    /// Stands in for a TLS handshake: sets <see cref="UpgradeRequested"/>, then either hands
    /// out the upgrade session or, when the connection was made to fail the upgrade, throws
    /// <see cref="TlsHandshakeException"/> and leaves the connection unusable. After an
    /// upgrade it goes on replaying the same byte script, which is plaintext.
    /// </summary>
    /// <param name="cancellationToken">Cuts the handshake off.</param>
    /// <returns>The upgrade session, also on <see cref="TlsSession"/> from then on.</returns>
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);
        ThrowIfUpgradeNotAllowed();

        UpgradeRequested = true;

        if (upgradeFails)
        {
            handshakeFailed = true;

            throw new TlsHandshakeException("The TLS handshake failed.", null);
        }

        TlsSession = upgradeTlsSession;

        return ValueTask.FromResult(upgradeTlsSession);
    }

    /// <summary>
    /// Closes the connection: completes writes unless it was aborted. Calling it twice is harmless.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        WritesCompleted |= !Aborted;
        Disposed = true;

        return ValueTask.CompletedTask;
    }

    private int CopyFromCurrentChunk(Span<byte> destination)
    {
        var remaining = inboundChunks[chunkIndex].Span[chunkOffset..];
        var count = Math.Min(remaining.Length, destination.Length);

        remaining[..count].CopyTo(destination);
        chunkOffset += count;

        if (chunkOffset == inboundChunks[chunkIndex].Length)
        {
            chunkIndex++;
            chunkOffset = 0;
        }

        return count;
    }

    private async Task<int> WaitForCancellationOrAbortAsync(CancellationToken cancellationToken)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        readPending = true;

        using (cancellationToken.Register(() => cancelled.TrySetResult()))
        {
            await Task.WhenAny(cancelled.Task, aborted.Task);
        }

        readPending = false;

        ThrowIfAborted();

        throw new OperationCanceledException(cancellationToken);
    }

    private void ThrowIfUnusable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Disposed, this);
        ThrowIfAborted();

        if (handshakeFailed)
        {
            throw new IOException("The TLS handshake failed; the connection is unusable.");
        }
    }

    private void ThrowIfUpgradeNotAllowed()
    {
        if (TlsSession is not null)
        {
            throw new InvalidOperationException("The connection is already secured with TLS.");
        }

        if (WritesCompleted)
        {
            throw new InvalidOperationException("The connection was half-closed; it cannot be upgraded to TLS.");
        }

        if (readPending)
        {
            throw new InvalidOperationException("A read is pending; the connection cannot be upgraded to TLS.");
        }
    }

    private void ThrowIfAborted()
    {
        if (Aborted)
        {
            throw new IOException("The connection was aborted.");
        }
    }
}
