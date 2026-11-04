using System.Buffers.Binary;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One open <c>session</c> channel (RFC 4254, sections 5 and 6; ADR-0051 decision 9): the
/// client's data buffered within the window the server granted, which is granted again once
/// half of it has been read, and the server's data sent within the window and maximum packet
/// the client granted. The connection's message loop feeds it; its handler reads and writes it
/// on another task, so its state is locked.
/// </summary>
internal sealed class SshSessionChannel : ISshChannelDataStream
{
    /// <summary>
    /// The window the server grants each channel, and the most of the client's data it ever buffers.
    /// </summary>
    public const uint WindowBytes = 2097152;

    /// <summary>
    /// The largest channel data the server asks the client to send in one packet.
    /// </summary>
    public const uint MaxPacketBytes = 32768;

    private readonly Lock gate = new();
    private readonly Queue<ReadOnlyMemory<byte>> inbound = new();
    private readonly SshConnectionProtocol connection;
    private readonly uint clientMaxPacket;
    private int headOffset;
    private uint window = WindowBytes;
    private uint readSinceGrant;
    private uint clientWindow;
    private bool clientSentEof;
    private bool inboundEnded;
    private bool closed;
    private TaskCompletionSource? inboundWaiter;
    private TaskCompletionSource? windowWaiter;

    /// <summary>
    /// Creates the channel the client asked to open.
    /// </summary>
    /// <param name="connection">The connection the channel's messages are written on.</param>
    /// <param name="number">The server's number for the channel.</param>
    /// <param name="clientNumber">The client's number for the channel, which every message the server sends on it names.</param>
    /// <param name="clientWindow">How many bytes the client lets the server send before it widens the window.</param>
    /// <param name="clientMaxPacket">The largest channel data the client takes in one packet.</param>
    public SshSessionChannel(SshConnectionProtocol connection, uint number, uint clientNumber, uint clientWindow, uint clientMaxPacket)
    {
        this.connection = connection;
        Number = number;
        ClientNumber = clientNumber;
        this.clientWindow = clientWindow;
        this.clientMaxPacket = Math.Max(1u, clientMaxPacket);
    }

    /// <summary>
    /// The server's number for the channel.
    /// </summary>
    public uint Number { get; }

    /// <summary>
    /// The client's number for the channel.
    /// </summary>
    public uint ClientNumber { get; }

    /// <summary>
    /// Whether an <c>exec</c> or <c>subsystem</c> has been handed to a handler on this channel.
    /// </summary>
    public bool HasStarted { get; set; }

    /// <summary>
    /// Whether the server has sent <c>CLOSE</c>; read and set only under the connection's write gate.
    /// </summary>
    public bool CloseSent { get; set; }

    /// <summary>
    /// Whether <see cref="End"/> has been called: the client closed the channel or the connection ended.
    /// </summary>
    public bool IsEnded
    {
        get
        {
            lock (gate)
            {
                return closed;
            }
        }
    }

    /// <summary>
    /// Takes channel data from the client into the buffer.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <exception cref="SshDisconnectRequiredException">The data runs past the window, or comes after the client's <c>EOF</c>.</exception>
    public void ReceiveData(ReadOnlyMemory<byte> data)
    {
        lock (gate)
        {
            TakeIntoWindow(data.Length);
            inbound.Enqueue(data);
            Wake(ref inboundWaiter);
        }
    }

    /// <summary>
    /// Takes extended data (a client's <c>stderr</c>) from the client and discards it, which
    /// counts as reading it.
    /// </summary>
    /// <param name="length">How many bytes it held.</param>
    /// <returns>The bytes to grant the client again, 0 when none are due yet.</returns>
    /// <exception cref="SshDisconnectRequiredException">The data runs past the window, or comes after the client's <c>EOF</c>.</exception>
    public uint ReceiveDiscardedData(int length)
    {
        lock (gate)
        {
            TakeIntoWindow(length);

            return CountRead(length);
        }
    }

    /// <summary>
    /// Takes the client's <c>EOF</c>: reads return 0 once the buffer is empty.
    /// </summary>
    public void ReceiveEof()
    {
        lock (gate)
        {
            clientSentEof = true;
            inboundEnded = true;
            Wake(ref inboundWaiter);
        }
    }

    /// <summary>
    /// Widens the window the client granted.
    /// </summary>
    /// <param name="bytes">How many more bytes the server may send.</param>
    /// <exception cref="SshDisconnectRequiredException">The window would pass 2^32 - 1 bytes (RFC 4254, section 5.2).</exception>
    public void AdjustClientWindow(uint bytes)
    {
        lock (gate)
        {
            if (clientWindow + (ulong)bytes > uint.MaxValue)
            {
                throw SshDisconnectRequiredException.ProtocolError($"The client widened the window of SSH channel {Number} past 2^32 - 1 bytes.");
            }

            clientWindow += bytes;
            Wake(ref windowWaiter);
        }
    }

