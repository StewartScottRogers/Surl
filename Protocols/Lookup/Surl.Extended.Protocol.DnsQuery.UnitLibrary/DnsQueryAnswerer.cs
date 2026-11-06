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
/// <item>AXFR or IXFR: <c>REFUSED</c> while zone transfer is off, the default (ADR-0092 decision
/// 4); with it on, as <see cref="Answer"/> and <see cref="AnswerOverStream"/> say for their
/// transports, and <c>NOTAUTH</c> for a name that is no served zone's apex.</item>
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
    private readonly bool allowZoneTransfer;

    /// <summary>
    /// Creates an answerer that answers from <paramref name="zones"/>, refusing zone transfer
    /// unless <paramref name="allowZoneTransfer"/> (ADR-0092 decision 4's <c>--allow-zone-transfer</c>).
    /// </summary>
    public DnsQueryAnswerer(DnsZoneSet zones, bool allowZoneTransfer = false)
    {
        ArgumentNullException.ThrowIfNull(zones);
        this.zones = zones;
        this.allowZoneTransfer = allowZoneTransfer;
    }

    /// <summary>
    /// Returns the response to <paramref name="query"/> carried in a datagram, one whole message,
    /// or <see langword="null"/> when it is to be dropped unanswered. With zone transfer allowed,
    /// AXFR is <c>NOTIMP</c> (RFC 5936 section 4.2) and IXFR at a served apex is answered with the
    /// apex SOA alone, which tells the client to use TCP (RFC 1995 section 4).
    /// </summary>
    public DnsResponse? Answer(ReadOnlySpan<byte> query) => AnswerWith(query, TransferOverDatagram)?[0];

    /// <summary>
    /// Returns the responses to <paramref name="query"/> carried on a stream, in the order they
    /// are sent, or none when it is to be dropped unanswered: one, except that an allowed AXFR or
    /// IXFR at a served apex is the whole zone in as many messages as it takes
    /// (<c>DnsZoneTransfer</c>; ADR-0092 decision 4).
    /// </summary>
    public IReadOnlyList<DnsResponse> AnswerOverStream(ReadOnlySpan<byte> query) => AnswerWith(query, TransferOverStream) ?? [];

    /// <summary>
    /// Returns the response to <paramref name="query"/> carried in one DNS over HTTPS request, or
    /// <see langword="null"/> when it is to be dropped, which DoH answers <c>400</c>. With zone
    /// transfer allowed, AXFR and IXFR are both <c>NOTIMP</c>: RFC 8484 defines one response per
    /// request and no answer of several messages (ADR-0092 decision 4).
    /// </summary>
    public DnsResponse? AnswerOverHttp(ReadOnlySpan<byte> query) => AnswerWith(query, TransferOverHttp)?[0];

    /// <summary>
    /// Reads <paramref name="query"/> as a standard query with one question it could look up: a
    /// readable query header with opcode QUERY, a message read whole with QDCOUNT 1, and at most
    /// one OPT record, owned by the root. Every other message is answered by the checks the
    /// remarks list, so no fault rule is asked about it (<see cref="DnsQueryFaults"/>).
    /// </summary>
    /// <param name="query">The message as received.</param>
    /// <returns>Its header, question and OPT, or <see langword="null"/> when it is not such a query.</returns>
    internal static DnsStandardQuery? StandardQueryOf(ReadOnlySpan<byte> query)
    {
        var read = DnsMessageReader.Read(query);
        if (read.Header is not { IsResponse: false, Opcode: DnsOpcode.Query } header
            || read.Message is not { Questions.Count: 1 } message)
        {
            return null;
        }

        var queryOpt = OptOf(message, out var optIsReadable);

        return optIsReadable ? new DnsStandardQuery(header, message.Questions[0], queryOpt) : null;
    }

    /// <summary>
    /// The answer to <paramref name="query"/> with the chosen <paramref name="responseCode"/> and
    /// nothing else: its ID, opcode and RD echoed, QR = 1, AA = 0, RA = 0, its question echoed, and
    /// an OPT exactly when the query had one, as every other answer.
    /// </summary>
    /// <param name="query">The standard query, as <see cref="StandardQueryOf"/> read it.</param>
    /// <param name="responseCode">The RCODE to answer with.</param>
    /// <returns>The response.</returns>
    internal static DnsResponse ChosenError(DnsStandardQuery query, DnsResponseCode responseCode) =>
        Fault(query.Header, [query.Question], responseCode, query.Opt);

    private IReadOnlyList<DnsResponse>? AnswerWith(ReadOnlySpan<byte> query, Func<DnsHeader, DnsQuestion, DnsOptRecord?, IReadOnlyList<DnsResponse>> answerTransfer)
    {
        var read = DnsMessageReader.Read(query);

        return read.Header is { IsResponse: false } header
            ? AnswerQueryHeader(BinaryPrimitives.ReadUInt16BigEndian(query[4..]), read, header, answerTransfer)
            : null;
    }

    // A message with a readable query header: NOTIMP, FORMERR or the question's answer.
    private IReadOnlyList<DnsResponse> AnswerQueryHeader(
        ushort questionCount,
        DnsReadOutcome read,
        DnsHeader header,
        Func<DnsHeader, DnsQuestion, DnsOptRecord?, IReadOnlyList<DnsResponse>> answerTransfer)
    {
        var echoed = EchoedQuestions(questionCount, read.Questions);
        var queryOpt = OptOf(read.Message, out var optIsReadable);

        if (header.Opcode != DnsOpcode.Query)
        {
            return [Fault(header, echoed, DnsResponseCode.NotImplemented, queryOpt)];
        }

        if (read.Message is null || !optIsReadable)
        {
            return [Fault(header, echoed, DnsResponseCode.FormatError, null)];
        }

        return questionCount == 1
            ? AnswerQuestion(header, echoed[0], queryOpt, answerTransfer)
            : [Fault(header, [], DnsResponseCode.FormatError, queryOpt)];
    }

    // The one question a fault echoes: only when QDCOUNT is 1 and that question was read.
    private static IReadOnlyList<DnsQuestion> EchoedQuestions(ushort questionCount, IReadOnlyList<DnsQuestion> read) =>
        questionCount == 1 && read.Count == 1 ? read : [];

    private IReadOnlyList<DnsResponse> AnswerQuestion(
        DnsHeader header,
        DnsQuestion question,
        DnsOptRecord? queryOpt,
        Func<DnsHeader, DnsQuestion, DnsOptRecord?, IReadOnlyList<DnsResponse>> answerTransfer)
    {
        if (queryOpt is { Version: > 0 })
        {
            return [Fault(header, [question], (DnsResponseCode)(BadVersion & 0xF), queryOpt, BadVersion >> 4)];
        }

        if (question.Type is not (DnsRecordType.AXFR or DnsRecordType.IXFR))
        {
            return [Respond(header, question, zones.Answer(question), queryOpt)];
        }

        return allowZoneTransfer
            ? answerTransfer(header, question, queryOpt)
            : [Respond(header, question, DnsAuthoritativeAnswer.Refused, queryOpt)];
    }

    // AXFR is NOTIMP; IXFR at a served apex is its SOA alone, else NOTAUTH.
    private IReadOnlyList<DnsResponse> TransferOverDatagram(DnsHeader header, DnsQuestion question, DnsOptRecord? queryOpt)
    {
        if (question.Type == DnsRecordType.AXFR)
        {
            return [Fault(header, [question], DnsResponseCode.NotImplemented, queryOpt)];
        }

        var answer = ZoneToTransfer(question) is { } zone
            ? new DnsAuthoritativeAnswer(DnsResponseCode.NoError, true, [zone.StartOfAuthority], [], [])
            : NotAuthoritative;

        return [Respond(header, question, answer, queryOpt)];
    }

    // AXFR and IXFR are both NOTIMP: DoH has one response per request.
    private static IReadOnlyList<DnsResponse> TransferOverHttp(DnsHeader header, DnsQuestion question, DnsOptRecord? queryOpt) =>
        [Fault(header, [question], DnsResponseCode.NotImplemented, queryOpt)];

    // AXFR and IXFR at a served apex are the whole zone, else NOTAUTH.
    private IReadOnlyList<DnsResponse> TransferOverStream(DnsHeader header, DnsQuestion question, DnsOptRecord? queryOpt) =>
        ZoneToTransfer(question) is { } zone
            ? DnsZoneTransfer.Messages(header, question, zone, ResponseOptFor(queryOpt, 0), queryOpt)
            : [Respond(header, question, NotAuthoritative, queryOpt)];

    // The served zone whose apex the question names, in class IN or ANY (RFC 5936 section 2.2.1).
    private DnsZone? ZoneToTransfer(DnsQuestion question) =>
        question.Class is DnsClasses.Internet or DnsClasses.Any ? zones.ZoneAtApex(question.Name) : null;

    private static DnsAuthoritativeAnswer NotAuthoritative { get; } = new(DnsResponseCode.NotAuthoritative, false, [], [], []);

    private static DnsResponse Respond(DnsHeader header, DnsQuestion question, DnsAuthoritativeAnswer answer, DnsOptRecord? queryOpt) =>
        new(answer.ToResponse(header, question), ResponseOptFor(queryOpt, 0), queryOpt);

    // The query's one OPT record, if any; readable is false when it has more than one, or one
    // whose owner is not the root (RFC 6891 section 6.1.1).
    private static DnsOptRecord? OptOf(DnsMessage? message, out bool readable)
    {
        var opts = OptRecordsOf(message);
        readable = opts.Length <= 1 && opts.All(IsOwnedByTheRoot);
        return readable && opts.Length == 1 ? DnsOptRecord.FromResourceRecord(opts[0]) : null;
    }

    private static DnsResourceRecord[] OptRecordsOf(DnsMessage? message) =>
        message is null ? [] : message.Additionals.Where(IsOpt).ToArray();

    private static bool IsOpt(DnsResourceRecord record) => record.Type == DnsRecordType.OPT;

    private static bool IsOwnedByTheRoot(DnsResourceRecord record) => record.Name.LabelCount == 0;

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
