using System.Buffers.Binary;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Answers one query message, whatever transport carried it (ADR-0092 decision 2): reads it,
/// drops what must not be answered, answers what cannot be read or is not a standard query with
/// the RCODE the ADR gives, and looks the rest up in the served zones.
/// </summary>
/// <remarks>
/// The checks, in order:
/// <list type="number">
/// <item>Fewer than 12 bytes, no header: dropped (<see langword="null"/>), there is no ID to answer.</item>
/// <item>QR = 1, a response: dropped, since answering a response can loop two servers.</item>
/// <item>An opcode other than QUERY: <c>NOTIMP</c>. The opcode is the header's, so it is checked
/// before the body, whose form another opcode may define otherwise.</item>
/// <item>A message that cannot be read whole, or holds more than one OPT record or one not owned
/// by the root (RFC 6891 section 6.1.1): <c>FORMERR</c>.</item>
/// <item>QDCOUNT other than 1 (RFC 9619): <c>FORMERR</c>, no question echoed.</item>
/// <item>An OPT of a version above 0 (RFC 6891 section 6.1.3): <c>BADVERS</c>.</item>
/// <item>AXFR or IXFR: <c>REFUSED</c>, since zone transfer is off (ADR-0092 decision 4).</item>
/// <item>Anything else: the zones' authoritative answer.</item>
/// </list>
/// Every answer echoes the query's ID, opcode and RD, with QR = 1, RA = 0 and Z = 0, and its one
/// question when the query had exactly one and it was read. It carries an OPT exactly when the
/// query had one it could read (RFC 6891 section 7): payload size
/// <see cref="ServerPayloadSize"/>, version 0, DO copied from the query (RFC 3225 section 3).
/// </remarks>
public sealed class DnsQueryAnswerer
{
    /// <summary>The UDP payload size the server's OPT advertises, and the most a UDP answer holds (ADR-0092 decision 3).</summary>
    public const ushort ServerPayloadSize = 1232;

    /// <summary>The extended RCODE BADVERS (RFC 6891 section 9).</summary>
    internal const int BadVersion = 16;

    private readonly DnsZoneSet zones;

    /// <summary>Creates an answerer that answers from <paramref name="zones"/>.</summary>
    public DnsQueryAnswerer(DnsZoneSet zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        this.zones = zones;
    }

    /// <summary>
    /// Returns the response to <paramref name="query"/>, one whole message, or
    /// <see langword="null"/> when it is to be dropped unanswered.
    /// </summary>
    public DnsResponse? Answer(ReadOnlySpan<byte> query)
    {
        var read = DnsMessageReader.Read(query);

        if (read.Header is not { IsResponse: false } header)
        {
            return null;
        }

        var questionCount = BinaryPrimitives.ReadUInt16BigEndian(query[4..]);
        IReadOnlyList<DnsQuestion> echoed = questionCount == 1 && read.Questions.Count == 1 ? read.Questions : [];
        var queryOpt = OptOf(read.Message, out var optIsReadable);

        if (header.Opcode != DnsOpcode.Query)
        {
            return Fault(header, echoed, DnsResponseCode.NotImplemented, queryOpt);
        }

        if (read.Message is null || !optIsReadable)
        {
            return Fault(header, echoed, DnsResponseCode.FormatError, null);
        }

        return questionCount == 1
            ? AnswerQuestion(header, echoed[0], queryOpt)
            : Fault(header, [], DnsResponseCode.FormatError, queryOpt);
    }

    private DnsResponse AnswerQuestion(DnsHeader header, DnsQuestion question, DnsOptRecord? queryOpt)
    {
        if (queryOpt is { Version: > 0 })
        {
            return Fault(header, [question], (DnsResponseCode)(BadVersion & 0xF), queryOpt, BadVersion >> 4);
        }

        var answer = question.Type is DnsRecordType.AXFR or DnsRecordType.IXFR
            ? DnsAuthoritativeAnswer.Refused
            : zones.Answer(question);

        return new DnsResponse(answer.ToResponse(header, question), ResponseOptFor(queryOpt, 0), queryOpt);
    }

    // The query's one OPT record, if any; readable is false when it has more than one, or one
    // whose owner is not the root (RFC 6891 section 6.1.1).
    private static DnsOptRecord? OptOf(DnsMessage? message, out bool readable)
    {
        var opts = message?.Additionals.Where(record => record.Type == DnsRecordType.OPT).ToArray() ?? [];
        readable = opts.Length <= 1 && opts.All(record => record.Name.LabelCount == 0);
        return readable && opts.Length == 1 ? DnsOptRecord.FromResourceRecord(opts[0]) : null;
    }

    private static DnsResponse Fault(
        DnsHeader query,
        IReadOnlyList<DnsQuestion> questions,
        DnsResponseCode responseCode,
        DnsOptRecord? queryOpt,
        int extendedResponseCodeHigh = 0)
    {
        var header = new DnsHeader(
            query.Id,
            IsResponse: true,
            query.Opcode,
            IsAuthoritativeAnswer: false,
            IsTruncated: false,
            query.IsRecursionDesired,
            IsRecursionAvailable: false,
            Z: 0,
            responseCode);

        return new DnsResponse(
            new DnsMessage(header, questions, [], [], []),
            ResponseOptFor(queryOpt, (byte)extendedResponseCodeHigh),
            queryOpt);
    }

    private static DnsOptRecord? ResponseOptFor(DnsOptRecord? queryOpt, byte extendedResponseCodeHigh) => queryOpt is null
        ? null
        : new DnsOptRecord(ServerPayloadSize, extendedResponseCodeHigh, 0, queryOpt.IsDnssecOk, []);
}
