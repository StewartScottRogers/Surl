using System.Buffers.Binary;

namespace Surl.Compression;

/// <summary>
/// Decodes one LZ4 frame (lz4's <c>doc/lz4_Frame_format.md</c>) whose blocks are in the LZ4 block
/// format (<c>doc/lz4_Block_format.md</c>): the magic <c>0x184D2204</c>, the descriptor and its
/// header checksum, data blocks (the high bit of a block size marks it stored uncompressed), the
/// end mark and the optional content checksum. Every checksum present is checked. Linked blocks
/// may copy from earlier blocks; independent blocks may not.
/// </summary>
public static class Lz4FrameDecoder
{
    /// <summary>The frame's magic number, little-endian on the wire.</summary>
    internal const uint MagicNumber = 0x184D2204;

    private const byte VersionMask = 0xC0;
    private const byte Version01 = 0x40;
    private const byte IndependentBlocksFlag = 0x20;
    private const byte BlockChecksumFlag = 0x10;
    private const byte ContentSizeFlag = 0x08;
    private const byte ContentChecksumFlag = 0x04;
    private const byte ReservedFlagBit = 0x02;
    private const byte DictionaryIdFlag = 0x01;
    private const byte BlockDescriptorReservedBits = 0x8F;
    private const int SmallestBlockMaximumCode = 4;
    private const uint StoredBlockBit = 0x80000000;
    private const int MinimumMatchLength = 4;
    private const int LengthNibbleExtended = 15;

    /// <summary>
    /// Decodes the frame in <paramref name="input"/>, writing at most <paramref name="outputBound"/>
    /// bytes: a content size or output past the bound is <see cref="DecompressionOutcome.OutputBoundPassed"/>,
    /// a checksum that disagrees is <see cref="DecompressionOutcome.ChecksumMismatch"/>, and anything
    /// else that breaks the format, bytes after the frame included, is <see cref="DecompressionOutcome.Malformed"/>.
    /// A dictionary id is refused as malformed, there being no dictionary to decode against.
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
        int position = 0;
        if (ReadUInt32(input, ref position) != MagicNumber)
        {
            throw DecompressionRefusedException.Malformed("the magic number is not an LZ4 frame's");
        }

        Lz4FrameDescriptor descriptor = ReadDescriptor(input, ref position);
        if (descriptor.ContentSize > (ulong)outputBound)
        {
            throw new DecompressionRefusedException(DecompressionOutcome.OutputBoundPassed, "the content size passes the output bound");
        }

        BoundedDecompressionOutput output = new(outputBound, DecompressionOutcome.OutputBoundPassed, "the blocks pass the output bound");
        while (ReadBlock(input, ref position, descriptor, output))
        {
        }

