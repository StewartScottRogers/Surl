namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A whole DNS message (RFC 1035 section 4.1): the header and its four sections, whose counts
/// are the lengths of the lists.
/// </summary>
/// <param name="Header">The header.</param>
/// <param name="Questions">The question section.</param>
/// <param name="Answers">The answer section.</param>
/// <param name="Authorities">The authority section.</param>
/// <param name="Additionals">The additional section, where an OPT record sits.</param>
public sealed record DnsMessage(
    DnsHeader Header,
    IReadOnlyList<DnsQuestion> Questions,
    IReadOnlyList<DnsResourceRecord> Answers,
    IReadOnlyList<DnsResourceRecord> Authorities,
    IReadOnlyList<DnsResourceRecord> Additionals);
