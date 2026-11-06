namespace Surl.Hpack;

/// <summary>
/// Encodes header lists into HPACK header blocks (RFC 7541). One encoder serves one direction of one
/// connection: its dynamic table carries over from block to block, so blocks must be sent in the
/// order they were encoded.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A field whose name and value match a table entry is sent as that index - the static table
/// first, then the newest dynamic entry - unless it is <see cref="HpackFieldIndexing.NeverIndexed" />.</item>
/// <item>Any other field is a literal of the kind its <see cref="HpackHeaderField.Indexing" /> names,
/// and only <see cref="HpackFieldIndexing.IncrementalIndexing" /> adds it to the table.</item>
/// <item>A literal names its field by index when a table has the name: the first static entry with
/// it, otherwise the newest dynamic one.</item>
/// <item>A string is Huffman-coded as the encoder's <see cref="HpackStringCoding" /> says.</item>
/// </list>
/// These are the choices RFC 7541 Appendix C makes, so its examples encode to its bytes.
/// </remarks>
public sealed class HpackEncoder
{
    /// <summary>The dynamic table size HTTP/2 starts with (RFC 9113 section 6.5.2).</summary>
    public const int DefaultMaximumTableSize = 4096;

    private const byte IndexedFlags = 0x80;

    private const byte IncrementalIndexingFlags = 0x40;

    private const byte WithoutIndexingFlags = 0x00;

    private const byte NeverIndexedFlags = 0x10;

    private const byte TableSizeUpdateFlags = 0x20;

    private readonly HpackDynamicTable dynamicTable;

    private readonly int largestMaximumTableSize;

    private readonly HpackStringCoding stringCoding;

    /// <summary>The smallest size the table passed through since the last block, if it changed.</summary>
    private int smallestMaximumTableSizeSinceLastBlock = int.MaxValue;

    private bool isTableSizeUpdatePending;

    /// <summary>Initializes a new instance of the <see cref="HpackEncoder" /> class.</summary>
    /// <param name="maximumTableSize">
    /// The largest dynamic table this encoder will use, whatever the peer allows. The table starts at
    /// this size, which the peer must allow: HTTP/2's initial 4096 does.
    /// </param>
    /// <param name="stringCoding">When to Huffman-code a string literal.</param>
    public HpackEncoder(
        int maximumTableSize = DefaultMaximumTableSize,
        HpackStringCoding stringCoding = HpackStringCoding.WhenShorter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTableSize);
        largestMaximumTableSize = maximumTableSize;
        this.stringCoding = stringCoding;
        dynamicTable = new HpackDynamicTable(maximumTableSize);
    }

    /// <summary>Gets the dynamic table's current maximum size.</summary>
    public int MaximumTableSize => dynamicTable.MaximumSize;

    /// <summary>Gets the dynamic table's current size (RFC 7541 section 4.1).</summary>
    public int TableSize => dynamicTable.Size;

    /// <summary>Gets the dynamic table's entries, newest first.</summary>
    internal IReadOnlyList<HpackHeaderField> DynamicTableEntries => dynamicTable.NewestFirst;

    /// <summary>
    /// Takes a new <c>SETTINGS_HEADER_TABLE_SIZE</c> from the peer. The table's maximum becomes the
    /// smaller of it and the encoder's own limit at once, and the next block opens with the size
    /// updates that tell the peer: the smallest size passed through, when that is below the final
    /// one, then the final one (RFC 7541 section 4.2).
    /// </summary>
    /// <param name="peerMaximumTableSize">The peer's <c>SETTINGS_HEADER_TABLE_SIZE</c>.</param>
    public void SetPeerMaximumTableSize(int peerMaximumTableSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(peerMaximumTableSize);
        var size = Math.Min(peerMaximumTableSize, largestMaximumTableSize);
        dynamicTable.Resize(size);
        smallestMaximumTableSizeSinceLastBlock = Math.Min(smallestMaximumTableSizeSinceLastBlock, size);
        isTableSizeUpdatePending = true;
    }

    /// <summary>Encodes one header list into a header block.</summary>
    /// <param name="fields">The header list, in order.</param>
    /// <returns>The header block.</returns>
    public byte[] Encode(IEnumerable<HpackHeaderField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var output = new List<byte>();
        WritePendingTableSizeUpdates(output);
        foreach (var field in fields)
        {
            WriteField(output, field);
        }

        return [.. output];
    }

    private void WritePendingTableSizeUpdates(List<byte> output)
    {
        if (!isTableSizeUpdatePending)
        {
            return;
        }

        if (smallestMaximumTableSizeSinceLastBlock < dynamicTable.MaximumSize)
        {
            HpackPrimitives.WriteInteger(output, smallestMaximumTableSizeSinceLastBlock, 5, TableSizeUpdateFlags);
        }

        HpackPrimitives.WriteInteger(output, dynamicTable.MaximumSize, 5, TableSizeUpdateFlags);
        isTableSizeUpdatePending = false;
        smallestMaximumTableSizeSinceLastBlock = int.MaxValue;
    }

    private void WriteField(List<byte> output, HpackHeaderField field)
    {
        var (index, isExact) = Find(field, nameOnly: field.Indexing == HpackFieldIndexing.NeverIndexed);
        if (isExact)
        {
            HpackPrimitives.WriteInteger(output, index, 7, IndexedFlags);
            return;
        }

        var (prefixBits, flags) = LiteralPrefix(field.Indexing);
        if (field.Indexing == HpackFieldIndexing.IncrementalIndexing)
        {
            dynamicTable.Add(field);
        }

        HpackPrimitives.WriteInteger(output, index, prefixBits, flags);
        if (index == 0)
        {
            HpackPrimitives.WriteString(output, field.Name, stringCoding);
        }

        HpackPrimitives.WriteString(output, field.Value, stringCoding);
    }

    private static (int PrefixBits, byte Flags) LiteralPrefix(HpackFieldIndexing indexing) => indexing switch
    {
        HpackFieldIndexing.IncrementalIndexing => (6, IncrementalIndexingFlags),
        HpackFieldIndexing.NeverIndexed => (4, NeverIndexedFlags),
        _ => (4, WithoutIndexingFlags),
    };

    private (int Index, bool IsExact) Find(HpackHeaderField field, bool nameOnly)
    {
        var (staticIndex, isStaticExact) = HpackStaticTable.Find(field.Name, field.Value, nameOnly);
        if (isStaticExact)
        {
            return (staticIndex, true);
        }

        var (dynamicIndex, isDynamicExact) = dynamicTable.Find(field.Name, field.Value, nameOnly);
        if (isDynamicExact || (staticIndex == 0 && dynamicIndex != 0))
        {
            return (HpackStaticTable.Count + dynamicIndex, isDynamicExact);
        }

        return (staticIndex, false);
    }
}