        CheckContent(input, ref position, descriptor, output.Written);
        return position == input.Length
            ? output.ToArray()
            : throw DecompressionRefusedException.Malformed("bytes follow the frame");
    }

    private static Lz4FrameDescriptor ReadDescriptor(ReadOnlySpan<byte> input, ref int position)
    {
        int start = position;
        byte flags = Take(input, ref position, 1)[0];
        byte blockDescriptor = Take(input, ref position, 1)[0];
        CheckDescriptorBits(flags, blockDescriptor);
        ulong? contentSize = (flags & ContentSizeFlag) != 0
            ? BinaryPrimitives.ReadUInt64LittleEndian(Take(input, ref position, sizeof(ulong)))
            : null;
        byte expectedHeaderChecksum = (byte)(XxHash32.Hash(input[start..position]) >> 8);
        if (Take(input, ref position, 1)[0] != expectedHeaderChecksum)
        {
            throw new DecompressionRefusedException(DecompressionOutcome.ChecksumMismatch, "the header checksum does not match the descriptor");
        }

        return new Lz4FrameDescriptor(
            (flags & IndependentBlocksFlag) != 0,
            (flags & BlockChecksumFlag) != 0,
            (flags & ContentChecksumFlag) != 0,
            contentSize,
            1 << (8 + (2 * ((blockDescriptor >> 4) & 7))));
    }

    private static void CheckDescriptorBits(byte flags, byte blockDescriptor)
    {
        if ((flags & VersionMask) != Version01)
        {
            throw DecompressionRefusedException.Malformed("the frame version is not 01");
        }

        if ((flags & ReservedFlagBit) != 0 || (blockDescriptor & BlockDescriptorReservedBits) != 0)
        {
            throw DecompressionRefusedException.Malformed("a reserved descriptor bit is set");
        }

        if ((flags & DictionaryIdFlag) != 0)
        {
            throw DecompressionRefusedException.Malformed("the frame needs a dictionary");
        }

        if (((blockDescriptor >> 4) & 7) < SmallestBlockMaximumCode)
        {
            throw DecompressionRefusedException.Malformed("the block maximum size code is not 4 to 7");
        }
    }

    /// <summary>Reads one block into <paramref name="output"/>; false at the end mark.</summary>
    private static bool ReadBlock(ReadOnlySpan<byte> input, ref int position, Lz4FrameDescriptor descriptor, BoundedDecompressionOutput output)
    {
        uint size = ReadUInt32(input, ref position);
        if (size == 0)
        {
            return false;
        }

        int length = (int)(size & ~StoredBlockBit);
        if (length > descriptor.BlockMaximumSize)
        {
            throw DecompressionRefusedException.Malformed("a block is larger than the block maximum size");
        }

        ReadOnlySpan<byte> block = Take(input, ref position, length);
        if (descriptor.BlockChecksums)
        {
            CheckChecksum(input, ref position, block, "a block checksum does not match its block");
        }

        int blockStart = output.Count;
        WriteBlock(block, (size & StoredBlockBit) != 0, descriptor.IndependentBlocks ? blockStart : 0, output);
        return output.Count - blockStart <= descriptor.BlockMaximumSize
            ? true
            : throw DecompressionRefusedException.Malformed("a block decodes to more than the block maximum size");
    }

    /// <summary>Appends a stored block as it is, or decodes a compressed one whose matches reach back no further than <paramref name="windowStart"/>.</summary>
    private static void WriteBlock(ReadOnlySpan<byte> block, bool stored, int windowStart, BoundedDecompressionOutput output)
    {
        if (stored)
        {
            output.Append(block);
        }
        else
        {
            DecodeBlock(block, output, windowStart);
        }
    }

    private static void CheckContent(ReadOnlySpan<byte> input, ref int position, Lz4FrameDescriptor descriptor, ReadOnlySpan<byte> content)
    {
        if (descriptor.ContentChecksum)
        {
            CheckChecksum(input, ref position, content, "the content checksum does not match the content");
        }

        if (descriptor.ContentSize is ulong size && size != (ulong)content.Length)
        {
            throw DecompressionRefusedException.Malformed("the content is not the content size");
        }
    }

    private static void CheckChecksum(ReadOnlySpan<byte> input, ref int position, ReadOnlySpan<byte> covered, string reason)
    {
        if (ReadUInt32(input, ref position) != XxHash32.Hash(covered))
        {
            throw new DecompressionRefusedException(DecompressionOutcome.ChecksumMismatch, reason);
        }
    }

    /// <summary>
    /// Decodes one compressed block: sequences of a token, literals and, unless it is the last, a
    /// 2-byte little-endian offset and the match length; matches reach back no further than
    /// <paramref name="windowStart"/>.
    /// </summary>
    private static void DecodeBlock(ReadOnlySpan<byte> block, BoundedDecompressionOutput output, int windowStart)
    {
        int position = 0;
        while (true)
        {
            byte token = Take(block, ref position, 1)[0];
            output.Append(Take(block, ref position, ReadLength(block, ref position, token >> 4)));
            if (position == block.Length)
            {
                return;
            }

            int offset = BinaryPrimitives.ReadUInt16LittleEndian(Take(block, ref position, sizeof(ushort)));
            output.CopyFromEarlier(offset, ReadLength(block, ref position, token & 0x0F) + MinimumMatchLength, windowStart);
        }
    }

    /// <summary>A length from its token nibble, extended by bytes while they are 255 when the nibble is 15.</summary>
    private static int ReadLength(ReadOnlySpan<byte> block, ref int position, int nibble)
    {
        int length = nibble;
        if (nibble == LengthNibbleExtended)
        {
            byte extension;
            do
            {
                extension = Take(block, ref position, 1)[0];
                length += extension;
            }
            while (extension == byte.MaxValue);
        }

        return length;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> input, ref int position) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Take(input, ref position, sizeof(uint)));

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
