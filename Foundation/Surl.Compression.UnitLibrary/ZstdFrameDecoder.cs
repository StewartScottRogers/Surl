using System.Buffers.Binary;

namespace Surl.Compression;

/// <summary>
/// Decodes zstd compressed data (RFC 8878): one or more frames, each a zstd frame (the magic
/// <c>0xFD2FB528</c>, the frame header, Raw, RLE and Compressed blocks, and the optional content
/// checksum, the lowest 32 bits of the content's <see cref="XxHash64"/>) or a skippable frame, which
/// is skipped. Frames' contents are joined; a match never reaches into an earlier frame. A frame
/// that needs a dictionary is refused, there being none to decode against (ADR-0105, decision 4).
/// The window size is not enforced; the output bound is what limits a frame.
/// </summary>
public static class ZstdFrameDecoder
{
    /// <summary>A zstd frame's magic number, little-endian on the wire.</summary>
    internal const uint MagicNumber = 0xFD2FB528;

    /// <summary>A skippable frame's magic number, its lowest 4 bits free.</summary>
    internal const uint SkippableMagicNumber = 0x184D2A50;

    /// <summary>Block_Maximum_Size at its largest (RFC 8878, section 3.1.1.2.3).</summary>
    internal const int MaximumBlockSize = 128 * 1024;

    private const uint SkippableMagicMask = 0xFFFFFFF0;
    private const byte SingleSegmentFlag = 0x20;
    private const byte ReservedFlag = 0x08;
    private const byte ContentChecksumFlag = 0x04;
    private const int RawBlock = 0;
    private const int RleBlock = 1;
    private const int CompressedBlock = 2;
    private const int BlockHeaderLength = 3;

    /// <summary>The Dictionary_ID field's length, by the descriptor's lowest two bits.</summary>
    private static readonly int[] DictionaryIdLengths = [0, 1, 2, 4];

    /// <summary>The Frame_Content_Size field's length, by the descriptor's top two bits; 0 is 1 in a single-segment frame.</summary>
    private static readonly int[] ContentSizeLengths = [0, 2, 4, 8];

