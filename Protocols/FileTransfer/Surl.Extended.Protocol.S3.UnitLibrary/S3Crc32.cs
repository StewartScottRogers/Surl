namespace Surl.Extended.Protocol.S3;

/// <summary>
/// CRC-32 (ISO-HDLC: the polynomial 0x04C11DB7 reflected, 0xEDB88320), written by hand because the
/// base class library has none (<c>System.IO.Hashing</c> is a package; ADR-0108 decision 4.5).
/// The running value starts at all ones and is inverted at the end by the caller.
/// </summary>
internal static class S3Crc32
{
    private const uint ReflectedPolynomial = 0xEDB88320;

    private static readonly uint[] Table = BuildTable();

    /// <summary>
    /// Runs <paramref name="bytes"/> through the running value <paramref name="crc"/>.
    /// </summary>
    /// <param name="crc">The running value: <see cref="uint.MaxValue"/> before the first byte.</param>
    /// <param name="bytes">The next bytes.</param>
    /// <returns>The running value after them.</returns>
    public static uint Append(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = Table[(byte)(crc ^ value)] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var entry = index;
            for (var bit = 0; bit < 8; bit++)
            {
                entry = (entry & 1) == 1 ? (entry >> 1) ^ ReflectedPolynomial : entry >> 1;
            }

            table[index] = entry;
        }

        return table;
    }
}
