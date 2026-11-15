namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// An immutable list of zones with the records and bytes they hold: what one <see cref="DnsZoneSet"/> held at one
/// moment. A set replaces its view whole, so a query that took a view answers from the zones it saw, never
/// from half a change (BL-1164).
/// </summary>
internal sealed class DnsZoneView
{
    /// <summary>A view holding no zone.</summary>
    public static readonly DnsZoneView Empty = new([]);

    private readonly DnsZone[] zones;

    private DnsZoneView(DnsZone[] zones)
    {
        this.zones = zones;
        RecordCount = zones.Sum(zone => zone.RecordCount);
        ByteCount = zones.Sum(zone => zone.ByteCount);
    }

    /// <summary>The number of zones.</summary>
    public int ZoneCount => zones.Length;

    /// <summary>The records of every zone.</summary>
    public int RecordCount { get; }

    /// <summary>The bytes of owner names and RDATA of every zone.</summary>
    public long ByteCount { get; }

    /// <summary>The apexes, in the order the zones were added.</summary>
    public IReadOnlyList<DnsName> Apexes => zones.Select(zone => zone.Apex).ToArray();

    /// <summary>This view with <paramref name="zone"/> added last.</summary>
    public DnsZoneView With(DnsZone zone) => new([.. zones, zone]);

    /// <summary>Whether a zone has apex <paramref name="apex"/>.</summary>
    public bool HasApex(DnsName apex) => ZoneAtApex(apex) is not null;

    /// <summary>The zone with the longest apex at or above <paramref name="name"/>, or <see langword="null"/>.</summary>
    public DnsZone? ZoneFor(DnsName name) =>
        zones.Where(zone => name.IsAtOrBelow(zone.Apex)).MaxBy(zone => zone.Apex.LabelCount);

    /// <summary>The zone whose apex is <paramref name="name"/>, or <see langword="null"/>.</summary>
    public DnsZone? ZoneAtApex(DnsName name) =>
        Array.Find(zones, zone => zone.Apex.FoldedKey == name.FoldedKey);
}
