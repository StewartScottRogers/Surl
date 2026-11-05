namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A record's RDATA, read and written field by field for the types ADR-0092 decision 2 serves and
/// as opaque bytes for every other type (RFC 3597).
/// </summary>
public abstract record DnsRecordData
{
    /// <summary>The record type this data belongs to.</summary>
    public abstract DnsRecordType Type { get; }

    /// <summary>Writes the RDATA, without its length, at the writer's position.</summary>
    internal abstract void WriteTo(DnsMessageWriter writer);
}
