namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// One name of a zone and its RRsets (RFC 2181 section 5), each kept in the order its first record
/// was written. A node with no RRset is an empty non-terminal (RFC 4592 section 2.2.2): the name
/// exists because a name below it does.
/// </summary>
internal sealed class DnsZoneNode(DnsName owner)
{
    private readonly List<List<DnsResourceRecord>> recordSets = [];

    /// <summary>The name, as the zone file writes it.</summary>
    public DnsName Owner { get; } = owner;

    /// <summary>The RRsets, each a non-empty list of records of one type.</summary>
    public IReadOnlyList<IReadOnlyList<DnsResourceRecord>> RecordSets => recordSets;

    /// <summary>The RRset of <paramref name="type"/>, or <see langword="null"/>.</summary>
    public IReadOnlyList<DnsResourceRecord>? RecordSetOf(DnsRecordType type) =>
        recordSets.Find(set => set[0].Type == type);

    /// <summary>Adds <paramref name="record"/> to the RRset of its type, making the set when it is the first.</summary>
    public void Add(DnsResourceRecord record)
    {
        var set = recordSets.Find(existing => existing[0].Type == record.Type);

        if (set is null)
        {
            recordSets.Add([record]);
        }
        else
        {
            set.Add(record);
        }
    }
}
