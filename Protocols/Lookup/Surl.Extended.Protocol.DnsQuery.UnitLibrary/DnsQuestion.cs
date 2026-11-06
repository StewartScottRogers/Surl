namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>One entry of a message's question section (RFC 1035 section 4.1.2).</summary>
/// <param name="Name">QNAME.</param>
/// <param name="Type">QTYPE.</param>
/// <param name="Class">QCLASS (<see cref="DnsClasses"/>).</param>
public sealed record DnsQuestion(DnsName Name, DnsRecordType Type, ushort Class);
