namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Writes a <see cref="DnsResponse"/> as one message no longer than a limit, cutting it as RFC 2181
/// section 9 and ADR-0092 decision 3 say. UDP's limit is the requester's payload size
/// (<see cref="DnsUdpResponseWriter"/>); TCP's is the 65535 bytes its length prefix can carry
/// (<see cref="DnsTcpResponseWriter"/>, ADR-0092 decision 4).
/// </summary>
/// <remarks>
/// A response past the limit is cut: the additional section first, without TC; then the authority
/// section, without TC unless it held the negative answer's SOA; then the answer section's RRsets
/// are kept whole, in order, while they fit, and TC = 1 is set. The response's OPT is always kept.
/// </remarks>
public static class DnsResponseWriter
{
    /// <summary>The block length padded answers are rounded up to (RFC 8467 section 4.1's recommended response policy).</summary>
    public const int PaddingBlockLength = 468;

    /// <summary>
    /// Returns the message <see cref="WriteWithin"/> writes, padded for an encrypted transport (RFC 7830):
    /// when the query's OPT carried a Padding option and the response has an OPT, the response's OPT ends
    /// with a Padding option that makes the message a multiple of <see cref="PaddingBlockLength"/> bytes.
    /// Otherwise it is <see cref="WriteWithin"/>'s message.
    /// </summary>
    /// <remarks>
    /// Padding only lengthens the OPT, which every cut keeps, so it is counted after the cut. When the
    /// padded message would pass <paramref name="limit"/> the Padding option is kept empty: RFC 7830
    /// section 3 allows any length, and an empty one still tells the client padding is understood.
    /// </remarks>
    public static byte[] WritePaddedWithin(DnsResponse response, int limit)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.ResponseOpt is not { } responseOpt
            || response.QueryOpt is not { } queryOpt
            || !queryOpt.Options.Any(option => option.Code == DnsOption.PaddingCode))
        {
            return WriteWithin(response, limit);
        }

        var empty = WriteWithin(response with { ResponseOpt = WithPadding(responseOpt, 0) }, limit);
        var padding = ((PaddingBlockLength - (empty.Length % PaddingBlockLength)) % PaddingBlockLength);
        if (empty.Length + padding > limit)
        {
            return empty;
        }

        return WriteWithin(response with { ResponseOpt = WithPadding(responseOpt, padding) }, limit);
    }

    private static DnsOptRecord WithPadding(DnsOptRecord opt, int length) =>
        opt with { Options = [.. opt.Options, new DnsOption(DnsOption.PaddingCode, new byte[length])] };

    /// <summary>Returns the message that answers with <paramref name="response"/>, no longer than <paramref name="limit"/>, cut to fit as the remarks say.</summary>
    public static byte[] WriteWithin(DnsResponse response, int limit)
    {
        ArgumentNullException.ThrowIfNull(response);

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
