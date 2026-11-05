namespace Surl.Qpack;

/// <summary>
/// One field line of a QPACK field section (RFC 9204 section 1.1), and one entry of its dynamic table:
/// a name, a value, and whether it must stay a literal. Names and values are byte strings; each
/// character stands for one byte (Latin-1), so every byte a peer sends survives a decode and an encode
/// unchanged.
/// </summary>
/// <param name="Name">The field name.</param>
/// <param name="Value">The field value.</param>
/// <param name="IsNeverIndexed">
/// Whether the line travels, or travelled, as a literal with the N bit set (RFC 9204 section 4.5.4):
/// an intermediary must keep it a literal, and <see cref="QpackEncoder" /> never sends it indexed.
/// </param>
public readonly record struct QpackFieldLine(string Name, string Value, bool IsNeverIndexed = false)
{
    /// <summary>The overhead RFC 9204 section 3.2.1 adds to an entry's name and value lengths.</summary>
    public const int EntryOverhead = 32;

    /// <summary>
    /// Gets the line's size as RFC 9204 section 3.2.1 counts it for the dynamic table, and RFC 9114
    /// section 4.2.2 for a field section: the name's and the value's lengths in bytes plus 32.
    /// </summary>
    public int Size => Name.Length + Value.Length + EntryOverhead;
}
