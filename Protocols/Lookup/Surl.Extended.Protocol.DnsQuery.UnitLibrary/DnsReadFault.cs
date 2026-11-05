namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>Why <see cref="DnsMessageReader"/> could not read a message, or that it could.</summary>
public enum DnsReadFault
{
    /// <summary>The message was read whole.</summary>
    None,

    /// <summary>Fewer than 12 bytes: there is no header to read (ADR-0092 decision 2's "dropped").</summary>
    NoHeader,

    /// <summary>A count, a field or a name runs past the message's last byte.</summary>
    PastEndOfMessage,

    /// <summary>A length byte starts with the bits 01 or 10, which RFC 1035 section 4.1.4 reserves.</summary>
    ReservedLabelType,

    /// <summary>A name is longer than 255 bytes once its pointers are followed (RFC 1035 section 2.3.4).</summary>
    NameTooLong,

    /// <summary>A compression pointer names an offset at or past the message's end.</summary>
    CompressionPointerPastEndOfMessage,

    /// <summary>
    /// A compression pointer does not name an offset strictly before the labels it continues, so
    /// following it could loop.
    /// </summary>
    CompressionPointerNotBackwards,

    /// <summary>A record's RDATA does not hold its type's fields exactly.</summary>
    RecordDataMalformed,

    /// <summary>Bytes are left after the last record the counts announce.</summary>
    BytesAfterLastRecord,
}
