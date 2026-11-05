using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Reads AMQP 1.0 frames (Part 2 section 2.3) from one connection, one after another, however
/// their bytes are split across reads.
/// </summary>
/// <remarks>
/// The 8-byte header is judged before the rest is read: a size below 8, a size above the
/// <c>max-frame-size</c> given, a DOFF below 2 or past the end of the frame, and a type other than
/// AMQP or SASL are refused from the header alone (ADR-0107 decisions 3 and 7). The extended
/// header, the bytes between the header and DOFF, is read and skipped (section 2.3.1). A read
/// never asks for more than what is left of the frame, so the reader never takes a byte of the
/// next one. Not safe for concurrent calls.
/// </remarks>
/// <param name="connection">The connection to read from.</param>
public sealed class Amqp1FrameReader(IConnection connection)
{
    private readonly byte[] header = new byte[Amqp1Frame.HeaderBytes];

    /// <summary>Reads the next frame.</summary>
    /// <param name="maxFrameSize">
    /// The largest frame allowed, header included: 512 before <c>open</c> and in SASL, the
    /// <c>max-frame-size</c> the container offered after it (ADR-0107 decision 7).
    /// </param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The frame, or the named reason there is none.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxFrameSize"/> is below <see cref="Amqp1Frame.HeaderBytes"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    public async ValueTask<Amqp1FrameReadResult> ReadFrameAsync(uint maxFrameSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFrameSize, (uint)Amqp1Frame.HeaderBytes);

        var headerRead = await FillAsync(header, cancellationToken);
        if (headerRead < header.Length)
        {
            return new(headerRead == 0 ? Amqp1FrameReadOutcome.ConnectionClosed : Amqp1FrameReadOutcome.ConnectionClosedMidFrame, null);
        }

        var size = BinaryPrimitives.ReadUInt32BigEndian(header);
        var dataOffsetBytes = header[4] * 4;
        var type = (Amqp1FrameType)header[5];
        var refusal = JudgeHeader(size, dataOffsetBytes, type, maxFrameSize);
        if (refusal is not null)
        {
            return new(refusal.Value, null);
        }

        var channel = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6));
        return await ReadRestAsync(type, channel, (int)size, dataOffsetBytes, cancellationToken);
    }

    private static Amqp1FrameReadOutcome? JudgeHeader(uint size, int dataOffsetBytes, Amqp1FrameType type, uint maxFrameSize)
    {
        if (size < Amqp1Frame.HeaderBytes)
        {
            return Amqp1FrameReadOutcome.FrameTooSmall;
        }

        if (size > maxFrameSize)
        {
            return Amqp1FrameReadOutcome.FrameTooLarge;
        }

        if (dataOffsetBytes < Amqp1Frame.HeaderBytes || dataOffsetBytes > size)
        {
            return Amqp1FrameReadOutcome.BadDataOffset;
        }

        return Enum.IsDefined(type) ? null : Amqp1FrameReadOutcome.UnknownFrameType;
    }

    private async ValueTask<Amqp1FrameReadResult> ReadRestAsync(Amqp1FrameType type, ushort channel, int size, int dataOffsetBytes, CancellationToken cancellationToken)
    {
        var rest = new byte[size - Amqp1Frame.HeaderBytes];
        if (await FillAsync(rest, cancellationToken) < rest.Length)
        {
            return new(Amqp1FrameReadOutcome.ConnectionClosedMidFrame, null);
        }

        var body = rest.AsMemory(dataOffsetBytes - Amqp1Frame.HeaderBytes);
        return new(Amqp1FrameReadOutcome.FrameRead, new Amqp1Frame(type, channel, body));
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
