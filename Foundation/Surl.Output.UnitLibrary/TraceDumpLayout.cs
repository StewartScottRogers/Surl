namespace Surl.Output;

/// <summary>
/// How the trace dump lays out each event's bytes (ADR-0033, section 4).
/// </summary>
public enum TraceDumpLayout
{
    /// <summary>
    /// <c>--trace</c>: rows of sixteen bytes, as hex and then as characters.
    /// </summary>
    HexAndAscii,

    /// <summary>
    /// <c>--trace-ascii</c>: rows of at most 64 bytes as characters, each ending early after
    /// a CR LF pair, which is not printed.
    /// </summary>
    Ascii,
}
