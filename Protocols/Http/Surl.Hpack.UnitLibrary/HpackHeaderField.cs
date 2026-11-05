namespace Surl.Hpack;

/// <summary>
/// One header field of an HPACK header list (RFC 7541 section 1.3): a name, a value, and how it
/// travels as a literal. Names and values are byte strings; each character stands for one byte
/// (Latin-1), so every byte a peer sends survives a decode and an encode unchanged.
/// </summary>
/// <param name="Name">The field name.</param>
/// <param name="Value">The field value.</param>
/// <param name="Indexing">How the field travels, or travelled, as a literal.</param>
public readonly record struct HpackHeaderField(
    string Name,
    string Value,
    HpackFieldIndexing Indexing = HpackFieldIndexing.IncrementalIndexing)
{
    /// <summary>The overhead RFC 7541 section 4.1 adds to an entry's name and value lengths.</summary>
    public const int EntryOverhead = 32;

    /// <summary>
    /// Gets the field's size as RFC 7541 section 4.1 counts it for the dynamic table, and RFC 9113
    /// section 6.5.2 for a header list: the name's and the value's lengths in bytes plus 32.
    /// </summary>
    public int Size => Name.Length + Value.Length + EntryOverhead;
}
