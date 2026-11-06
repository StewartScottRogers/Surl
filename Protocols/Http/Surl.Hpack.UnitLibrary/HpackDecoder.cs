namespace Surl.Hpack;

/// <summary>
/// Decodes HPACK header blocks (RFC 7541) into header lists, bounded as ADR-0081 decision 5 says:
/// no dynamic table size update above the size this end advertised, and no header list larger than
/// the <c>SETTINGS_MAX_HEADER_LIST_SIZE</c> it advertised, which is <c>--max-message</c>. One decoder
/// serves one direction of one connection: its dynamic table carries over from block to block, so
/// blocks must be decoded in the order they arrived. A block that fails leaves the table in an
/// unknown state, so the connection ends (RFC 9113 section 4.3).
/// </summary>
public sealed class HpackDecoder
{
    /// <summary>The dynamic table size HTTP/2 starts with (RFC 9113 section 6.5.2).</summary>
    public const int DefaultMaximumTableSize = 4096;

    private readonly HpackDynamicTable dynamicTable;

    private readonly int advertisedMaximumTableSize;

    private readonly int maximumHeaderListSize;

    /// <summary>Initializes a new instance of the <see cref="HpackDecoder" /> class.</summary>
    /// <param name="advertisedMaximumTableSize">
    /// The largest dynamic table the peer may use: the <c>SETTINGS_HEADER_TABLE_SIZE</c> this end
    /// advertised. The table starts at this size, and a size update above it is refused.
    /// </param>
    /// <param name="maximumHeaderListSize">
    /// The largest header list a block may decode to, counted as RFC 9113 section 6.5.2 counts it:
    /// the <c>SETTINGS_MAX_HEADER_LIST_SIZE</c> this end advertised.
    /// </param>
    public HpackDecoder(int advertisedMaximumTableSize, int maximumHeaderListSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(advertisedMaximumTableSize);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumHeaderListSize);
        this.advertisedMaximumTableSize = advertisedMaximumTableSize;
        this.maximumHeaderListSize = maximumHeaderListSize;
        dynamicTable = new HpackDynamicTable(advertisedMaximumTableSize);
    }

    /// <summary>Gets the dynamic table's current maximum size, as last set by the peer.</summary>
    public int MaximumTableSize => dynamicTable.MaximumSize;

    /// <summary>Gets the dynamic table's current size (RFC 7541 section 4.1).</summary>
    public int TableSize => dynamicTable.Size;

    /// <summary>Gets the dynamic table's entries, newest first.</summary>
    internal IReadOnlyList<HpackHeaderField> DynamicTableEntries => dynamicTable.NewestFirst;

    /// <summary>
    /// Decodes one complete header block: a <c>HEADERS</c> frame's fragment with all its
    /// <c>CONTINUATION</c> fragments appended. Never throws on malformed input.
    /// </summary>
    /// <param name="block">The header block.</param>
    /// <returns>The header list, or the rule the block broke.</returns>
    public HpackDecodeResult Decode(ReadOnlySpan<byte> block)
    {
        try
        {
            return new HpackDecodeResult(DecodeFields(block), null);
        }
        catch (HpackDecodingException exception)
        {
            return new HpackDecodeResult([], exception.Error);
        }
    }

    private List<HpackHeaderField> DecodeFields(ReadOnlySpan<byte> block)
    {
        var fields = new List<HpackHeaderField>();
        var position = 0;
        var headerListSize = 0L;
        ReadTableSizeUpdates(block, ref position);
        while (position < block.Length)
        {
            var field = ReadField(block, ref position);
            headerListSize += field.Size;
            HpackDecodingException.ThrowIf(
                headerListSize > maximumHeaderListSize, HpackDecodingError.HeaderListTooLarge);
            fields.Add(field);
        }

        return fields;
    }

    private void ReadTableSizeUpdates(ReadOnlySpan<byte> block, ref int position)
    {
        while (position < block.Length && IsTableSizeUpdate(block[position]))
        {
            var size = HpackPrimitives.ReadInteger(block, ref position, 5);
            HpackDecodingException.ThrowIf(
                size > advertisedMaximumTableSize, HpackDecodingError.TableSizeUpdateAboveLimit);
            dynamicTable.Resize(size);
        }
    }

    private HpackHeaderField ReadField(ReadOnlySpan<byte> block, ref int position)
    {
        var first = block[position];
        if ((first & 0x80) != 0)
        {
            return GetIndexed(HpackPrimitives.ReadInteger(block, ref position, 7));
        }

        if ((first & 0x40) != 0)
        {
            var field = ReadLiteral(block, ref position, 6, HpackFieldIndexing.IncrementalIndexing);
            dynamicTable.Add(field);
            return field;
        }

        HpackDecodingException.ThrowIf(IsTableSizeUpdate(first), HpackDecodingError.TableSizeUpdateAfterField);
        var indexing = (first & 0x10) != 0 ? HpackFieldIndexing.NeverIndexed : HpackFieldIndexing.WithoutIndexing;
        return ReadLiteral(block, ref position, 4, indexing);
    }

    private HpackHeaderField ReadLiteral(
        ReadOnlySpan<byte> block, ref int position, int prefixBits, HpackFieldIndexing indexing)
    {
        var nameIndex = HpackPrimitives.ReadInteger(block, ref position, prefixBits);
        var name = nameIndex == 0 ? HpackPrimitives.ReadString(block, ref position) : GetIndexed(nameIndex).Name;
        var value = HpackPrimitives.ReadString(block, ref position);
        return new HpackHeaderField(name, value, indexing);
    }

    private HpackHeaderField GetIndexed(int index)
    {
        HpackDecodingException.ThrowIf(
            index == 0 || index > HpackStaticTable.Count + dynamicTable.Count,
            HpackDecodingError.IndexOutsideBothTables);
        return index <= HpackStaticTable.Count
            ? HpackStaticTable.Get(index)
            : dynamicTable.Get(index - HpackStaticTable.Count);
    }

    private static bool IsTableSizeUpdate(byte first) => (first & 0xE0) == 0x20;
}
