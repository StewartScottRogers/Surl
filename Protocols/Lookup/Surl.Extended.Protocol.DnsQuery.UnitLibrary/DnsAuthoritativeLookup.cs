namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The authoritative lookup of RFC 1034 section 4.3.2 steps 2 and 3, as ADR-0092 decision 2's table
/// answers each outcome. Records found at the name being looked up are answered with that name as
/// owner - the question's own case for the question's name (RFC 4343 section 4.1), the CNAME's
/// target as the zone writes it along a chain, and the name a wildcard stands in for (RFC 4592
/// section 3.3.1). Every other record keeps the zone's owner.
/// </summary>
internal sealed class DnsAuthoritativeLookup
{
    /// <summary>The most CNAMEs a chain is followed through before it is answered <c>SERVFAIL</c>.</summary>
    public const int MaxCanonicalNames = 16;

    private readonly DnsZoneSet zones;
    private readonly DnsRecordType type;
    private readonly List<DnsResourceRecord> answers = [];
    private readonly HashSet<string> visitedNames = new(StringComparer.Ordinal);

    private DnsAuthoritativeLookup(DnsZoneSet zones, DnsRecordType type)
    {
        this.zones = zones;
        this.type = type;
    }

    /// <summary>Answers <paramref name="question"/> from <paramref name="zones"/>.</summary>
    public static DnsAuthoritativeAnswer Answer(DnsZoneSet zones, DnsQuestion question)
    {
        var zone = question.Class is DnsClasses.Internet or DnsClasses.Any ? zones.ZoneFor(question.Name) : null;

        return zone is null
            ? DnsAuthoritativeAnswer.Refused
            : new DnsAuthoritativeLookup(zones, question.Type).Follow(zone, question.Name);
    }

    private DnsAuthoritativeAnswer Follow(DnsZone zone, DnsName name)
    {
        visitedNames.Add(name.FoldedKey);
        DnsAuthoritativeAnswer? answer = null;

        while (answer is null)
        {
            answer = Step(ref zone, ref name);
        }

        return answer;
    }

    /// <summary>Looks <paramref name="name"/> up in <paramref name="zone"/>; when it is a CNAME, moves both on to its target and answers nothing yet.</summary>
    private DnsAuthoritativeAnswer? Step(ref DnsZone zone, ref DnsName name)
    {
        var match = zone.Find(name);

        if (match.Kind == DnsZoneMatchKind.Delegation)
        {
            return Referral(match.Node!);
        }

        if (match.Kind == DnsZoneMatchKind.NoName)
        {
            return Negative(zone, DnsResponseCode.NonExistentDomain);
        }

        var canonicalName = CanonicalNameAt(match.Node!);
        return canonicalName is null
            ? Positive(zone, name, match.Node!)
            : FollowCanonicalName(canonicalName, ref zone, ref name);
    }

    private DnsResourceRecord? CanonicalNameAt(DnsZoneNode node) =>
        type is DnsRecordType.CNAME or DnsRecordType.ANY ? null : node.RecordSetOf(DnsRecordType.CNAME)?[0];

    private DnsAuthoritativeAnswer? FollowCanonicalName(DnsResourceRecord canonicalName, ref DnsZone zone, ref DnsName name)
    {
        if (answers.Count == MaxCanonicalNames)
        {
            return Chain(DnsResponseCode.ServerFailure);
        }

        answers.Add(canonicalName with { Name = name });
        name = ((DnsDomainNameData)canonicalName.Data).Name;

        if (!visitedNames.Add(name.FoldedKey))
        {
            return Chain(DnsResponseCode.ServerFailure);
        }

        var nextZone = zones.ZoneFor(name);
        if (nextZone is null)
        {
            return Chain(DnsResponseCode.NoError);
        }

        zone = nextZone;
        return null;
    }

    private DnsAuthoritativeAnswer Positive(DnsZone zone, DnsName name, DnsZoneNode node)
    {
        IReadOnlyList<IReadOnlyList<DnsResourceRecord>> sets = type == DnsRecordType.ANY
            ? node.RecordSets
            : node.RecordSetOf(type) is { } set ? [set] : [];

        if (sets.Count == 0)
        {
            return Negative(zone, DnsResponseCode.NoError);
        }

        answers.AddRange(sets.SelectMany(records => records).Select(record => record with { Name = name }));
        var authorities = zone.ApexNameServers.Where(record => !IsAnswered(record)).ToArray();
        return new DnsAuthoritativeAnswer(DnsResponseCode.NoError, true, answers, authorities, AddressesFor(answers.Concat(authorities)));
    }

    private DnsAuthoritativeAnswer Negative(DnsZone zone, DnsResponseCode responseCode)
    {
        var startOfAuthority = zone.StartOfAuthority;
        var minimum = ((DnsStartOfAuthorityData)startOfAuthority.Data).Minimum;
        var negative = startOfAuthority with { TimeToLive = Math.Min(startOfAuthority.TimeToLive, minimum) };
        return new DnsAuthoritativeAnswer(responseCode, true, answers, [negative], []);
    }

    private DnsAuthoritativeAnswer Referral(DnsZoneNode delegation)
    {
        var nameServers = delegation.RecordSetOf(DnsRecordType.NS)!;
        return new DnsAuthoritativeAnswer(DnsResponseCode.NoError, answers.Count > 0, answers, nameServers, AddressesFor(nameServers));
    }

    private DnsAuthoritativeAnswer Chain(DnsResponseCode responseCode) =>
        new(responseCode, true, answers, [], []);

    /// <summary>
    /// The A and AAAA records of the in-zone names the NS, MX and SRV records of
    /// <paramref name="records"/> name (RFC 1035 sections 3.3.9 and 3.3.11, RFC 2782), each name
    /// once and no record already answered.
    /// </summary>
    private DnsResourceRecord[] AddressesFor(IEnumerable<DnsResourceRecord> records) =>
        records
            .Select(record => TargetOf(record.Data))
            .OfType<DnsName>()
            .DistinctBy(target => target.FoldedKey)
            .Select(target => zones.ZoneFor(target)?.NodeOf(target))
            .OfType<DnsZoneNode>()
            .SelectMany(node => new[] { DnsRecordType.A, DnsRecordType.AAAA }.SelectMany(addressType => node.RecordSetOf(addressType) ?? []))
            .Where(record => !IsAnswered(record))
            .ToArray();

    private bool IsAnswered(DnsResourceRecord record) => answers.Exists(answer => ReferenceEquals(answer.Data, record.Data));

    private static DnsName? TargetOf(DnsRecordData data) => data switch
    {
        DnsDomainNameData { Type: DnsRecordType.NS } nameServer => nameServer.Name,
        DnsMailExchangeData mailExchange => mailExchange.Exchange,
        DnsServiceLocationData serviceLocation => serviceLocation.Target,
        _ => null,
    };
}
