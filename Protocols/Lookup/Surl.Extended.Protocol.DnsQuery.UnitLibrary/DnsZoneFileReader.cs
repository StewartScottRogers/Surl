namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>A record read from a zone file, with the first physical line of its entry.</summary>
/// <param name="Record">The record.</param>
/// <param name="LineNumber">The first physical line of its entry, from 1.</param>
internal sealed record DnsZoneRecord(DnsResourceRecord Record, int LineNumber);

/// <summary>
/// Reads the records of a zone's master file (RFC 1035 section 5; ADR-0092 decision 1):
/// <c>$ORIGIN</c>, <c>$TTL</c> (RFC 2308 section 4), <c>$INCLUDE</c> refused, <c>@</c>, relative
/// names, a blank owner meaning the previous owner, TTL and class in either order, the class IN
/// only, and every record's RDATA in its presentation form or RFC 3597's <c>\#</c> form. A record
/// without a TTL takes the last <c>$TTL</c>, or else the last TTL written (RFC 1035 section 5.1).
/// What the records must hold together is <see cref="DnsZone"/>'s to check.
/// </summary>
internal sealed class DnsZoneFileReader
{
    private readonly DnsName apex;
    private readonly List<DnsZoneRecord> records = [];
    private DnsName origin;
    private DnsName? previousOwner;
    private uint? defaultTimeToLive;
    private uint? lastTimeToLive;

    private DnsZoneFileReader(DnsName apex)
    {
        this.apex = apex;
        origin = apex;
    }

    /// <summary>Returns the records of <paramref name="text"/>, the master file of the zone at <paramref name="apex"/>.</summary>
    /// <exception cref="DnsZoneFileException">An entry cannot be read; the exception names its line.</exception>
    public static IReadOnlyList<DnsZoneRecord> Read(DnsName apex, string text)
    {
        var reader = new DnsZoneFileReader(apex);

        foreach (var entry in DnsMasterFileTokenizer.Entries(text))
        {
            try
            {
                reader.Take(entry);
            }
            catch (DnsZoneFileException exception) when (exception.LineNumber is null)
            {
                throw new DnsZoneFileException(entry.LineNumber, exception.Fault);
            }
        }

        return reader.records;
    }

    private void Take(DnsMasterFileEntry entry)
    {
        if (!entry.StartsWithBlank && IsDirective(entry.Tokens[0]))
        {
            TakeDirective(entry.Tokens);
        }
        else
        {
            TakeRecord(entry);
        }
    }

    private static bool IsDirective(DnsMasterFileToken token) => !token.IsQuoted && token.Text.StartsWith('$');

    private void TakeDirective(IReadOnlyList<DnsMasterFileToken> tokens)
    {
        var directive = tokens[0].Text.ToUpperInvariant();

        switch (directive)
        {
            case "$ORIGIN" when tokens.Count == 2:
                origin = DnsPresentationReader.ReadName(tokens[1], origin);
                break;
            case "$TTL" when tokens.Count == 2:
                defaultTimeToLive = DnsPresentationReader.ReadTimeToLive(tokens[1]);
                break;
            case "$INCLUDE":
                throw new DnsZoneFileException(null, "$INCLUDE is not served");
            case "$ORIGIN" or "$TTL":
                throw new DnsZoneFileException(null, $"{directive} takes one value");
            default:
                throw new DnsZoneFileException(null, $"an unknown directive {tokens[0].Text}");
        }
    }

    private void TakeRecord(DnsMasterFileEntry entry)
    {
        var tokens = entry.Tokens;
        var owner = entry.StartsWithBlank ? PreviousOwner() : DnsPresentationReader.ReadName(tokens[0], origin);
        var index = entry.StartsWithBlank ? 0 : 1;
        var (timeToLive, typeIndex) = ReadTimeToLiveAndClass(tokens, index);

        if (typeIndex >= tokens.Count)
        {
            throw new DnsZoneFileException(null, "no type");
        }

        if (!owner.IsAtOrBelow(apex))
        {
            throw new DnsZoneFileException(null, "an owner outside the zone");
        }

        var type = DnsPresentationReader.ReadType(tokens[typeIndex]);
        var data = DnsPresentationReader.ReadRecordData(type, tokens.Skip(typeIndex + 1).ToArray(), origin);
        records.Add(new DnsZoneRecord(new DnsResourceRecord(owner, DnsClasses.Internet, TimeToLiveFor(timeToLive), data), entry.LineNumber));
        previousOwner = owner;
    }

    private DnsName PreviousOwner() =>
        previousOwner ?? throw new DnsZoneFileException(null, "a blank owner with no owner before it");

    private static (uint? TimeToLive, int Next) ReadTimeToLiveAndClass(IReadOnlyList<DnsMasterFileToken> tokens, int index)
    {
        uint? timeToLive = null;
        var hasClass = false;

        while (index < tokens.Count && TakeTimeToLiveOrClass(tokens[index], ref timeToLive, ref hasClass))
        {
            index++;
        }

        return (timeToLive, index);
    }

    /// <summary>Takes <paramref name="token"/> as the entry's TTL or class when it is one not yet taken; returns whether it did.</summary>
    private static bool TakeTimeToLiveOrClass(DnsMasterFileToken token, ref uint? timeToLive, ref bool hasClass)
    {
        if (timeToLive is null && DnsPresentationReader.IsTimeToLive(token))
        {
            timeToLive = DnsPresentationReader.ReadTimeToLive(token);
            return true;
        }

        if (hasClass || !DnsPresentationReader.IsClass(token))
        {
            return false;
        }

        hasClass = DnsPresentationReader.IsInternetClass(token)
            ? true
            : throw new DnsZoneFileException(null, "a class other than IN");
        return true;
    }

    private uint TimeToLiveFor(uint? written)
    {
        if (written is { } explicitTimeToLive)
        {
            lastTimeToLive = explicitTimeToLive;
            return explicitTimeToLive;
        }

        return defaultTimeToLive ?? lastTimeToLive ?? throw new DnsZoneFileException(null, "no TTL, and no $TTL before it");
    }
}
