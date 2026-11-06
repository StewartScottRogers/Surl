namespace Surl.Compression;

/// <summary>
/// Writes a zstd backward bit stream (RFC 8878, section 4.1) from fields given in the order a
/// decoder reads them: the fields are packed least significant bit first in reverse order, and the
/// end mark, a 1 bit, follows the last, so that the decoder, reading from the end mark down, meets
/// them in the order given, each most significant bit first.
/// </summary>
internal sealed class ZstdBackwardBitStreamWriter
{
    private readonly List<(int Width, long Value)> fields = [];

    /// <summary>Adds a field of <paramref name="width"/> bits, which the decoder reads after the ones added before it.</summary>
    public ZstdBackwardBitStreamWriter Add(int width, long value)
    {
        fields.Add((width, value));
        return this;
    }

    /// <summary>The stream's bytes, ending in the byte holding the end mark.</summary>
    public byte[] ToArray()
    {
        List<bool> bits = [];
        for (int field = fields.Count - 1; field >= 0; field--)
        {
            for (int bit = 0; bit < fields[field].Width; bit++)
            {
                bits.Add(((fields[field].Value >> bit) & 1) != 0);
            }
        }

        bits.Add(true);
        return ZstdForwardBits.Pack(bits);
    }
}
