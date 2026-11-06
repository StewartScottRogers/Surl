namespace Surl.Quic;

/// <summary>
/// Puts a byte stream sent at offsets back in order: pieces may arrive out of order, overlap and
/// repeat, and each byte comes out once, in order. <c>CRYPTO</c> data and <c>STREAM</c> data share it
/// (RFC 9000 sections 2.2 and 7.5); the caller bounds how far ahead a piece may reach.
/// </summary>
internal sealed class QuicOrderedBytes
{
    private readonly SortedDictionary<ulong, byte[]> pending = [];

    /// <summary>How many bytes have come out so far: the offset the next byte out has.</summary>
    public ulong Delivered { get; private set; }

    /// <summary>Takes the bytes at <paramref name="offset"/> and returns the bytes that are now in order and new.</summary>
    public byte[] Add(ulong offset, ReadOnlySpan<byte> data)
    {
        if (offset + (ulong)data.Length <= Delivered)
        {
            return [];
        }

        if (!pending.TryGetValue(offset, out var existing) || existing.Length < data.Length)
        {
            pending[offset] = data.ToArray();
        }

        return TakeContiguous();
    }

    private byte[] TakeContiguous()
    {
        var output = new List<byte>();
        while (pending.Count > 0 && pending.First() is var (start, bytes) && start <= Delivered)
        {
            pending.Remove(start);
            var alreadyDelivered = Delivered - start;
            if (alreadyDelivered < (ulong)bytes.Length)
            {
                output.AddRange(bytes.AsSpan((int)alreadyDelivered));
                Delivered = start + (ulong)bytes.Length;
            }
        }

        return [.. output];
    }
}
