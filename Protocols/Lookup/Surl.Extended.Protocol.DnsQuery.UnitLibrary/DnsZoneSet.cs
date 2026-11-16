using System.Text;
using System.Text.Unicode;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The zones the DNS server answers from (ADR-0092 decision 1), each loaded from one master file
/// named <c>&lt;apex&gt;.zone</c>, read-only once loaded, and bounded as ADR-0006 bounds every
/// store. A query is answered from the zone with the longest apex containing its name
/// (<see cref="Answer"/>); a set with no zone answers every query <c>REFUSED</c>.
/// </summary>
public sealed class DnsZoneSet
{
    /// <summary>The most zone files a set holds by default.</summary>
    public const int DefaultMaxZones = 1000;

    /// <summary>The most records, all zones together, a set holds by default.</summary>
    public const int DefaultMaxRecords = 1_000_000;

    /// <summary>The most bytes of owner names and RDATA, all zones together, a set holds by default (256 MiB).</summary>
    public const long DefaultMaxTotalBytes = 268_435_456;

    /// <summary>The longest RDATA one record may hold (RFC 1035 section 3.2.1's RDLENGTH).</summary>
    public const int MaxRecordDataBytes = 65535;

    /// <summary>The file name extension every zone file has.</summary>
    public const string ZoneFileExtension = ".zone";

    /// <summary>The most bytes of zone-file text one <see cref="SeedZone"/> takes (16 MiB, ADR-0158 decision 3).</summary>
    public const int MaxSeedTextBytes = 16 * 1024 * 1024;

    /// <summary>The default most bytes <see cref="TakeSnapshot(long)"/> copies (256 MiB, ADR-0124's <c>MaxSnapshotBytes</c>).</summary>
    public const long DefaultMaxSnapshotBytes = 268_435_456;

    private readonly object changeLock = new();
    private readonly int maxZones;
    private readonly int maxRecords;
    private readonly long maxTotalBytes;
    private readonly ISurlEventPublisher events;
    private DnsZoneView view = DnsZoneView.Empty;
    private DnsZoneView configured = DnsZoneView.Empty;

