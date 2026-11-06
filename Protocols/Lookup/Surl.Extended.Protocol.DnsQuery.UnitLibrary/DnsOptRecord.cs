namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The EDNS(0) OPT pseudo-record read by its meaning (RFC 6891 section 6.1): the owner is the
/// root, CLASS is the requester's UDP payload size, and TTL holds the extended RCODE's upper
/// eight bits, the version and the DO flag (RFC 3225 section 3). The other fifteen flag bits are
/// zero when written and ignored when read (RFC 6891 section 6.1.4).
/// </summary>
/// <param name="PayloadSize">The UDP payload size, in bytes.</param>
/// <param name="ExtendedResponseCodeHigh">The upper eight bits of the 12-bit RCODE.</param>
/// <param name="Version">The EDNS version; 0 is the only one defined.</param>
/// <param name="IsDnssecOk">DO: the requester accepts DNSSEC records.</param>
/// <param name="Options">The options, in order.</param>
public sealed record DnsOptRecord(
    ushort PayloadSize,
    byte ExtendedResponseCodeHigh,
    byte Version,
    bool IsDnssecOk,
    IReadOnlyList<DnsOption> Options)
{
    private const uint DnssecOkBit = 0x8000;

    /// <summary>Returns the OPT record as the resource record that carries it, owned by the root.</summary>
    public DnsResourceRecord ToResourceRecord() => new(
        DnsName.Root,
        PayloadSize,
        ((uint)ExtendedResponseCodeHigh << 24) | ((uint)Version << 16) | (IsDnssecOk ? DnssecOkBit : 0),
        new DnsOptData(Options));

    /// <summary>
    /// Returns the OPT record <paramref name="record"/> carries. The owner is not checked: whether
    /// it is the root is the caller's question (RFC 6891 section 6.1.1).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="record"/> is not an OPT record.</exception>
    public static DnsOptRecord FromResourceRecord(DnsResourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Data is not DnsOptData data)
        {
            throw new ArgumentException("The record is not an OPT record.", nameof(record));
        }

        return new DnsOptRecord(
            record.Class,
            (byte)(record.TimeToLive >> 24),
            (byte)(record.TimeToLive >> 16),
            (record.TimeToLive & DnssecOkBit) != 0,
            data.Options);
    }
}
