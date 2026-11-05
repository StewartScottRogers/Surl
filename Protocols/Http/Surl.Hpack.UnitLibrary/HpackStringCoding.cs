namespace Surl.Hpack;

/// <summary>When <see cref="HpackEncoder" /> Huffman-codes a string literal (RFC 7541 section 5.2).</summary>
public enum HpackStringCoding
{
    /// <summary>Huffman-code a string when that is strictly shorter than its raw bytes.</summary>
    WhenShorter,

    /// <summary>Huffman-code every string, as RFC 7541 Appendix C.4 and C.6 do.</summary>
    Huffman,

    /// <summary>Send every string as raw bytes, as RFC 7541 Appendix C.2, C.3 and C.5 do.</summary>
    Raw,
}
