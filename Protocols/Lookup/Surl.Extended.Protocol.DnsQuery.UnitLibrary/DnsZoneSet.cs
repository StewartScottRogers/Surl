using System.Text;
using System.Text.Unicode;

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

    private readonly List<DnsZone> zones = [];
    private readonly int maxZones;
    private readonly int maxRecords;
    private readonly long maxTotalBytes;
    private int recordCount;
    private long totalBytes;

    /// <summary>Creates an empty set bounded by the limits given.</summary>
    /// <param name="maxZones">The most zones.</param>
    /// <param name="maxRecords">The most records, all zones together.</param>
    /// <param name="maxTotalBytes">The most bytes of owner names and RDATA, all zones together.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is negative.</exception>
    public DnsZoneSet(int maxZones = DefaultMaxZones, int maxRecords = DefaultMaxRecords, long maxTotalBytes = DefaultMaxTotalBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxZones);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRecords);
        ArgumentOutOfRangeException.ThrowIfNegative(maxTotalBytes);

        this.maxZones = maxZones;
        this.maxRecords = maxRecords;
        this.maxTotalBytes = maxTotalBytes;
    }

    /// <summary>The apexes of the zones held, in the order they were added.</summary>
    public IReadOnlyList<DnsName> Apexes => zones.Select(zone => zone.Apex).ToArray();

    /// <summary>
    /// Adds the zone whose master file is named <paramref name="fileName"/> and holds
    /// <paramref name="content"/>: UTF-8 without a byte order mark, its apex the file name
    /// without <c>.zone</c>.
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

        if (zones.Count >= maxZones)
        {
            throw new DnsZoneFileException(null, "past the zones' bounds");
        }

        var apex = ApexOf(fileName);
        if (zones.Exists(zone => zone.Apex.FoldedKey == apex.FoldedKey))
        {
            throw new DnsZoneFileException(null, $"a second zone for {apex}");
        }

        var zone = DnsZone.Build(apex, DnsZoneFileReader.Read(apex, TextOf(content)));
        if (recordCount + (long)zone.RecordCount > maxRecords || totalBytes + zone.ByteCount > maxTotalBytes)
        {
            throw new DnsZoneFileException(null, "past the zones' bounds");
        }

        zones.Add(zone);
        recordCount += zone.RecordCount;
        totalBytes += zone.ByteCount;
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
    internal DnsZone? ZoneFor(DnsName name) =>
        zones.Where(zone => name.IsAtOrBelow(zone.Apex)).MaxBy(zone => zone.Apex.LabelCount);

    /// <summary>The zone whose apex is <paramref name="name"/>, or <see langword="null"/>.</summary>
    internal DnsZone? ZoneAtApex(DnsName name) =>
        zones.Find(zone => zone.Apex.FoldedKey == name.FoldedKey);

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
