namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// One zone, its records checked as RFC 1035 section 5.2 and RFC 2181 sections 5.2 and 10.1 ask
/// (ADR-0092 decision 1): exactly one SOA, at the apex; no CNAME beside other data, and at most
/// one; one TTL per RRset; no record written twice; no wildcard NS (RFC 4592 section 4.2); no
/// RDATA past 65535 bytes. Names are matched ignoring ASCII case (RFC 4343).
/// </summary>
internal sealed class DnsZone
{
    private readonly Dictionary<string, DnsZoneNode> nodes = new(StringComparer.Ordinal);
    private readonly HashSet<string> recordKeys = new(StringComparer.Ordinal);
    private readonly List<DnsResourceRecord> records = [];
    private DnsResourceRecord? startOfAuthority;

    private DnsZone(DnsName apex)
    {
        Apex = apex;
        nodes[apex.FoldedKey] = new DnsZoneNode(apex);
    }

    /// <summary>The apex, the zone's top name.</summary>
    public DnsName Apex { get; }

    /// <summary>The SOA at the apex.</summary>
    public DnsResourceRecord StartOfAuthority => startOfAuthority!;

    /// <summary>The NS RRset at the apex, empty when the zone has none.</summary>
    public IReadOnlyList<DnsResourceRecord> ApexNameServers => nodes[Apex.FoldedKey].RecordSetOf(DnsRecordType.NS) ?? [];

    /// <summary>Every record, in the order the zone file holds them, the SOA among them.</summary>
    public IReadOnlyList<DnsResourceRecord> Records => records;

    /// <summary>The number of records.</summary>
    public int RecordCount { get; private set; }

    /// <summary>The bytes of owner names and RDATA the records hold, each owner counted uncompressed.</summary>
    public long ByteCount { get; private set; }

    /// <summary>Returns the zone at <paramref name="apex"/> holding <paramref name="records"/>.</summary>
    /// <exception cref="DnsZoneFileException">The records cannot be one zone.</exception>
    public static DnsZone Build(DnsName apex, IReadOnlyList<DnsZoneRecord> records)
    {
        var zone = new DnsZone(apex);

        foreach (var record in records)
        {
            try
            {
                zone.Add(record.Record);
            }
            catch (DnsZoneFileException exception)
            {
                throw new DnsZoneFileException(record.LineNumber, exception.Fault);
            }
        }

        if (zone.startOfAuthority is null)
        {
            throw new DnsZoneFileException(null, "no SOA at the apex");
        }

        return zone;
    }

    /// <summary>The node named exactly <paramref name="name"/>, an empty non-terminal included, or <see langword="null"/>.</summary>
    public DnsZoneNode? NodeOf(DnsName name) => nodes.GetValueOrDefault(name.FoldedKey);

    /// <summary>
    /// Finds what answers <paramref name="name"/>, at or below the apex (RFC 1034 section 4.3.2
    /// step 3; RFC 4592 section 3.3.1): the highest delegation at or above it, else its own node,
    /// else the wildcard below its closest encloser, else nothing.
    /// </summary>
    public DnsZoneMatch Find(DnsName name)
    {
        for (var depth = Apex.LabelCount + 1; depth <= name.LabelCount; depth++)
        {
            var node = NodeOf(name.Suffix(name.LabelCount - depth));

            if (node?.RecordSetOf(DnsRecordType.NS) is not null)
            {
                return new DnsZoneMatch(DnsZoneMatchKind.Delegation, node);
            }
        }

        var exact = NodeOf(name);
        if (exact is not null)
        {
            return new DnsZoneMatch(DnsZoneMatchKind.Name, exact);
        }

        var wildcard = NodeOf(ClosestEncloser(name).WildcardChild());
        return wildcard is null
            ? new DnsZoneMatch(DnsZoneMatchKind.NoName, null)
            : new DnsZoneMatch(DnsZoneMatchKind.Name, wildcard);
    }

    private DnsName ClosestEncloser(DnsName name)
    {
        var index = 1;

        while (NodeOf(name.Suffix(index)) is null)
        {
            index++;
        }

        return name.Suffix(index);
    }

    private void Add(DnsResourceRecord record)
    {
        var recordData = DnsMessageWriter.WriteRecordData(record.Data);
        CheckPlace(record, recordData.Length);

        var node = NodeWithAncestors(record.Name);
        CheckBeside(node, record);
        CheckSet(node, record, recordData);

        node.Add(record);
        records.Add(record);
        startOfAuthority ??= record.Type == DnsRecordType.SOA ? record : null;
        RecordCount++;
        ByteCount += record.Name.WireLength + recordData.Length;
    }

    private void CheckPlace(DnsResourceRecord record, int recordDataLength)
    {
        if (recordDataLength > DnsZoneSet.MaxRecordDataBytes)
        {
            throw new DnsZoneFileException(null, "an RDATA past 65535 bytes");
        }

        if (record.Type == DnsRecordType.SOA)
        {
            CheckStartOfAuthority(record);
        }

        if (record.Type == DnsRecordType.NS && record.Name.IsWildcard)
        {
            throw new DnsZoneFileException(null, "a wildcard NS");
        }
    }

    private void CheckStartOfAuthority(DnsResourceRecord record)
    {
        if (record.Name.FoldedKey != Apex.FoldedKey)
        {
            throw new DnsZoneFileException(null, "an SOA below the apex");
        }

        if (startOfAuthority is not null)
        {
            throw new DnsZoneFileException(null, "a second SOA");
        }
    }

    private static void CheckBeside(DnsZoneNode node, DnsResourceRecord record)
    {
        var hasCanonicalName = node.RecordSetOf(DnsRecordType.CNAME) is not null;

        if (record.Type == DnsRecordType.CNAME && hasCanonicalName)
        {
            throw new DnsZoneFileException(null, "a second CNAME");
        }

        if (hasCanonicalName || (record.Type == DnsRecordType.CNAME && node.RecordSets.Count > 0))
        {
            throw new DnsZoneFileException(null, "a CNAME beside other data");
        }
    }

    private void CheckSet(DnsZoneNode node, DnsResourceRecord record, byte[] recordData)
    {
        if (node.RecordSetOf(record.Type) is [var first, ..] && first.TimeToLive != record.TimeToLive)
        {
            throw new DnsZoneFileException(null, "records of one set with different TTLs");
        }

        if (!recordKeys.Add($"{record.Name.FoldedKey}/{(ushort)record.Type}/{Convert.ToHexString(recordData)}"))
        {
            throw new DnsZoneFileException(null, "a record written twice");
        }
    }

    private DnsZoneNode NodeWithAncestors(DnsName owner)
    {
        for (var index = owner.LabelCount - Apex.LabelCount - 1; index > 0; index--)
        {
            var ancestor = owner.Suffix(index);
            nodes.TryAdd(ancestor.FoldedKey, new DnsZoneNode(ancestor));
        }

        if (!nodes.TryGetValue(owner.FoldedKey, out var node))
        {
            node = new DnsZoneNode(owner);
            nodes[owner.FoldedKey] = node;
        }

        return node;
    }
}
