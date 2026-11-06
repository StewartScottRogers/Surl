namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The messages of a whole-zone transfer over a stream (ADR-0092 decision 4; RFC 5936 sections
/// 2.2 and 4.1): the apex SOA first and last, every other record of the zone once between them,
/// in the zone file's order; each message as full as a TCP frame's 65535 bytes allow; the question
/// in the first message only. IXFR is answered the same way, since the server keeps no history
/// (RFC 1995 section 4).
/// </summary>
internal static class DnsZoneTransfer
{
    // The fewest bytes a record takes on the wire: a one-byte root owner and ten fixed bytes.
    private const int SmallestRecordLength = 11;

    /// <summary>
    /// Returns the responses that transfer <paramref name="zone"/> to a query with
    /// <paramref name="queryHeader"/> and <paramref name="question"/>, each carrying
    /// <paramref name="responseOpt"/> when it is not <see langword="null"/>.
    /// </summary>
    public static IReadOnlyList<DnsResponse> Messages(
        DnsHeader queryHeader,
        DnsQuestion question,
        DnsZone zone,
        DnsOptRecord? responseOpt,
        DnsOptRecord? queryOpt)
    {
        var soa = zone.StartOfAuthority;
        DnsResourceRecord[] records = [soa, .. zone.Records.Where(record => record.Type != DnsRecordType.SOA), soa];
        var first = new DnsAuthoritativeAnswer(DnsResponseCode.NoError, true, [], [], []).ToResponse(queryHeader, question);
        var responses = new List<DnsResponse>();
        var message = first;
        var taken = 0;

        while (taken < records.Length)
        {
            var count = CountThatFits(records, taken, message, responseOpt, queryOpt);
            responses.Add(new DnsResponse(message with { Answers = records[taken..(taken + count)] }, responseOpt, queryOpt));
            taken += count;
            message = first with { Questions = [] };
        }

        return responses;
    }

    // The most of records, from start, that fit in one frame with message's header and
    // question: at least one, so a record too large for any frame is sent alone and cut there.
    private static int CountThatFits(DnsResourceRecord[] records, int start, DnsMessage message, DnsOptRecord? responseOpt, DnsOptRecord? queryOpt)
    {
        var fewest = 1;
        var most = Math.Min(records.Length - start, DnsTcpResponseWriter.MaxMessageLength / SmallestRecordLength);

        while (fewest < most)
        {
            var middle = (fewest + most + 1) / 2;
            var response = new DnsResponse(message with { Answers = records[start..(start + middle)] }, responseOpt, queryOpt);

            if (DnsMessageWriter.Write(response.ToMessage()).Length <= DnsTcpResponseWriter.MaxMessageLength)
            {
                fewest = middle;
            }
            else
            {
                most = middle - 1;
            }
        }

        return fewest;
    }
}
