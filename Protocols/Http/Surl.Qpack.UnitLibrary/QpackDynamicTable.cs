namespace Surl.Qpack;

/// <summary>
/// The decoder's copy of a QPACK dynamic table (RFC 9204 section 3.2): entries in insertion order,
/// each with an absolute index fixed for its lifetime, evicted oldest first to keep the table within
/// the capacity the encoder set, which never exceeds the maximum this end advertised.
/// </summary>
/// <param name="maximumCapacity">The <c>SETTINGS_QPACK_MAX_TABLE_CAPACITY</c> this end advertised.</param>
internal sealed class QpackDynamicTable(int maximumCapacity)
{
    private readonly List<QpackFieldLine> entries = [];

    /// <summary>Gets the most the encoder may set the capacity to.</summary>
    public int MaximumCapacity { get; } = maximumCapacity;

    /// <summary>Gets the capacity the encoder last set; the table starts at zero (RFC 9204 section 3.2.2).</summary>
    public long Capacity { get; private set; }

    /// <summary>Gets the table's size: the sum of its entries' sizes (RFC 9204 section 3.2.1).</summary>
    public long Size { get; private set; }

    /// <summary>Gets the absolute index of the oldest entry still in the table: how many were evicted.</summary>
    public long EvictedCount { get; private set; }

    /// <summary>Gets the number of inserts and duplicates so far, which is the next absolute index.</summary>
    public long InsertCount => EvictedCount + entries.Count;

    /// <summary>Gets the entries, oldest first; the first has absolute index <see cref="EvictedCount" />.</summary>
    public IReadOnlyList<QpackFieldLine> Entries => entries;

    /// <summary>Sets the capacity and evicts entries until the table fits it (RFC 9204 section 4.3.1).</summary>
    /// <param name="capacity">The new capacity.</param>
    public void SetCapacity(long capacity)
    {
        QpackDecodingException.ThrowIf(capacity > MaximumCapacity, QpackDecodingError.CapacityAboveMaximum);
        Capacity = capacity;
        EvictUntilSizeAtMost(capacity);
    }

    /// <summary>Adds an entry, evicting the oldest until it fits (RFC 9204 section 3.2.2).</summary>
    /// <param name="entry">The entry.</param>
    public void Insert(QpackFieldLine entry)
    {
        QpackDecodingException.ThrowIf(entry.Size > Capacity, QpackDecodingError.EntryLargerThanCapacity);
        EvictUntilSizeAtMost(Capacity - entry.Size);
        entries.Add(entry);
        Size += entry.Size;
    }

    /// <summary>Gets the entry with absolute index <paramref name="absoluteIndex" />.</summary>
    /// <param name="absoluteIndex">The absolute index.</param>
    /// <returns>The entry.</returns>
    public QpackFieldLine GetAbsolute(long absoluteIndex)
    {
        QpackDecodingException.ThrowIf(
            absoluteIndex < EvictedCount || absoluteIndex >= InsertCount,
            QpackDecodingError.DynamicReferenceOutsideTable);
        return entries[(int)(absoluteIndex - EvictedCount)];
    }

    /// <summary>
    /// Gets the entry an encoder instruction's relative index names: 0 is the newest (RFC 9204
    /// section 3.2.5).
    /// </summary>
    /// <param name="relativeIndex">The relative index.</param>
    /// <returns>The entry.</returns>
    public QpackFieldLine GetRelativeToNewest(long relativeIndex) => GetAbsolute(InsertCount - 1 - relativeIndex);

    private void EvictUntilSizeAtMost(long size)
    {
        while (Size > size)
        {
            Size -= entries[0].Size;
            entries.RemoveAt(0);
            EvictedCount++;
        }
    }
}
