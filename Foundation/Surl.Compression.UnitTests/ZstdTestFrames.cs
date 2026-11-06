namespace Surl.Compression;

/// <summary>
/// Writes zstd frames and blocks as RFC 8878, section 3.1.1 lays them out: the magic, the frame
/// header descriptor and its fields, and blocks behind 3-byte little-endian headers holding Last_Block
/// (bit 0), Block_Type (bits 1 and 2) and Block_Size (the rest).
/// </summary>
internal static class ZstdTestFrames
{
    /// <summary>The zstd frame magic number 0xFD2FB528, little-endian.</summary>
    public static readonly byte[] Magic = [0x28, 0xB5, 0x2F, 0xFD];

    /// <summary>A frame with a window descriptor and no content size, checksum or dictionary id.</summary>
    public static byte[] Frame(params byte[][] blocks) => [.. Magic, 0x00, 0x00, .. blocks.SelectMany(block => block)];

    /// <summary>A frame whose header, after the magic, is <paramref name="header"/>.</summary>
    public static byte[] FrameWithHeader(byte[] header, params byte[][] blocks) => [.. Magic, .. header, .. blocks.SelectMany(block => block)];

    /// <summary>A block of <paramref name="type"/> whose header gives <paramref name="size"/>.</summary>
    public static byte[] Block(bool last, int type, int size, byte[] content)
    {
        int header = (size << 3) | (type << 1) | (last ? 1 : 0);
        return [(byte)header, (byte)(header >> 8), (byte)(header >> 16), .. content];
    }

    /// <summary>A Raw_Block of <paramref name="bytes"/>.</summary>
    public static byte[] RawBlock(bool last, byte[] bytes) => Block(last, 0, bytes.Length, bytes);

    /// <summary>An RLE_Block: <paramref name="value"/> <paramref name="count"/> times.</summary>
    public static byte[] RleBlock(bool last, byte value, int count) => Block(last, 1, count, [value]);

    /// <summary>A Compressed_Block of a literals section and a sequences section.</summary>
    public static byte[] CompressedBlock(bool last, byte[] literalsSection, byte[] sequencesSection) =>
        Block(last, 2, literalsSection.Length + sequencesSection.Length, [.. literalsSection, .. sequencesSection]);

    /// <summary>A skippable frame of <paramref name="content"/> under the magic 0x184D2A5 and <paramref name="nibble"/>.</summary>
    public static byte[] SkippableFrame(int nibble, byte[] content) =>
    [
        (byte)(0x50 | nibble), 0x2A, 0x4D, 0x18,
        (byte)content.Length, (byte)(content.Length >> 8), (byte)(content.Length >> 16), (byte)(content.Length >> 24),
        .. content,
    ];
}
