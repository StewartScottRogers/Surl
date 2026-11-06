using System.Buffers.Binary;

namespace Surl.Compression;

/// <summary>
/// Encodes one zstd frame (RFC 8878) as ADR-0105 decision 4 specifies: a single-segment frame header
/// with <c>Frame_Content_Size</c> in the fewest bytes that hold it, no dictionary and no content
/// checksum, then the input in blocks of at most 128 KiB. A block of one byte repeated is an RLE
/// block; otherwise it is a Compressed block - Raw literals and a sequences section in
/// Predefined_Mode from a greedy matcher with repeat offsets - when that is smaller than the block,
/// and a Raw block when it is not. Empty input is one empty Raw block.
/// </summary>
public static class ZstdFrameEncoder
{
    /// <summary>How much input each block holds: Block_Maximum_Size, 128 KiB.</summary>
    public const int BlockInputLength = ZstdFrameDecoder.MaximumBlockSize;

    private const byte SingleSegmentFlag = 0x20;
    private const int RawBlock = 0;
    private const int RleBlock = 1;
    private const int CompressedBlock = 2;
    private const int OneByteContentSizeLimit = 256;
    private const int TwoByteContentSizeLimit = 65536 + 256;
    private const int OneByteLiteralsSizeLimit = 32;
    private const int TwoByteLiteralsSizeLimit = 4096;
    private const int TwoByteLiteralsSizeFormat = 0b0100;
    private const int ThreeByteLiteralsSizeFormat = 0b1100;

    /// <summary>The zstd frame holding <paramref name="input"/>.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> input)
    {
        byte[] data = input.ToArray();
        List<byte> frame = [];
        AppendLittleEndian(frame, ZstdFrameDecoder.MagicNumber, sizeof(uint));
        AppendHeader(frame, data.Length);
        ZstdSequenceFinder finder = new(data);
        int start = 0;
        do
        {
            int end = Math.Min(start + BlockInputLength, data.Length);
            AppendBlock(frame, data, finder, start, end);
            start = end;
        }
        while (start < data.Length);

        return [.. frame];
    }

    /// <summary>
    /// The frame header after the magic (RFC 8878, section 3.1.1.1): a descriptor marking the frame
    /// single-segment, so that no window descriptor follows, and <c>Frame_Content_Size</c> in 1, 2
    /// (counting from 256) or 4 bytes.
    /// </summary>
    private static void AppendHeader(List<byte> frame, int contentSize)
    {
        (int flag, int length, int value) = contentSize switch
        {
            < OneByteContentSizeLimit => (0, 1, contentSize),
            < TwoByteContentSizeLimit => (1, 2, contentSize - OneByteContentSizeLimit),
            _ => (2, 4, contentSize),
        };
        frame.Add((byte)((flag << 6) | SingleSegmentFlag));
        AppendLittleEndian(frame, (uint)value, length);
    }

    /// <summary>Appends the block for <c>data[start..end]</c>, marked last when it reaches the end of the data.</summary>
    private static void AppendBlock(List<byte> frame, byte[] data, ZstdSequenceFinder finder, int start, int end)
    {
        bool last = end == data.Length;
        ReadOnlySpan<byte> block = data.AsSpan(start, end - start);
        if (block.Length > 1 && !block[1..].ContainsAnyExcept(block[0]))
        {
            AppendBlockHeader(frame, last, RleBlock, block.Length);
            frame.Add(block[0]);
            return;
        }

        ZstdBlockSequences found = finder.Find(start, end);
        byte[] compressed = [.. LiteralsHeader(found.Literals.Length), .. found.Literals, .. ZstdSequencesEncoder.Encode(found.Sequences)];
        if (compressed.Length < block.Length)
        {
            finder.Keep(found);
            AppendBlockHeader(frame, last, CompressedBlock, compressed.Length);
            frame.AddRange(compressed);
            return;
        }

        AppendBlockHeader(frame, last, RawBlock, block.Length);
        frame.AddRange(block);
    }

    /// <summary>
    /// A Raw literals section header (RFC 8878, section 3.1.1.3.1.1): block type 0, then the size in
    /// 5 bits of one byte, 12 bits of two (size format 01) or 20 bits of three (size format 11).
    /// </summary>
    private static byte[] LiteralsHeader(int size) => size switch
    {
        < OneByteLiteralsSizeLimit => [(byte)(size << 3)],
        < TwoByteLiteralsSizeLimit => [(byte)((size << 4) | TwoByteLiteralsSizeFormat), (byte)(size >> 4)],
        _ => [(byte)((size << 4) | ThreeByteLiteralsSizeFormat), (byte)(size >> 4), (byte)(size >> 12)],
    };

    /// <summary>The 3-byte block header (RFC 8878, section 3.1.1.2): Last_Block, Block_Type and Block_Size.</summary>
    private static void AppendBlockHeader(List<byte> frame, bool last, int type, int size) =>
        AppendLittleEndian(frame, (uint)((size << 3) | (type << 1) | (last ? 1 : 0)), 3);

    private static void AppendLittleEndian(List<byte> output, uint value, int length)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        output.AddRange(bytes[..length]);
    }
}
