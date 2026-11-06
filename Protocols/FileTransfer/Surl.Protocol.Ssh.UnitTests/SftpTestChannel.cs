using System.Buffers.Binary;

namespace Surl.Protocol.Ssh;

/// <summary>
/// A session channel's data with the client's side scripted: <see cref="ReadAsync"/> hands out the
/// inbound chunks in order, each at most one buffer at a time, then reports <c>EOF</c>; everything
/// the handler writes is kept in <see cref="Written"/>.
/// </summary>
internal sealed class SftpTestChannel(params byte[][] inbound) : ISshChannelDataStream
{
    private readonly Queue<ReadOnlyMemory<byte>> chunks = new(inbound.Select(chunk => (ReadOnlyMemory<byte>)chunk));
    private int taken;

    public List<byte> Written { get; } = [];

    /// <summary>
    /// <see cref="Written"/> cut into packets at their length fields, each with its length.
    /// </summary>
    public List<byte[]> WrittenPackets()
    {
        var packets = new List<byte[]>();
        var bytes = Written.ToArray();
        for (var offset = 0; offset < bytes.Length;)
        {
            var length = 4 + (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset));
            packets.Add(bytes[offset..(offset + length)]);
            offset += length;
        }

        return packets;
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!chunks.TryPeek(out var chunk))
        {
            return ValueTask.FromResult(0);
        }

        var count = Math.Min(buffer.Length, chunk.Length - taken);
        chunk.Slice(taken, count).CopyTo(buffer);
        taken += count;
        if (taken == chunk.Length)
        {
            chunks.Dequeue();
            taken = 0;
        }

        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        Written.AddRange(data.ToArray());

        return ValueTask.CompletedTask;
    }
}
