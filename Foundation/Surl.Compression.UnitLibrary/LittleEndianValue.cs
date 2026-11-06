namespace Surl.Compression;

/// <summary>Reads an unsigned little-endian integer of 0 to 8 bytes, the width zstd's header fields vary by.</summary>
internal static class LittleEndianValue
{
    /// <summary>The value of <paramref name="bytes"/>, least significant byte first; 0 when empty.</summary>
    public static ulong Read(ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;
        for (int index = bytes.Length - 1; index >= 0; index--)
        {
            value = (value << 8) | bytes[index];
        }

        return value;
    }
}