    /// <summary>
    /// Closes the channel's data both ways, because the client closed the channel or the
    /// connection ended: reads return 0 once the buffer is empty, and writes fail.
    /// </summary>
    public void End()
    {
        lock (gate)
        {
            inboundEnded = true;
            closed = true;
            Wake(ref inboundWaiter);
            Wake(ref windowWaiter);
        }
    }

    /// <summary>
    /// Sends <c>SSH_MSG_CHANNEL_WINDOW_ADJUST</c> for <paramref name="bytes"/>, unless it is 0.
    /// </summary>
    /// <param name="bytes">How many bytes to grant the client again.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the grant is written or skipped.</returns>
    public async ValueTask GrantAsync(uint bytes, CancellationToken cancellationToken)
    {
        if (bytes > 0)
        {
            await connection.WriteOnChannelAsync(this, Message(SshMessageNumber.ChannelWindowAdjust, bytes), closes: false, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            var read = TakeInbound(buffer, out var grant, out var wait);
            if (wait is null)
            {
                await GrantAsync(grant, cancellationToken);

                return read;
            }

            await wait.WaitAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        while (!data.IsEmpty)
        {
            var count = ReserveClientWindow(data.Length, out var wait);
            if (wait is not null)
            {
                await wait.WaitAsync(cancellationToken);
                continue;
            }

            var message = new SshWireWriter();
            message.WriteByte(SshMessageNumber.ChannelData);
            message.WriteUInt32(ClientNumber);
            message.WriteString(data.Span[..count]);

            // A channel that ends between the reservation and the write drops the packet; the
            // next reservation, or the handler's next read, tells the handler.
            await connection.WriteOnChannelAsync(this, message.ToArray(), closes: false, cancellationToken);
            data = data[count..];
        }
    }

    /// <summary>
    /// A message of <paramref name="messageNumber"/> naming the client's channel number, then
    /// <paramref name="fields"/> as <c>uint32</c>s.
    /// </summary>
    /// <param name="messageNumber">The message number.</param>
    /// <param name="fields">The fields after the channel number.</param>
    /// <returns>The payload.</returns>
    public byte[] Message(byte messageNumber, params uint[] fields)
    {
        var message = new byte[5 + (4 * fields.Length)];
        message[0] = messageNumber;
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(1), ClientNumber);
        for (var field = 0; field < fields.Length; field++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(5 + (4 * field)), fields[field]);
        }

        return message;
    }

    private static IOException ClosedFailure() => new("The SSH channel was closed before the data was sent.");

    private static void Wake(ref TaskCompletionSource? waiter)
    {
        waiter?.TrySetResult();
        waiter = null;
    }

    private static Task NewWaiter(out TaskCompletionSource waiter)
    {
        waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return waiter.Task;
    }

    // Under the lock: the client may send no more than the window, and nothing after its EOF.
    private void TakeIntoWindow(int length)
    {
        if (clientSentEof)
        {
            throw SshDisconnectRequiredException.ProtocolError($"The client sent data on SSH channel {Number} after its EOF.");
        }

        if ((uint)length > window)
        {
            throw SshDisconnectRequiredException.ProtocolError($"The client sent {length} bytes on SSH channel {Number}, past its window of {window}.");
        }

        window -= (uint)length;
    }

    // Under the lock: counts bytes read, and grants them again once half the window is read.
    private uint CountRead(int length)
    {
        readSinceGrant += (uint)length;
        if (readSinceGrant < WindowBytes / 2)
        {
            return 0;
        }

        var grant = readSinceGrant;
        window += grant;
        readSinceGrant = 0;

        return grant;
    }

    private int TakeInbound(Memory<byte> buffer, out uint grant, out Task? wait)
    {
        lock (gate)
        {
            grant = 0;
            wait = null;
            if (inbound.Count == 0)
            {
                wait = inboundEnded ? null : NewWaiter(out inboundWaiter);

                return 0;
            }

            var head = inbound.Peek()[headOffset..];
            var read = Math.Min(head.Length, buffer.Length);
            head[..read].CopyTo(buffer);
            headOffset += read;
            if (read == head.Length)
            {
                inbound.Dequeue();
                headOffset = 0;
            }

            grant = CountRead(read);

            return read;
        }
    }

    private int ReserveClientWindow(int wanted, out Task? wait)
    {
        lock (gate)
        {
            if (closed)
            {
                throw ClosedFailure();
            }

            var count = (int)Math.Min((uint)wanted, Math.Min(clientWindow, clientMaxPacket));
            wait = count == 0 ? NewWaiter(out windowWaiter) : null;
            clientWindow -= (uint)count;

            return count;
        }
    }
}
