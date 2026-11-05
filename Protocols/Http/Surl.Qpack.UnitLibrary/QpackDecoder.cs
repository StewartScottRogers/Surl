namespace Surl.Qpack;

/// <summary>
/// The QPACK decoder of one HTTP/3 connection (RFC 9204 section 2.2): it applies the peer's encoder
/// stream to its dynamic table, decodes the field sections that arrive on request streams, and writes
/// the decoder stream's instructions back. It holds no blocked streams: a field section that needs
/// inserts not yet received fails, because ADR-0082 decision 8 advertises
/// <c>SETTINGS_QPACK_BLOCKED_STREAMS</c> 0. A failure other than
/// <see cref="QpackDecodingError.FieldSectionTooLarge" /> ends the connection, so the decoder's state
/// after one is not defined.
/// </summary>
public sealed class QpackDecoder
{
    private readonly QpackDynamicTable dynamicTable;

    private readonly int maximumFieldSectionSize;

    private readonly QpackInstructionStream encoderStream;

    private readonly List<byte> pendingDecoderInstructions = [];

    private long acknowledgedInsertCount;

    /// <summary>Initializes a new instance of the <see cref="QpackDecoder" /> class.</summary>
    /// <param name="maximumTableCapacity">
    /// The <c>SETTINGS_QPACK_MAX_TABLE_CAPACITY</c> this end advertised: 0 for Surl's HTTP/3 server
    /// (ADR-0082 decision 8), so the peer can insert nothing.
    /// </param>
    /// <param name="maximumFieldSectionSize">
    /// The largest field section a section may decode to, counted as RFC 9114 section 4.2.2 counts it:
    /// the <c>SETTINGS_MAX_FIELD_SECTION_SIZE</c> this end advertised, which is <c>--max-message</c>.
    /// </param>
    public QpackDecoder(int maximumTableCapacity, int maximumFieldSectionSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTableCapacity);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumFieldSectionSize);
        dynamicTable = new QpackDynamicTable(maximumTableCapacity);
        this.maximumFieldSectionSize = maximumFieldSectionSize;
        encoderStream = new QpackInstructionStream(QpackErrorCode.EncoderStreamError, ReadEncoderInstruction);
    }

    /// <summary>Gets the dynamic table's capacity, as the peer's encoder last set it.</summary>
    public long TableCapacity => dynamicTable.Capacity;

    /// <summary>Gets the dynamic table's size (RFC 9204 section 3.2.1).</summary>
    public long TableSize => dynamicTable.Size;

    /// <summary>Gets the number of entries the peer has inserted or duplicated so far.</summary>
    public long InsertCount => dynamicTable.InsertCount;

    /// <summary>Gets the dynamic table's entries, oldest first.</summary>
    internal IReadOnlyList<QpackFieldLine> DynamicTableEntries => dynamicTable.Entries;

    /// <summary>Gets the absolute index of the oldest entry still in the dynamic table.</summary>
    internal long FirstAbsoluteIndex => dynamicTable.EvictedCount;

    /// <summary>Gets the number of bytes of an unfinished encoder instruction waiting for more.</summary>
    internal int UnconsumedEncoderStreamCount => encoderStream.UnconsumedCount;

    /// <summary>
    /// Applies the encoder instructions in bytes received on the peer's encoder stream (RFC 9204
    /// section 4.3). An instruction split across calls waits for the rest. Never throws on malformed
    /// input; a refusal is <see cref="QpackErrorCode.EncoderStreamError" />.
    /// </summary>
    /// <param name="bytes">The bytes the encoder stream delivered.</param>
    /// <returns>Whether the instructions were accepted, and if not, why.</returns>
    public QpackInstructionResult ReceiveEncoderStream(ReadOnlySpan<byte> bytes) => encoderStream.Receive(bytes);

    /// <summary>
    /// Decodes one complete encoded field section: a <c>HEADERS</c> frame's payload (RFC 9204 section
    /// 4.5). Never throws on malformed input.
    /// </summary>
    /// <param name="streamId">The request stream the section arrived on, for its Section Acknowledgment.</param>
    /// <param name="section">The encoded field section.</param>
    /// <returns>The field lines, or the rule the section broke.</returns>
    public QpackFieldSectionResult DecodeFieldSection(long streamId, ReadOnlySpan<byte> section)
    {
        try
        {
            var fieldSection = new QpackFieldSectionReader(dynamicTable, maximumFieldSectionSize).Read(section);
            AcknowledgeSection(streamId, fieldSection.RequiredInsertCount);
            return new QpackFieldSectionResult(fieldSection.FieldLines, null);
        }
        catch (QpackDecodingException exception)
        {
            return new QpackFieldSectionResult([], exception.Error);
        }
    }

    /// <summary>
    /// Records that a request stream was reset or abandoned before its field sections were decoded, so
    /// the decoder stream tells the peer (RFC 9204 section 4.4.2). With a maximum table capacity of 0
    /// nothing is sent, as RFC 9204 section 2.2.2.2 allows.
    /// </summary>
    /// <param name="streamId">The stream.</param>
    public void CancelStream(long streamId)
    {
        if (dynamicTable.MaximumCapacity > 0)
        {
            QpackPrimitives.WriteInteger(pendingDecoderInstructions, streamId, 6, 0x40);
        }
    }

    /// <summary>
    /// Takes the decoder instructions to send on this end's decoder stream (RFC 9204 section 4.4): the
    /// Section Acknowledgments and Stream Cancellations in order, then one Insert Count Increment for
    /// the inserts no acknowledgment has covered yet.
    /// </summary>
    /// <returns>The bytes, empty when there is nothing to send.</returns>
    public byte[] TakeDecoderStreamInstructions()
    {
        if (dynamicTable.InsertCount > acknowledgedInsertCount)
        {
            QpackPrimitives.WriteInteger(
                pendingDecoderInstructions, dynamicTable.InsertCount - acknowledgedInsertCount, 6, 0x00);
            acknowledgedInsertCount = dynamicTable.InsertCount;
        }

        var instructions = pendingDecoderInstructions.ToArray();
        pendingDecoderInstructions.Clear();
        return instructions;
    }

    private void AcknowledgeSection(long streamId, long requiredInsertCount)
    {
        if (requiredInsertCount > 0)
        {
            QpackPrimitives.WriteInteger(pendingDecoderInstructions, streamId, 7, 0x80);
            acknowledgedInsertCount = Math.Max(acknowledgedInsertCount, requiredInsertCount);
        }
    }

    private int ReadEncoderInstruction(ReadOnlySpan<byte> bytes, int position)
    {
        var first = bytes[position];
        if ((first & 0x80) != 0)
        {
            return ReadInsertWithNameReference(bytes, position, isStatic: (first & 0x40) != 0);
        }

        if ((first & 0x40) != 0)
        {
            return ReadInsertWithLiteralName(bytes, position);
        }

        var value = QpackPrimitives.ReadInteger(bytes, ref position, 5);
        if ((first & 0x20) != 0)
        {
            dynamicTable.SetCapacity(value);
        }
        else
        {
            dynamicTable.Insert(dynamicTable.GetRelativeToNewest(value));
        }

        return position;
    }

    private int ReadInsertWithNameReference(ReadOnlySpan<byte> bytes, int position, bool isStatic)
    {
        var nameIndex = QpackPrimitives.ReadInteger(bytes, ref position, 6);
        var name = isStatic
            ? QpackStaticTable.Get(nameIndex).Name
            : dynamicTable.GetRelativeToNewest(nameIndex).Name;
        var value = QpackPrimitives.ReadString(bytes, ref position, 8, LongestValueThatFits(name));
        dynamicTable.Insert(new QpackFieldLine(name, value));
        return position;
    }

    private int ReadInsertWithLiteralName(ReadOnlySpan<byte> bytes, int position)
    {
        var name = QpackPrimitives.ReadString(bytes, ref position, 6, LongestValueThatFits(string.Empty));
        var value = QpackPrimitives.ReadString(bytes, ref position, 8, LongestValueThatFits(name));
        dynamicTable.Insert(new QpackFieldLine(name, value));
        return position;
    }

    private long LongestValueThatFits(string name) => dynamicTable.Capacity - QpackFieldLine.EntryOverhead - name.Length;
}
