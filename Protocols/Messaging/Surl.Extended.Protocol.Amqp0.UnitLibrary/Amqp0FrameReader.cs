using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Reads AMQP 0-9-1 frames from one connection, one after another, however their bytes are
/// split across reads (section 4.2.3).
/// </summary>
/// <remarks>
/// The 7-byte header is judged before the payload is read: an unknown type, a frame whose
/// whole size (header, payload and frame-end) is above the frame-max given, and a heartbeat on a
/// channel other than 0 are refused from the header alone (ADR-0112 decisions 3 and 8). A read
/// never asks for more than what is left of the frame, so the reader never takes a byte of the
/// next one. Not safe for concurrent calls.
/// </remarks>
/// <param name="connection">The connection to read from.</param>
public sealed class Amqp0FrameReader(IConnection connection)
{
    private readonly byte[] header = new byte[Amqp0Frame.HeaderBytes];

    /// <summary>Reads the next frame.</summary>
    /// <param name="frameMax">
    /// The largest frame allowed, header and frame-end included: 4096 before <c>tune-ok</c>, the
    /// negotiated frame-max after it (ADR-0112 decision 3).
    /// </param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The frame, or the named reason there is none.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frameMax"/> is below <see cref="Amqp0Frame.OverheadBytes"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    public async ValueTask<Amqp0FrameReadResult> ReadFrameAsync(int frameMax, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frameMax, Amqp0Frame.OverheadBytes);

        var headerRead = await FillAsync(header, cancellationToken);
        if (headerRead < header.Length)
        {
            return new(headerRead == 0 ? Amqp0FrameReadOutcome.ConnectionClosed : Amqp0FrameReadOutcome.ConnectionClosedMidFrame, null);
        }

        var type = (Amqp0FrameType)header[0];
        var channel = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1));
        var size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(3));
        var refusal = JudgeHeader(type, channel, size, frameMax);
        if (refusal is not null)
        {
            return new(refusal.Value, null);
        }

        return await ReadPayloadAsync(type, channel, (int)size, cancellationToken);
    }

    private static Amqp0FrameReadOutcome? JudgeHeader(Amqp0FrameType type, ushort channel, uint size, int frameMax)
    {
        if (!Enum.IsDefined(type))
        {
            return Amqp0FrameReadOutcome.UnknownFrameType;
        }

        if (size + (long)Amqp0Frame.OverheadBytes > frameMax)
        {
            return Amqp0FrameReadOutcome.FrameTooLarge;
        }

        return type == Amqp0FrameType.Heartbeat && channel != 0 ? Amqp0FrameReadOutcome.HeartbeatOffChannelZero : null;
    }

    private async ValueTask<Amqp0FrameReadResult> ReadPayloadAsync(Amqp0FrameType type, ushort channel, int size, CancellationToken cancellationToken)
    {
        var payloadAndEnd = new byte[size + 1];
        if (await FillAsync(payloadAndEnd, cancellationToken) < payloadAndEnd.Length)
        {
            return new(Amqp0FrameReadOutcome.ConnectionClosedMidFrame, null);
        }

        if (payloadAndEnd[size] != Amqp0Frame.FrameEnd)
        {
            return new(Amqp0FrameReadOutcome.BadFrameEnd, null);
        }

        return new(Amqp0FrameReadOutcome.FrameRead, new Amqp0Frame(type, channel, payloadAndEnd.AsMemory(0, size)));
    }

    // Reads until the buffer is full or the client closes; returns how many bytes arrived.
    private async ValueTask<int> FillAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer[filled..], cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }
}
