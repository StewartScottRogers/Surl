namespace Surl.Qpack;

/// <summary>
/// Why a QPACK field section or instruction was refused. Which <see cref="QpackErrorCode" /> ends the
/// connection depends on where it arrived: a field section's failure is
/// <see cref="QpackErrorCode.DecompressionFailed" />, an encoder stream's
/// <see cref="QpackErrorCode.EncoderStreamError" />, a decoder stream's
/// <see cref="QpackErrorCode.DecoderStreamError" />. <see cref="FieldSectionTooLarge" /> is the one
/// exception: it is the <c>--max-message</c> limit, answered on its stream alone (ADR-0082 decision 7).
/// </summary>
public enum QpackDecodingError
{
    /// <summary>A field section ends inside a prefixed integer (RFC 7541 section 5.1).</summary>
    TruncatedInteger,

    /// <summary>A prefixed integer is longer than the 62 bits RFC 9204 section 4.1.1 requires.</summary>
    IntegerOverflow,

    /// <summary>A field section ends inside a string literal (RFC 9204 section 4.1.2).</summary>
    TruncatedString,

    /// <summary>A Huffman-coded string contains the EOS symbol (RFC 7541 section 5.2).</summary>
    HuffmanEndOfStringSymbol,

    /// <summary>A Huffman-coded string ends in more than seven bits of padding (RFC 7541 section 5.2).</summary>
    HuffmanPaddingLongerThanSevenBits,

    /// <summary>A Huffman-coded string ends in padding that is not all ones (RFC 7541 section 5.2).</summary>
    HuffmanPaddingNotAllOnes,

    /// <summary>A static table index is beyond the 99 entries of RFC 9204 Appendix A.</summary>
    StaticIndexOutsideTable,

    /// <summary>
    /// A dynamic table reference names an entry that was evicted, was never inserted, or - in a field
    /// section - has an absolute index at or above the section's Required Insert Count (RFC 9204
    /// section 2.2.3).
    /// </summary>
    DynamicReferenceOutsideTable,

    /// <summary>
    /// A field section's encoded Required Insert Count could not have come from a conformant encoder
    /// (RFC 9204 section 4.5.1.1); with a maximum table capacity of 0, any value but 0.
    /// </summary>
    InvalidRequiredInsertCount,

    /// <summary>
    /// A field section needs inserts the decoder has not received. The decoder holds no blocked
    /// streams (ADR-0082 decision 8 sends <c>SETTINGS_QPACK_BLOCKED_STREAMS</c> 0), so this is a
    /// failure, as RFC 9204 section 2.1.2 says.
    /// </summary>
    RequiredInsertCountNotReached,

    /// <summary>
    /// A field section's Required Insert Count is larger than the entries it references need
    /// (RFC 9204 section 2.2.1).
    /// </summary>
    RequiredInsertCountAboveReferences,

    /// <summary>A field section's Base is negative (RFC 9204 section 4.5.1.2).</summary>
    NegativeBase,

    /// <summary>
    /// The decoded field section is larger, counted as RFC 9114 section 4.2.2 counts it, than the
    /// decoder's bound: <c>SETTINGS_MAX_FIELD_SECTION_SIZE</c>, which is <c>--max-message</c>.
    /// </summary>
    FieldSectionTooLarge,

    /// <summary>
    /// A Set Dynamic Table Capacity instruction exceeds the maximum this end advertised (RFC 9204
    /// section 4.3.1).
    /// </summary>
    CapacityAboveMaximum,

    /// <summary>An insert would add an entry larger than the table's capacity (RFC 9204 section 3.2.2).</summary>
    EntryLargerThanCapacity,

    /// <summary>
    /// A Section Acknowledgment names a stream with no unacknowledged field section that references the
    /// dynamic table (RFC 9204 section 4.4.1).
    /// </summary>
    UnexpectedSectionAcknowledgment,

    /// <summary>
    /// An Insert Count Increment is zero or acknowledges inserts the encoder never sent (RFC 9204
    /// section 4.4.3).
    /// </summary>
    InvalidInsertCountIncrement,
}
