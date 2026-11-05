namespace Surl.Compression;

/// <summary>
/// Reads the literals section that starts a zstd compressed block (RFC 8878, section 3.1.1.3.1):
/// Raw, RLE, Compressed (a Huffman tree description, then one or four streams) or Treeless, which
/// reuses the Huffman table of the frame's last Compressed literals. One instance serves one frame,
/// so that a Treeless section finds the table.
/// </summary>
internal sealed class ZstdLiteralsSection
{
    /// <summary>The most literals one block may hold, Block_Maximum_Size (RFC 8878, section 3.1.1.2.3).</summary>
    internal const int MaximumLiterals = 128 * 1024;

    private const int RawType = 0;
    private const int RleType = 1;
    private const int CompressedType = 2;
    private const int JumpTableLength = 6;
    private const int StreamCount = 4;

    /// <summary>The header's length for Raw and RLE literals, by size format.</summary>
    private static readonly int[] UncompressedHeaderLengths = [1, 2, 1, 3];

    /// <summary>The width of each of the two sizes in a Compressed or Treeless header, by size format.</summary>
    private static readonly int[] CompressedSizeBits = [10, 10, 14, 18];

    /// <summary>The header's length for Compressed and Treeless literals, by size format.</summary>
    private static readonly int[] CompressedHeaderLengths = [3, 3, 4, 5];

    private ZstdHuffmanTable? previousTable;

    /// <summary>
    /// The literals of the section at the start of <paramref name="block"/>; <paramref name="bytesRead"/>
    /// is the section's length.
    /// </summary>
    public byte[] Read(ReadOnlySpan<byte> block, out int bytesRead)
    {
        if (block.IsEmpty)
        {
            throw DecompressionRefusedException.Malformed("the literals section is missing");
        }

        int type = block[0] & 3;
        int sizeFormat = (block[0] >> 2) & 3;
        return type <= RleType
            ? ReadUncompressed(block, type, sizeFormat, out bytesRead)
            : ReadCompressed(block, type, sizeFormat, out bytesRead);
    }

    private static byte[] ReadUncompressed(ReadOnlySpan<byte> block, int type, int sizeFormat, out int bytesRead)
    {
        int headerLength = UncompressedHeaderLengths[sizeFormat];
        int size = (int)(LittleEndianValue.Read(Take(block, 0, headerLength)) >> (headerLength == 1 ? 3 : 4));
        CheckSize(size);
        ReadOnlySpan<byte> payload = Take(block, headerLength, type == RawType ? size : 1);
        bytesRead = headerLength + payload.Length;
        if (type == RawType)
        {
            return payload.ToArray();
        }

        byte[] repeated = new byte[size];
        Array.Fill(repeated, payload[0]);
        return repeated;
    }

    private byte[] ReadCompressed(ReadOnlySpan<byte> block, int type, int sizeFormat, out int bytesRead)
    {
        int sizeBits = CompressedSizeBits[sizeFormat];
        int headerLength = CompressedHeaderLengths[sizeFormat];
        ulong sizes = LittleEndianValue.Read(Take(block, 0, headerLength)) >> 4;
        ulong mask = (1UL << sizeBits) - 1;
        int regeneratedSize = (int)(sizes & mask);
        CheckSize(regeneratedSize);
        ReadOnlySpan<byte> payload = Take(block, headerLength, (int)((sizes >> sizeBits) & mask));
        bytesRead = headerLength + payload.Length;
        ZstdHuffmanTable table = SelectTable(type, ref payload);
        byte[] literals = new byte[regeneratedSize];
        if (sizeFormat == 0)
        {
            DecodeStream(table, payload, literals);
        }
        else
        {
            DecodeFourStreams(table, payload, literals);
        }

        return literals;
    }

    /// <summary>Reads the tree at the start of <paramref name="payload"/> unless the literals are Treeless, and gives the table to decode with.</summary>
    private ZstdHuffmanTable SelectTable(int type, ref ReadOnlySpan<byte> payload)
    {
        if (type == CompressedType)
        {
            previousTable = ZstdHuffmanTable.Read(payload, out int treeBytes);
            payload = payload[treeBytes..];
        }

        return previousTable ?? throw DecompressionRefusedException.Malformed("Treeless literals have no earlier Huffman table in the frame");
    }

    /// <summary>Decodes four streams after a jump table of the first three's sizes, each but the last regenerating a quarter, rounded up.</summary>
    private static void DecodeFourStreams(ZstdHuffmanTable table, ReadOnlySpan<byte> payload, byte[] literals)
    {
        ReadOnlySpan<byte> jumpTable = Take(payload, 0, JumpTableLength);
        int share = (literals.Length + 3) / 4;
        if (share * 3 > literals.Length)
        {
            throw DecompressionRefusedException.Malformed("too few literals for four streams");
        }

        int streamStart = JumpTableLength;
        for (int stream = 0; stream < StreamCount; stream++)
        {
            int streamLength = stream < StreamCount - 1
                ? (int)LittleEndianValue.Read(jumpTable.Slice(stream * 2, 2))
                : payload.Length - streamStart;
            int regenerated = stream < StreamCount - 1 ? share : literals.Length - (3 * share);
            DecodeStream(table, Take(payload, streamStart, streamLength), literals.AsSpan(stream * share, regenerated));
            streamStart += streamLength;
        }
    }

    private static void DecodeStream(ZstdHuffmanTable table, ReadOnlySpan<byte> stream, Span<byte> destination)
    {
        ZstdBackwardBitReader reader = new(stream);
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = table.DecodeSymbol(reader);
        }

        if (reader.RemainingBits != 0)
        {
            throw DecompressionRefusedException.Malformed("a literals stream does not end where its symbols do");
        }
    }

    private static void CheckSize(int size)
    {
        if (size > MaximumLiterals)
        {
            throw DecompressionRefusedException.Malformed("a block holds more than 128 KiB of literals");
        }
    }

    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> source, int start, int count)
    {
        if (count > source.Length - start)
        {
            throw DecompressionRefusedException.Malformed("the literals section is truncated");
        }

        return source.Slice(start, count);
    }
}
