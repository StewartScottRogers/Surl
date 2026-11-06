namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// One resource record of an answer, authority or additional section (RFC 1035 section 4.1.3).
/// Its TYPE is its data's (<see cref="DnsRecordData.Type"/>), so the two cannot disagree.
/// </summary>
/// <param name="Name">The owner name.</param>
/// <param name="Class">CLASS (<see cref="DnsClasses"/>), or for OPT the payload size.</param>
/// <param name="TimeToLive">TTL, or for OPT the extended RCODE, the version and the flags.</param>
/// <param name="Data">RDATA.</param>
public sealed record DnsResourceRecord(DnsName Name, ushort Class, uint TimeToLive, DnsRecordData Data)
{
    /// <summary>TYPE, taken from <see cref="Data"/>.</summary>
    public DnsRecordType Type => Data.Type;
}
