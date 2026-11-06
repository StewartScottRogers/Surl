namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>What a zone holds for a name (RFC 1034 section 4.3.2 step 3).</summary>
internal enum DnsZoneMatchKind
{
    /// <summary>The name is at or below a delegation: the node is the cut, holding the NS RRset.</summary>
    Delegation,

    /// <summary>The name exists, or a wildcard stands in for it: the node holds what answers it.</summary>
    Name,

    /// <summary>The name does not exist and no wildcard matches it.</summary>
    NoName,
}

/// <summary>What <see cref="DnsZone.Find"/> found for a name.</summary>
/// <param name="Kind">What kind of match it is.</param>
/// <param name="Node">The delegation or the answering node; <see langword="null"/> for <see cref="DnsZoneMatchKind.NoName"/>.</param>
internal sealed record DnsZoneMatch(DnsZoneMatchKind Kind, DnsZoneNode? Node);
