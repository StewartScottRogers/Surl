namespace Surl.Hpack;

/// <summary>
/// Why an HPACK header block could not be decoded. On HTTP/2 each is a connection error of type
/// <c>COMPRESSION_ERROR</c> (RFC 9113 section 4.3), except <see cref="HeaderListTooLarge" />, which
/// is the <c>--max-message</c> limit (ADR-0081 decision 5).
/// </summary>
public enum HpackDecodingError
{
    /// <summary>The block ends inside a prefixed integer (RFC 7541 section 5.1).</summary>
    TruncatedInteger,

    /// <summary>
    /// A prefixed integer does not fit in 31 bits, or runs to more continuation bytes than one that
    /// does (RFC 7541 section 5.1).
    /// </summary>
    IntegerOverflow,

    /// <summary>A string literal's length runs past the end of the block (RFC 7541 section 5.2).</summary>
    TruncatedString,

    /// <summary>A Huffman-coded string contains the EOS symbol (RFC 7541 section 5.2).</summary>
    HuffmanEndOfStringSymbol,

    /// <summary>A Huffman-coded string ends in more than seven bits of padding (RFC 7541 section 5.2).</summary>
    HuffmanPaddingLongerThanSevenBits,

    /// <summary>
    /// A Huffman-coded string ends in padding that is not the most significant bits of the EOS code,
    /// all ones (RFC 7541 section 5.2).
    /// </summary>
    HuffmanPaddingNotAllOnes,

    /// <summary>An index is zero or beyond both the static and the dynamic table (RFC 7541 section 2.3.3).</summary>
    IndexOutsideBothTables,

    /// <summary>
    /// A dynamic table size update sets a size above the one the decoder advertised
    /// (RFC 7541 section 6.3).
    /// </summary>
    TableSizeUpdateAboveLimit,

    /// <summary>A dynamic table size update follows a header field in the block (RFC 7541 section 4.2).</summary>
    TableSizeUpdateAfterField,

    /// <summary>
    /// The decoded header list is larger, counted as RFC 9113 section 6.5.2 counts
    /// <c>SETTINGS_MAX_HEADER_LIST_SIZE</c>, than the decoder's bound.
    /// </summary>
    HeaderListTooLarge,
}
