namespace Surl.Hpack;

/// <summary>
/// How a header field travels in a header block when it is sent as a literal (RFC 7541
/// section 6.2): whether the receiver adds it to its dynamic table.
/// </summary>
public enum HpackFieldIndexing
{
    /// <summary>
    /// A literal with incremental indexing (RFC 7541 section 6.2.1): the receiver adds the field
    /// to its dynamic table. A field decoded from an index carries this value too.
    /// </summary>
    IncrementalIndexing,

    /// <summary>A literal without indexing (RFC 7541 section 6.2.2): the dynamic table is left alone.</summary>
    WithoutIndexing,

    /// <summary>
    /// A literal never indexed (RFC 7541 section 6.2.3): no table holds it and every
    /// intermediary forwards it the same way. The encoder never sends such a field as an index.
    /// </summary>
    NeverIndexed,
}
