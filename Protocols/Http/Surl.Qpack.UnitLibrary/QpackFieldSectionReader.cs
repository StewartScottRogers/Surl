namespace Surl.Qpack;

/// <summary>
/// Reads one encoded field section (RFC 9204 section 4.5) against a dynamic table it never changes:
/// the prefix, then each field line representation. One reader reads one section.
/// </summary>
/// <param name="dynamicTable">The decoder's dynamic table.</param>
/// <param name="maximumFieldSectionSize">The largest field section, counted as RFC 9114 section 4.2.2 counts it.</param>
internal sealed class QpackFieldSectionReader(QpackDynamicTable dynamicTable, int maximumFieldSectionSize)
{
    /// <summary>The longest string a field line may carry; the section's own length bounds it first.</summary>
    private const long AnyStringLength = int.MaxValue;

    private readonly List<QpackFieldLine> fieldLines = [];

    private long requiredInsertCount;

    private long baseIndex;

    private long largestReferencedIndex = -1;

    private long fieldSectionSize;

    /// <summary>Reads the section.</summary>
    /// <param name="section">The encoded field section.</param>
    /// <returns>The field lines and the section's Required Insert Count.</returns>
    public (IReadOnlyList<QpackFieldLine> FieldLines, long RequiredInsertCount) Read(ReadOnlySpan<byte> section)
    {
        var position = 0;
        ReadPrefix(section, ref position);
        while (position < section.Length)
        {
            var line = ReadFieldLine(section, ref position);
            fieldSectionSize += line.Size;
            QpackDecodingException.ThrowIf(
                fieldSectionSize > maximumFieldSectionSize, QpackDecodingError.FieldSectionTooLarge);
            fieldLines.Add(line);
        }

        QpackDecodingException.ThrowIf(
            requiredInsertCount != largestReferencedIndex + 1, QpackDecodingError.RequiredInsertCountAboveReferences);
        return (fieldLines, requiredInsertCount);
    }

    /// <summary>
    /// Reconstructs a Required Insert Count from its encoded value, as RFC 9204 section 4.5.1.1 does.
    /// </summary>
    /// <param name="encodedInsertCount">The encoded value.</param>
    /// <param name="maximumTableCapacity">The <c>SETTINGS_QPACK_MAX_TABLE_CAPACITY</c> this end advertised.</param>
    /// <param name="totalInserts">The inserts the decoder has received.</param>
    /// <returns>The Required Insert Count.</returns>
    internal static long ReconstructRequiredInsertCount(
        long encodedInsertCount, int maximumTableCapacity, long totalInserts)
    {
        if (encodedInsertCount == 0)
        {
            return 0;
        }

        long maximumEntries = maximumTableCapacity / QpackFieldLine.EntryOverhead;
        var fullRange = 2 * maximumEntries;
        QpackDecodingException.ThrowIf(encodedInsertCount > fullRange, QpackDecodingError.InvalidRequiredInsertCount);
        var maximumValue = totalInserts + maximumEntries;
        var maximumWrapped = maximumValue / fullRange * fullRange;
        var count = maximumWrapped + encodedInsertCount - 1;
        if (count > maximumValue)
        {
            QpackDecodingException.ThrowIf(count <= fullRange, QpackDecodingError.InvalidRequiredInsertCount);
            count -= fullRange;
        }

        QpackDecodingException.ThrowIf(count == 0, QpackDecodingError.InvalidRequiredInsertCount);
        return count;
    }

    private void ReadPrefix(ReadOnlySpan<byte> section, ref int position)
    {
        var encodedInsertCount = QpackPrimitives.ReadInteger(section, ref position, 8);
        requiredInsertCount = ReconstructRequiredInsertCount(
            encodedInsertCount, dynamicTable.MaximumCapacity, dynamicTable.InsertCount);
        QpackDecodingException.ThrowIf(
            requiredInsertCount > dynamicTable.InsertCount, QpackDecodingError.RequiredInsertCountNotReached);
        var isBaseBelowRequiredInsertCount = (QpackPrimitives.PeekByte(section, position) & 0x80) != 0;
        var deltaBase = QpackPrimitives.ReadInteger(section, ref position, 7);
        QpackDecodingException.ThrowIf(
            isBaseBelowRequiredInsertCount && requiredInsertCount <= deltaBase, QpackDecodingError.NegativeBase);
        baseIndex = isBaseBelowRequiredInsertCount
            ? requiredInsertCount - deltaBase - 1
            : requiredInsertCount + deltaBase;
    }

    private QpackFieldLine ReadFieldLine(ReadOnlySpan<byte> section, ref int position)
    {
        var first = section[position];
        if ((first & 0x80) != 0)
        {
            var index = QpackPrimitives.ReadInteger(section, ref position, 6);
            return (first & 0x40) != 0 ? QpackStaticTable.Get(index) : GetReferenced(baseIndex - 1 - index);
        }

        if ((first & 0x40) != 0)
        {
            return ReadLiteralWithNameReference(section, ref position, first);
        }

        if ((first & 0x20) != 0)
        {
            return ReadLiteralWithLiteralName(section, ref position, first);
        }

        return (first & 0x10) != 0
            ? GetReferenced(baseIndex + QpackPrimitives.ReadInteger(section, ref position, 4))
            : ReadLiteralWithPostBaseNameReference(section, ref position, first);
    }

    private QpackFieldLine ReadLiteralWithNameReference(ReadOnlySpan<byte> section, ref int position, byte first)
    {
        var nameIndex = QpackPrimitives.ReadInteger(section, ref position, 4);
        var name = (first & 0x10) != 0
            ? QpackStaticTable.Get(nameIndex).Name
            : GetReferenced(baseIndex - 1 - nameIndex).Name;
        var value = QpackPrimitives.ReadString(section, ref position, 8, AnyStringLength);
        return new QpackFieldLine(name, value, IsNeverIndexed: (first & 0x20) != 0);
    }

    private QpackFieldLine ReadLiteralWithPostBaseNameReference(ReadOnlySpan<byte> section, ref int position, byte first)
    {
        var name = GetReferenced(baseIndex + QpackPrimitives.ReadInteger(section, ref position, 3)).Name;
        var value = QpackPrimitives.ReadString(section, ref position, 8, AnyStringLength);
        return new QpackFieldLine(name, value, IsNeverIndexed: (first & 0x08) != 0);
    }

    private static QpackFieldLine ReadLiteralWithLiteralName(ReadOnlySpan<byte> section, ref int position, byte first)
    {
        var name = QpackPrimitives.ReadString(section, ref position, 4, AnyStringLength);
        var value = QpackPrimitives.ReadString(section, ref position, 8, AnyStringLength);
        return new QpackFieldLine(name, value, IsNeverIndexed: (first & 0x10) != 0);
    }

    private QpackFieldLine GetReferenced(long absoluteIndex)
    {
        QpackDecodingException.ThrowIf(
            absoluteIndex < 0 || absoluteIndex >= requiredInsertCount, QpackDecodingError.DynamicReferenceOutsideTable);
        var entry = dynamicTable.GetAbsolute(absoluteIndex);
        largestReferencedIndex = Math.Max(largestReferencedIndex, absoluteIndex);
        return entry;
    }
}
