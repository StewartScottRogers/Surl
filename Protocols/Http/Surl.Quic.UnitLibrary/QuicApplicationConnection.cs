using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Quic;

/// <summary>
/// One QUIC connection as an <see cref="IQuicApplicationProtocol"/> sees it: the client's streams
/// to accept, reads that wait for bytes, and writes, resets and a close that are sent at once. It
/// is the <see cref="QuicServerConnection"/> of a <see cref="QuicDatagramFlowServer"/>, which
/// receives datagrams and acts on timers while the application works, so every call takes the
/// same lock and is safe from any thread.
/// </summary>
/// <remarks>
/// Once the connection has ended - closed by either side, idle, or the server stopped - a read
/// returns the bytes already received and then 0, a datagram receive the <c>DATAGRAM</c> data already
/// received and then null, an accept returns null, <see cref="CloseAsync"/> does nothing, and every other call
/// throws <see cref="OperationCanceledException"/> with <see cref="Ended"/>.
/// </remarks>
public sealed class QuicApplicationConnection
{
    private readonly QuicServerConnection connection;
    private readonly IDatagramFlow flow;
    private readonly CancellationTokenSource ending;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Channel<QuicStream> clientStreams = Channel.CreateUnbounded<QuicStream>();
    private readonly HashSet<ulong> clientStreamIds = [];
    private readonly Channel<byte[]> receivedDatagrams = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(ReceivedDatagramCapacity) { FullMode = BoundedChannelFullMode.DropOldest });
    private TaskCompletionSource changed = NewChangedSignal();
    private bool ended;

    internal QuicApplicationConnection(QuicServerConnection connection, IDatagramFlow flow, CancellationTokenSource ending)
    {
        this.connection = connection;
        this.flow = flow;
        this.ending = ending;
        Ended = ending.Token;
    }

    /// <summary>
    /// How many received <c>DATAGRAM</c> frames wait for <see cref="ReceiveDatagramAsync"/> at most;
    /// past it the oldest is dropped, as RFC 9221 section 5 lets a receiver drop any.
    /// </summary>
    public const int ReceivedDatagramCapacity = 128;

    /// <summary>
    /// How many bytes <see cref="WriteAsync"/> lets wait unsent on one stream at most, 64 KiB: past
    /// it a write waits for the client's credit, so an answer of any size holds a bounded amount in
    /// memory (ADR-0082 amendment 3).
    /// </summary>
    public const int MaxUnsentLength = 65536;

    /// <summary>The ALPN protocol the handshake agreed, or null when the options named none.</summary>
    public string? ApplicationProtocol => connection.ApplicationProtocol;

    /// <summary>The TLS 1.3 cipher suite the handshake agreed.</summary>
    public Surl.Tls13.TlsCipherSuite CipherSuite => connection.CipherSuite!.Value;

    /// <summary>The client's transport parameters.</summary>
    public QuicTransportParameters PeerTransportParameters => connection.PeerTransportParameters!;

    /// <summary>Cancelled once the connection has ended or the server stops.</summary>
    public CancellationToken Ended { get; }

    private bool IsOver => ended || connection.IsClosed;

    /// <summary>Waits for the next stream the client opens, in the order the client's frames reached them.</summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The stream, or null once the connection has ended.</returns>
    public async ValueTask<QuicStream?> AcceptStreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await clientStreams.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Waits for the data of the next <c>DATAGRAM</c> frame the client sends (RFC 9221), in the
    /// order received; at most <see cref="ReceivedDatagramCapacity"/> wait, the oldest dropped past it.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>A copy of the frame's data, or null once the connection has ended and none received is left.</returns>
    public async ValueTask<byte[]?> ReceiveDatagramAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await receivedDatagrams.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <summary>Sends <paramref name="data"/> in one <c>DATAGRAM</c> frame, as <see cref="QuicServerConnection.SendDatagram"/> does.</summary>
    /// <param name="data">The application data.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once what the congestion window allows is sent.</returns>
    /// <exception cref="InvalidOperationException">The client's <c>max_datagram_frame_size</c> is absent or 0.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The frame would be larger than the client takes or than one packet holds.</exception>
    public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
        ActAsync(() => connection.SendDatagram(data), cancellationToken);

    /// <summary>
    /// Waits until <paramref name="stream"/> has bytes, then copies them into <paramref name="buffer"/>
    /// and sends the credit reading them grants.
    /// </summary>
    /// <param name="stream">A stream of this connection with a receiving part.</param>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>How many bytes were copied: 0 for an empty buffer, once the stream's receiving part has ended (its <c>FIN</c> read, or reset), or once the connection has ended and nothing received is left.</returns>
    public async ValueTask<int> ReadAsync(QuicStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        while (true)
        {
            await gate.WaitAsync(cancellationToken);
            Task signal;
            try
            {
                var count = stream.Read(buffer.Span);
                if (count > 0 || buffer.IsEmpty || stream.IsReceiveDone || IsOver)
                {
                    await TransmitIfOpenAsync(cancellationToken);
                    return count;
                }

                signal = changed.Task;
            }
            finally
            {
                gate.Release();
            }

            await signal.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Queues <paramref name="data"/> on <paramref name="stream"/>, with <paramref name="fin"/> ending
    /// it, and sends what the client's credit allows, never letting the stream's
    /// <see cref="QuicStream.UnsentLength"/> pass <see cref="MaxUnsentLength"/>: it queues what fits,
    /// then waits for the client's <c>MAX_STREAM_DATA</c>, <c>MAX_DATA</c> or acknowledgements to let
    /// some go before it queues more.
    /// </summary>
    /// <param name="stream">A stream of this connection with a sending part.</param>
    /// <param name="data">The bytes.</param>
    /// <param name="fin">Whether the stream ends after them.</param>
    /// <param name="cancellationToken">Cuts the send, or the wait for credit, off.</param>
    /// <returns>A task that completes once the last of the bytes is queued and what can be sent is sent; at most <see cref="MaxUnsentLength"/> waits for credit then.</returns>
    /// <exception cref="OperationCanceledException">The connection has ended, before or while the write waited.</exception>
    /// <exception cref="InvalidOperationException">The stream has no sending part, or it has ended or been reset, before or while the write waited.</exception>
    public async ValueTask WriteAsync(QuicStream stream, ReadOnlyMemory<byte> data, bool fin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var rest = data;
        while (true)
        {
            var (queued, roomChanged) = await QueueWhatFitsAsync(stream, rest, fin, cancellationToken);
            if (roomChanged is null)
            {
                return;
            }

            rest = rest[queued..];
            await roomChanged.WaitAsync(cancellationToken);
        }
    }

    /// <summary>Abandons <paramref name="stream"/>'s sending part with <c>RESET_STREAM</c>.</summary>
    /// <param name="stream">A stream of this connection with a sending part.</param>
    /// <param name="applicationErrorCode">Why, in the application protocol's error codes.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the frame is sent.</returns>
    public ValueTask ResetAsync(QuicStream stream, ulong applicationErrorCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ActAsync(() => stream.Reset(applicationErrorCode), cancellationToken);
    }

    /// <summary>Abandons <paramref name="stream"/>'s sending part with <c>RESET_STREAM_AT</c>, as <see cref="QuicStream.ResetAt"/> does, still delivering its first <paramref name="reliableSize"/> bytes.</summary>
    /// <param name="stream">A stream of this connection with a sending part.</param>
    /// <param name="applicationErrorCode">Why, in the application protocol's error codes.</param>
    /// <param name="reliableSize">How many bytes from the start of the stream are still delivered.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once what can be sent is sent; the rest, and then the frame, go as credit arrives.</returns>
    public ValueTask ResetAtAsync(QuicStream stream, ulong applicationErrorCode, ulong reliableSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ActAsync(() => stream.ResetAt(applicationErrorCode, reliableSize), cancellationToken);
    }

    /// <summary>Asks the client to stop sending on <paramref name="stream"/> with <c>STOP_SENDING</c>.</summary>
    /// <param name="stream">A stream of this connection with a receiving part.</param>
    /// <param name="applicationErrorCode">Why, in the application protocol's error codes.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the frame is sent.</returns>
    public ValueTask StopSendingAsync(QuicStream stream, ulong applicationErrorCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ActAsync(() => stream.StopSending(applicationErrorCode), cancellationToken);
    }

    /// <summary>Opens the server's next unidirectional stream, as <see cref="QuicServerConnection.OpenUnidirectionalStream"/> does.</summary>
    /// <param name="cancellationToken">Cuts the wait for the lock off.</param>
    /// <returns>The stream, or null when the client's <c>MAX_STREAMS</c> allows no more.</returns>
    public async ValueTask<QuicStream?> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        QuicStream? stream = null;
        await ActAsync(() => stream = connection.OpenUnidirectionalStream(), cancellationToken);
        return stream;
    }

    /// <summary>Ends the connection with an application error, as <see cref="QuicServerConnection.Close"/> does; nothing once it has ended.</summary>
    /// <param name="applicationErrorCode">The application's error code.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once <c>CONNECTION_CLOSE</c> is sent.</returns>
    public async ValueTask CloseAsync(ulong applicationErrorCode, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!IsOver)
            {
                await SendAsync(connection.Close(applicationErrorCode), cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Calls the connection under the lock, hands new client streams to <see cref="AcceptStreamAsync"/> and <c>DATAGRAM</c> data to <see cref="ReceiveDatagramAsync"/>, sends its datagrams and wakes every waiter.</summary>
    internal async Task ApplyAsync(Func<QuicServerConnection, QuicConnectionOutput> call, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var output = call(connection);
            PublishClientStreams(output.Streams);
            PublishReceivedDatagrams(output.ReceivedDatagramFrames);
            await SendAsync(output, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Under the lock: whether the connection is closed, whether its handshake is complete, its next deadline, and the signal the next change completes.</summary>
    internal async Task<(bool IsClosed, bool IsHandshakeComplete, DateTimeOffset? Deadline, Task Changed)> ObserveAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return (connection.IsClosed, connection.IsHandshakeComplete, connection.NextDeadline, changed.Task);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Ends the application's side: accepts return null, reads 0, other calls throw, and <see cref="Ended"/> is cancelled, which also stops the loop's waits.</summary>
    internal async Task EndAsync()
    {
        await gate.WaitAsync(CancellationToken.None);
        try
        {
            ended = true;
            clientStreams.Writer.TryComplete();
            receivedDatagrams.Writer.TryComplete();
            Signal();
        }
        finally
        {
            gate.Release();
        }

        await ending.CancelAsync();
    }

    private static TaskCompletionSource NewChangedSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ValueTask ActAsync(Action act, CancellationToken cancellationToken) =>
        ActAsync(
            () =>
            {
                act();
                return connection.Transmit();
            },
            cancellationToken);

    private async ValueTask ActAsync(Func<QuicConnectionOutput> act, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (IsOver)
            {
                throw new OperationCanceledException("The QUIC connection has ended.", Ended);
            }

            await SendAsync(act(), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Under the lock: queues as much of <paramref name="rest"/> as keeps the stream's unsent bytes
    /// within <see cref="MaxUnsentLength"/>, the <c>FIN</c> only with the last of it, and sends what
    /// the client's credit allows.
    /// </summary>
    /// <returns>How many bytes were queued, and null once all were, else the signal the next change completes.</returns>
    private async Task<(int Queued, Task? RoomChanged)> QueueWhatFitsAsync(QuicStream stream, ReadOnlyMemory<byte> rest, bool fin, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (IsOver)
            {
                throw new OperationCanceledException("The QUIC connection has ended.", Ended);
            }

            var queued = Math.Min(Math.Max(MaxUnsentLength - stream.UnsentLength, 0), rest.Length);
            var isLast = queued == rest.Length;
            stream.Write(rest.Span[..queued], fin && isLast);
            await SendAsync(connection.Transmit(), cancellationToken);
            return (queued, isLast ? null : changed.Task);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task TransmitIfOpenAsync(CancellationToken cancellationToken)
    {
        if (!IsOver)
        {
            await SendAsync(connection.Transmit(), cancellationToken);
        }
    }

    private void PublishClientStreams(IReadOnlyList<QuicStream> streams)
    {
        foreach (var stream in streams)
        {
            if (stream.IsClientInitiated && clientStreamIds.Add(stream.Id))
            {
                clientStreams.Writer.TryWrite(stream);
            }
        }
    }

    /// <summary>Queues a copy of each frame's data, so it outlives the packet it was read from.</summary>
    private void PublishReceivedDatagrams(IReadOnlyList<QuicDatagramFrame> frames)
    {
        foreach (var frame in frames)
        {
            receivedDatagrams.Writer.TryWrite(frame.Data.ToArray());
        }
    }

    /// <summary>Sends the output's datagrams, in order, and wakes every waiter; called under the lock.</summary>
    private async Task SendAsync(QuicConnectionOutput output, CancellationToken cancellationToken)
    {
        foreach (var datagram in output.Datagrams)
        {
            await flow.SendAsync(datagram, cancellationToken);
        }

        Signal();
    }

    private void Signal()
    {
        var current = changed;
        changed = NewChangedSignal();
        current.TrySetResult();
    }
}
