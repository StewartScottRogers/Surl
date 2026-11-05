namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// What an authoritative lookup answers to one question (ADR-0092 decision 2's table): the RCODE,
/// whether the answer is authoritative, and the three record sections.
/// </summary>
/// <param name="ResponseCode">RCODE.</param>
/// <param name="IsAuthoritative">AA: the answer comes from a served zone.</param>
/// <param name="Answers">The answer section.</param>
/// <param name="Authorities">The authority section.</param>
/// <param name="Additionals">The additional section.</param>
public sealed record DnsAuthoritativeAnswer(
    DnsResponseCode ResponseCode,
    bool IsAuthoritative,
    IReadOnlyList<DnsResourceRecord> Answers,
    IReadOnlyList<DnsResourceRecord> Authorities,
    IReadOnlyList<DnsResourceRecord> Additionals)
{
    /// <summary>The answer to a name in no served zone, or a class other than IN and ANY: <c>REFUSED</c>, AA = 0, nothing else.</summary>
    public static DnsAuthoritativeAnswer Refused { get; } = new(DnsResponseCode.Refused, false, [], [], []);

    /// <summary>
    /// Returns the response to a query with <paramref name="queryHeader"/> and the one
    /// <paramref name="question"/> (RFC 1035 section 4.1.1; ADR-0092 decision 2): the query's ID,
    /// opcode and RD; QR = 1; RA = 0, since no recursion is offered; Z = 0; TC = 0; and the
    /// question echoed with its own case (RFC 4343 section 4.1).
    /// </summary>
    public DnsMessage ToResponse(DnsHeader queryHeader, DnsQuestion question)
    {
        ArgumentNullException.ThrowIfNull(queryHeader);
        ArgumentNullException.ThrowIfNull(question);

        var header = new DnsHeader(
            queryHeader.Id,
            IsResponse: true,
            queryHeader.Opcode,
            IsAuthoritative,
            IsTruncated: false,
            queryHeader.IsRecursionDesired,
            IsRecursionAvailable: false,
            Z: 0,
            ResponseCode);

        return new DnsMessage(header, [question], Answers, Authorities, Additionals);
    }
}
