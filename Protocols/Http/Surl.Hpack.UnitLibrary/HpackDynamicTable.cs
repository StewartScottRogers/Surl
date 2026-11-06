namespace Surl.Hpack;

/// <summary>
/// An HPACK dynamic table (RFC 7541 section 2.3.2): a first-in, first-out list of header fields,
/// newest first, bounded in size, with entries evicted from the oldest end.
/// </summary>
/// <param name="maximumSize">The table's starting maximum size in bytes.</param>
internal sealed class HpackDynamicTable(int maximumSize)
{
    /// <summary>Entries, oldest first, so the newest is last.</summary>
    private readonly List<HpackHeaderField> entries = [];

    /// <summary>Gets the number of entries.</summary>
    public int Count => entries.Count;

    /// <summary>Gets the sum of the entries' sizes (RFC 7541 section 4.1).</summary>
    public int Size { get; private set; }

    /// <summary>Gets the maximum size, which <see cref="Size" /> never exceeds.</summary>
    public int MaximumSize { get; private set; } = maximumSize;

    /// <summary>Gets the entries newest first, as the dynamic indexes number them.</summary>
    public IReadOnlyList<HpackHeaderField> NewestFirst => [.. Enumerable.Reverse(entries)];

    /// <summary>Gets the entry at a 1-based dynamic index, where 1 is the newest.</summary>
    /// <param name="dynamicIndex">1 to <see cref="Count" />.</param>
    /// <returns>The entry.</returns>
    public HpackHeaderField Get(int dynamicIndex) => entries[entries.Count - dynamicIndex];

    /// <summary>
    /// Adds an entry as the newest, evicting the oldest until it fits (RFC 7541 section 4.4). An
    /// entry larger than the maximum size empties the table and is not added.
    /// </summary>
    /// <param name="field">The entry; its <see cref="HpackHeaderField.Indexing" /> is not kept.</param>
    public void Add(HpackHeaderField field)
    {
        var size = field.Size;
        EvictUntilSizeIsAtMost(MaximumSize - size);
        if (size <= MaximumSize)
        {
            entries.Add(new HpackHeaderField(field.Name, field.Value));
            Size += size;
        }
    }

    /// <summary>
    /// Changes the maximum size, evicting the oldest entries until the table fits
    /// (RFC 7541 section 4.3).
    /// </summary>
    /// <param name="newMaximumSize">The new maximum size in bytes.</param>
    public void Resize(int newMaximumSize)
    {
        MaximumSize = newMaximumSize;
        EvictUntilSizeIsAtMost(newMaximumSize);
    }

    /// <summary>
    /// Finds a field: the newest entry whose name and value both match, otherwise the newest entry
    /// with the name.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The field value.</param>
    /// <param name="nameOnly">Match names only, never name and value.</param>
    /// <returns>The 1-based dynamic index and whether the value matched too, or index 0 when no entry has the name.</returns>
    public (int Index, bool IsExact) Find(string name, string value, bool nameOnly)
    {
        var newestWithName = 0;
        for (var dynamicIndex = 1; dynamicIndex <= entries.Count; dynamicIndex++)
        {
            var entry = Get(dynamicIndex);
            if (entry.Name != name)
            {
                continue;
            }

            if (!nameOnly && entry.Value == value)
            {
                return (dynamicIndex, true);
            }

            newestWithName = newestWithName == 0 ? dynamicIndex : newestWithName;
        }

        return (newestWithName, false);
    }

    private void EvictUntilSizeIsAtMost(int size)
    {
        while (entries.Count > 0 && Size > size)
        {
            Size -= entries[0].Size;
            entries.RemoveAt(0);
        }
    }
}