    /// <summary>Creates an empty set bounded by the limits given.</summary>
    /// <param name="maxZones">The most zones.</param>
    /// <param name="maxRecords">The most records, all zones together.</param>
    /// <param name="maxTotalBytes">The most bytes of owner names and RDATA, all zones together.</param>
    /// <param name="events">Where seed, reset and restore publish their <see cref="StoreChangedEvent"/>; none by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is negative.</exception>
    public DnsZoneSet(
        int maxZones = DefaultMaxZones,
        int maxRecords = DefaultMaxRecords,
        long maxTotalBytes = DefaultMaxTotalBytes,
        ISurlEventPublisher? events = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxZones);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRecords);
        ArgumentOutOfRangeException.ThrowIfNegative(maxTotalBytes);

        this.maxZones = maxZones;
        this.maxRecords = maxRecords;
        this.maxTotalBytes = maxTotalBytes;
        this.events = events ?? DiscardingSurlEventPublisher.Instance;
    }

    /// <summary>The apexes of the zones held, in the order they were added.</summary>
    public IReadOnlyList<DnsName> Apexes => view.Apexes;

    /// <summary>
    /// Writes the zone at <paramref name="apex"/> as master-file text for a test reading it back (ADR-0164 decision 9):
    /// every record on its own line with absolute names, a TTL and a class. Changes and publishes nothing.
    /// </summary>
    /// <param name="apex">The zone's apex, a domain name with or without its final dot; case is ignored.</param>
    /// <returns>The text, or <see langword="null"/> when no zone has that apex or the text is no domain name.</returns>
    public string? ExportZone(string apex)
    {
        ArgumentNullException.ThrowIfNull(apex);
        DnsName name;
        try
        {
            name = DnsName.Parse(apex);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var zone = CurrentView.ZoneAtApex(name);
        return zone is null ? null : DnsZoneFileWriter.Write(zone);
    }

    /// <summary>What the set holds now, as one unit a query answers from whole.</summary>
    internal DnsZoneView CurrentView => Volatile.Read(ref view);

    /// <summary>
    /// Seeds the zone <paramref name="zoneFileText"/> holds, as <see cref="Add"/> adds a file named
    /// <c>&lt;apex&gt;.zone</c>, then publishes <see cref="StoreChange.Seeded"/> named for the apex
    /// (<c>example.com.</c>) with the zone's byte count. The fault plan is never consulted. A seed is lost by
    /// <see cref="Reset"/> unless <paramref name="keepOnReset"/> is set (ADR-0158 decision 3).
    /// </summary>
    /// <param name="apex">The zone's apex, a domain name with or without its final dot.</param>
    /// <param name="zoneFileText">The zone file's text, at most <see cref="MaxSeedTextBytes"/> bytes of UTF-8.</param>
    /// <param name="keepOnReset">Whether <see cref="Reset"/> returns to a set holding this zone too.</param>
    /// <exception cref="DnsZoneFileException">The text is over the limit, cannot be a zone, a zone with the apex is
    /// held, or the set's bounds would be passed. The set is unchanged and nothing is published.</exception>
    public void SeedZone(string apex, string zoneFileText, bool keepOnReset = false)
    {
        ArgumentNullException.ThrowIfNull(apex);
        ArgumentNullException.ThrowIfNull(zoneFileText);

        var content = Encoding.UTF8.GetBytes(zoneFileText);
        if (content.Length > MaxSeedTextBytes)
        {
            throw new DnsZoneFileException(null, "past the zones' bounds");
        }

        lock (changeLock)
        {
            var zone = Include(apex + ZoneFileExtension, content, keepOnReset);
            events.Publish(new StoreChangedEvent(StoreKind.Zones, StoreChange.Seeded, zone.Apex.ToString(), zone.ByteCount, null));
        }
    }

    /// <summary>
    /// Returns the set to the zones <see cref="Add"/> loaded and any seed kept, drops every other, and
    /// publishes one <see cref="StoreChange.Reset"/> with an empty name and 0 bytes. A query running meanwhile
    /// sees the old zones or the new, never a mix.
    /// </summary>
    public void Reset()
    {
        lock (changeLock)
        {
            Volatile.Write(ref view, configured);
            events.Publish(new StoreChangedEvent(StoreKind.Zones, StoreChange.Reset, string.Empty, 0, null));
        }
    }

    /// <summary>Copies what the set holds into a <see cref="DnsZoneSetSnapshot"/>. Changes and publishes nothing.</summary>
    /// <param name="maxBytes">The most bytes the snapshot may hold, as <see cref="DnsZoneSetSnapshot.ByteCount"/> counts.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The set holds more than <paramref name="maxBytes"/>.</exception>
    public DnsZoneSetSnapshot TakeSnapshot(long maxBytes = DefaultMaxSnapshotBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        var held = CurrentView;
        if (held.ByteCount > maxBytes)
        {
            throw new InvalidOperationException(
                $"The DNS zones hold {held.ByteCount} bytes, more than the {maxBytes} a snapshot may hold.");
        }

        return new DnsZoneSetSnapshot(this, held);
    }

    /// <summary>
    /// Replaces the zones with <paramref name="snapshot"/>'s and publishes one <see cref="StoreChange.Restored"/>
    /// with an empty name and the snapshot's byte count.
    /// </summary>
    /// <param name="snapshot">A snapshot this set took.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> was taken from another set.</exception>
    public void Restore(DnsZoneSetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Set != this)
        {
            throw new ArgumentException("The snapshot was taken from another DNS zone set.", nameof(snapshot));
        }

        lock (changeLock)
        {
            Volatile.Write(ref view, snapshot.Zones);
            events.Publish(new StoreChangedEvent(StoreKind.Zones, StoreChange.Restored, string.Empty, snapshot.ByteCount, null));
        }
    }

    /// <summary>
    /// Adds the zone whose master file is named <paramref name="fileName"/> and holds
    /// <paramref name="content"/>: UTF-8 without a byte order mark, its apex the file name
    /// without <c>.zone</c>. A zone added so is what <see cref="Reset"/> returns to. Publishes nothing.
    /// </summary>
    /// <param name="fileName">The file's name, without a folder: <c>example.com.zone</c>.</param>
    /// <param name="content">The file's bytes.</param>
    /// <exception cref="DnsZoneFileException">
    /// The file cannot be a zone, a zone with its apex is already held, or the set's bounds would be passed.
    /// The set is unchanged.
    /// </exception>
    public void Add(string fileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        lock (changeLock)
        {
            Include(fileName, content, keepOnReset: true);
        }
    }

    /// <summary>Reads and adds one zone, to what the set holds and, when asked, to what reset returns to. Called under the lock.</summary>
    private DnsZone Include(string fileName, byte[] content, bool keepOnReset)
    {
        var held = view;
        if (held.ZoneCount >= maxZones)
        {
            throw new DnsZoneFileException(null, "past the zones' bounds");
        }

        var apex = ApexOf(fileName);
        if (held.HasApex(apex) || configured.HasApex(apex))
        {
            throw new DnsZoneFileException(null, $"a second zone for {apex}");
        }

        var zone = DnsZone.Build(apex, DnsZoneFileReader.Read(apex, TextOf(content)));
        if (held.RecordCount + (long)zone.RecordCount > maxRecords || held.ByteCount + zone.ByteCount > maxTotalBytes)
        {
            throw new DnsZoneFileException(null, "past the zones' bounds");
        }

        Volatile.Write(ref view, held.With(zone));
        if (keepOnReset)
        {
            configured = configured.With(zone);
        }

        return zone;
    }

    /// <summary>
    /// Answers <paramref name="question"/> as an authoritative server does (RFC 1034 section
    /// 4.3.2; ADR-0092 decision 2's table): the matching RRset, NODATA or NXDOMAIN with the SOA,
    /// a CNAME chain followed through every zone held, a wildcard, a referral, or <c>REFUSED</c>
    /// for a name in no zone or a class other than IN and ANY.
    /// </summary>
    public DnsAuthoritativeAnswer Answer(DnsQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        return DnsAuthoritativeLookup.Answer(this, question);
    }

    /// <summary>The zone with the longest apex at or above <paramref name="name"/>, or <see langword="null"/>.</summary>
    internal DnsZone? ZoneFor(DnsName name) => CurrentView.ZoneFor(name);

    /// <summary>The zone whose apex is <paramref name="name"/>, or <see langword="null"/>.</summary>
    internal DnsZone? ZoneAtApex(DnsName name) => CurrentView.ZoneAtApex(name);

    private static DnsName ApexOf(string fileName)
    {
        if (!fileName.EndsWith(ZoneFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new DnsZoneFileException(null, "a file name not ending in .zone");
        }

        try
        {
            return DnsName.Parse(fileName[..^ZoneFileExtension.Length]);
        }
        catch (ArgumentException)
        {
            throw new DnsZoneFileException(null, "a file name that is not a domain name");
        }
    }

    private static string TextOf(byte[] content)
    {
        if (content.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            throw new DnsZoneFileException(1, "a byte order mark");
        }

        var lineNumber = 1;
        foreach (var line in content.AsSpan().Split((byte)'\n'))
        {
            if (!Utf8.IsValid(content.AsSpan()[line]))
            {
                throw new DnsZoneFileException(lineNumber, "not UTF-8");
            }

            lineNumber++;
        }

        return Encoding.UTF8.GetString(content);
    }
}
