namespace Surl.Compression;

/// <summary>Packs bits into bytes forward, least significant bit first, as zstd's forward fields are.</summary>
internal static class ZstdForwardBits
{
    /// <summary>Appends <paramref name="value"/>'s low <paramref name="width"/> bits to <paramref name="bits"/>, least significant first.</summary>
    public static void Put(List<bool> bits, int value, int width)
    {
        for (int bit = 0; bit < width; bit++)
        {
            bits.Add(((value >> bit) & 1) != 0);
        }
    }

    /// <summary>The bytes holding <paramref name="bits"/>, the last byte padded with zeros.</summary>
    public static byte[] Pack(List<bool> bits)
    {
        byte[] bytes = new byte[(bits.Count + 7) / 8];
        for (int index = 0; index < bits.Count; index++)
        {
            bytes[index >> 3] |= (byte)(bits[index] ? 1 << (index & 7) : 0);
        }

        return bytes;
    }
}
