namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Writes a <see cref="DnsResponse"/> as one UDP datagram no longer than the requester can take
/// (ADR-0092 decision 3; RFC 1035 section 4.2.1, RFC 6891 sections 6.2.3 to 6.2.5, RFC 2181
/// section 9).
/// </summary>
/// <remarks>
/// The limit is 512 bytes without an OPT in the query; with one, its payload size, a value below
/// 512 taken as 512, and never more than <see cref="DnsQueryAnswerer.ServerPayloadSize"/>. A
/// response past it is cut: the additional section first, without TC; then the authority section,
/// without TC unless it held the negative answer's SOA; then the answer section's RRsets are kept
/// whole, in order, while they fit, and TC = 1 is set. The response's OPT is always kept.
/// </remarks>
public static class DnsUdpResponseWriter
{
    /// <summary>The most a UDP answer holds without EDNS(0) (RFC 1035 section 4.2.1).</summary>
    public const int ClassicPayloadSize = 512;

    /// <summary>Returns the most bytes a UDP answer to a query with <paramref name="queryOpt"/> may hold.</summary>
    public static int LimitFor(DnsOptRecord? queryOpt) => queryOpt is null
        ? ClassicPayloadSize
        : Math.Clamp((int)queryOpt.PayloadSize, ClassicPayloadSize, DnsQueryAnswerer.ServerPayloadSize);

    /// <summary>Returns the datagram that answers with <paramref name="response"/>, cut to fit as the remarks say.</summary>
    public static byte[] Write(DnsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var limit = LimitFor(response.QueryOpt);
        var message = response.Message;

        var whole = DnsMessageWriter.Write(response.WithOpt(message));
        if (whole.Length <= limit)
        {
            return whole;
        }

        var withoutAdditionals = DnsMessageWriter.Write(response.WithOpt(message with { Additionals = [] }));
        if (withoutAdditionals.Length <= limit)
        {
            return withoutAdditionals;
        }

        var heldNegativeAnswer = message.Authorities.Any(record => record.Type == DnsRecordType.SOA);
        var answersOnly = message with { Authorities = [], Additionals = [] };
        var withoutAuthorities = DnsMessageWriter.Write(response.WithOpt(answersOnly with
        {
            Header = answersOnly.Header with { IsTruncated = heldNegativeAnswer },
        }));

        return withoutAuthorities.Length <= limit
            ? withoutAuthorities
            : WriteWholeRecordSetsThatFit(response, answersOnly, limit);
    }

    // Keeps the answer section's RRsets, in order, while the message with TC = 1 fits.
    private static byte[] WriteWholeRecordSetsThatFit(DnsResponse response, DnsMessage answersOnly, int limit)
    {
        var truncated = answersOnly with { Header = answersOnly.Header with { IsTruncated = true }, Answers = [] };
        var written = DnsMessageWriter.Write(response.WithOpt(truncated));

        foreach (var end in RecordSetEnds(answersOnly.Answers))
        {
            var longer = DnsMessageWriter.Write(response.WithOpt(truncated with { Answers = answersOnly.Answers.Take(end).ToArray() }));
            if (longer.Length > limit)
            {
                break;
            }

            written = longer;
        }

        return written;
    }

    // The count of records up to the end of each RRset: a run of records with one owner, type and class.
    private static List<int> RecordSetEnds(IReadOnlyList<DnsResourceRecord> records)
    {
        var ends = new List<int>();

        for (var index = 1; index <= records.Count; index++)
        {
            if (index == records.Count || !IsSameRecordSet(records[index - 1], records[index]))
            {
                ends.Add(index);
            }
        }

        return ends;
    }

    private static bool IsSameRecordSet(DnsResourceRecord first, DnsResourceRecord second) =>
        first.Type == second.Type && first.Class == second.Class && first.Name.FoldedKey == second.Name.FoldedKey;
}
