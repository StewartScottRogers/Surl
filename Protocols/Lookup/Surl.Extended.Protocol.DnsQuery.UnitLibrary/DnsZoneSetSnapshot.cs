namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// An opaque, in-memory copy of one <see cref="DnsZoneSet"/>'s zones (ADR-0158 decision 5), made by
/// <see cref="DnsZoneSet.TakeSnapshot(long)"/> and given back by <see cref="DnsZoneSet.Restore"/> to the set that
/// made it, and to no other.
/// </summary>
public sealed class DnsZoneSetSnapshot
{
    internal DnsZoneSetSnapshot(DnsZoneSet set, DnsZoneView zones)
    {
        Set = set;
        Zones = zones;
    }

    /// <summary>The bytes of owner names and RDATA the snapshot's zones hold.</summary>
    public long ByteCount => Zones.ByteCount;

    internal DnsZoneSet Set { get; }

    internal DnsZoneView Zones { get; }
}