    /// <summary>
    /// Decodes the frames in <paramref name="input"/>, writing at most <paramref name="outputBound"/>
    /// bytes: a frame content size or output past the bound is <see cref="DecompressionOutcome.OutputBoundPassed"/>,
    /// a content checksum that disagrees is <see cref="DecompressionOutcome.ChecksumMismatch"/>, and
    /// anything else that breaks the format, no frame at all and a dictionary id included, is
    /// <see cref="DecompressionOutcome.Malformed"/>.
    /// </summary>
    public static DecompressionResult Decode(ReadOnlySpan<byte> input, int outputBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outputBound);
        try
        {
            return DecompressionResult.Decompressed(DecodeOrRefuse(input, outputBound));
        }
        catch (DecompressionRefusedException refusal)
        {
            return refusal.ToResult();
        }
    }

    private static byte[] DecodeOrRefuse(ReadOnlySpan<byte> input, int outputBound)
    {
        if (input.IsEmpty)
        {
            throw DecompressionRefusedException.Malformed("the input holds no frame");
        }

        BoundedDecompressionOutput output = new(outputBound, DecompressionOutcome.OutputBoundPassed, "the blocks pass the output bound");
        int position = 0;
        while (position < input.Length)
        {
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(Take(input, ref position, sizeof(uint)));
            if ((magic & SkippableMagicMask) == SkippableMagicNumber)
            {
                Take(input, ref position, (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(Take(input, ref position, sizeof(uint))), int.MaxValue));
            }
            else
            {
                DecodeFrame(input, ref position, magic, output);
            }
        }

        return output.ToArray();
    }

    private static void DecodeFrame(ReadOnlySpan<byte> input, ref int position, uint magic, BoundedDecompressionOutput output)
    {
        if (magic != MagicNumber)
        {
            throw DecompressionRefusedException.Malformed("the magic number is not a zstd frame's");
        }

        int frameStart = output.Count;
        byte descriptor = ReadHeader(input, ref position, output, out ulong? contentSize);
        ZstdLiteralsSection literals = new();
        ZstdSequencesSection sequences = new(frameStart);
        while (ReadBlock(input, ref position, literals, sequences, output))
        {
        }

        CheckContent(input, ref position, descriptor, contentSize, output.Written[frameStart..]);
    }

    /// <summary>Checks the frame's content against its content size and, when the frame has one, its content checksum.</summary>
    private static void CheckContent(ReadOnlySpan<byte> input, ref int position, byte descriptor, ulong? contentSize, ReadOnlySpan<byte> content)
    {
        if (contentSize is ulong size && size != (ulong)content.Length)
        {
            throw DecompressionRefusedException.Malformed("the content is not the frame content size");
        }

        if ((descriptor & ContentChecksumFlag) != 0
            && BinaryPrimitives.ReadUInt32LittleEndian(Take(input, ref position, sizeof(uint))) != (uint)XxHash64.Hash(content))
        {
            throw new DecompressionRefusedException(DecompressionOutcome.ChecksumMismatch, "the content checksum does not match the content");
        }
    }

    /// <summary>
    /// Reads the frame header after the magic (RFC 8878, section 3.1.1.1): the descriptor, the window
    /// descriptor unless the frame is single-segment, the dictionary id and the content size. Gives
    /// the descriptor; a content size past what the output may still hold is refused at once.
    /// </summary>
    private static byte ReadHeader(ReadOnlySpan<byte> input, ref int position, BoundedDecompressionOutput output, out ulong? contentSize)
    {
        byte descriptor = Take(input, ref position, 1)[0];
        if ((descriptor & ReservedFlag) != 0)
        {
            throw DecompressionRefusedException.Malformed("the frame header's reserved bit is set");
        }

        bool singleSegment = (descriptor & SingleSegmentFlag) != 0;
        Take(input, ref position, singleSegment ? 0 : 1);
        if (LittleEndianValue.Read(Take(input, ref position, DictionaryIdLengths[descriptor & 3])) != 0)
        {
            throw DecompressionRefusedException.Malformed("the frame needs a dictionary");
        }

        int contentSizeLength = (descriptor >> 6) == 0 && singleSegment ? 1 : ContentSizeLengths[descriptor >> 6];
        contentSize = ReadContentSize(Take(input, ref position, contentSizeLength));
        if (contentSize > (ulong)output.Remaining)
        {
            throw new DecompressionRefusedException(DecompressionOutcome.OutputBoundPassed, "the frame content size passes the output bound");
        }

        return descriptor;
    }

    /// <summary>The content size a field gives, a 2-byte one counting from 256; none when the field is empty.</summary>
    private static ulong? ReadContentSize(ReadOnlySpan<byte> field) =>
        field.IsEmpty ? null : LittleEndianValue.Read(field) + (field.Length == 2 ? 256UL : 0UL);

    /// <summary>Reads one block into <paramref name="output"/>; true while it was not the last.</summary>
    private static bool ReadBlock(ReadOnlySpan<byte> input, ref int position, ZstdLiteralsSection literals, ZstdSequencesSection sequences, BoundedDecompressionOutput output)
    {
        uint header = (uint)LittleEndianValue.Read(Take(input, ref position, BlockHeaderLength));
        int type = (int)((header >> 1) & 3);
        int size = (int)(header >> 3);
        if (size > MaximumBlockSize)
        {
            throw DecompressionRefusedException.Malformed("a block is larger than 128 KiB");
        }

        ReadOnlySpan<byte> content = Take(input, ref position, type == RleBlock ? 1 : size);
        switch (type)
        {
            case RawBlock:
                output.Append(content);
                break;
            case RleBlock:
                byte[] run = new byte[size];
                Array.Fill(run, content[0]);
                output.Append(run);
                break;
            case CompressedBlock:
                byte[] blockLiterals = literals.Read(content, out int literalsLength);
                sequences.Execute(content[literalsLength..], blockLiterals, output);
                break;
            default:
                throw DecompressionRefusedException.Malformed("a block has the reserved block type");
        }

        return (header & 1) == 0;
    }

    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> input, ref int position, int count)
    {
        if (count > input.Length - position)
        {
            throw DecompressionRefusedException.Malformed("the input is truncated");
        }

        ReadOnlySpan<byte> taken = input.Slice(position, count);
        position += count;
        return taken;
    }
}
