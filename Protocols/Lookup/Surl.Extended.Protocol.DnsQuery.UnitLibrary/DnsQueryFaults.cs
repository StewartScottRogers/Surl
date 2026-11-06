using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Asks the exchange's fault plan, for each standard query before it is answered, whether to
/// answer it with a chosen RCODE instead (ADR-0124, decisions 4 and 9): the point names the
/// scheme (<c>dns</c>), the QTYPE as <c>command</c> (<c>A</c>, <c>MX</c>, any type without a name
/// as RFC 3597's <c>TYPE</c><i>n</i>) and the QNAME as <c>path</c> (<c>web.example.test</c>,
/// ASCII letters in lower case and no trailing dot, <c>.</c> for the root). DNS has no account,
/// so the point names none. Every transport the server answers asks it: UDP, TCP, DNS over QUIC
/// and DNS over HTTPS.
/// </summary>
/// <remarks>
/// Only a message <see cref="DnsQueryAnswerer.StandardQueryOf"/> reads is asked about; one it does
/// not is answered by the server's own checks. Only a <see cref="FaultKind.Error"/> rule whose
/// reply is an RCODE <c>1</c> to <c>5</c> (<see cref="TryParse"/>) is applied, and applying it
/// publishes one <see cref="FaultAppliedEvent"/> naming the QNAME. A rule of any other kind, or a
/// reply that does not fit, is noted as not applied and the query is answered as without it.
/// </remarks>
internal static class DnsQueryFaults
{
    /// <summary>
    /// Finds the chosen error answer to a query, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream, log and listen URL.</param>
    /// <param name="query">The message as received.</param>
    /// <returns>The chosen answer, or <see langword="null"/> to answer as usual.</returns>
    public static async Task<DnsResponse?> FindErrorAsync(ExchangeContext context, ReadOnlyMemory<byte> query)
    {
        if (DnsQueryAnswerer.StandardQueryOf(query.Span) is not { } standard)
        {
            return null;
        }

        var path = PathOf(standard.Question.Name);
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Path = path,
            Command = CommandOf(standard.Question.Type),
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { } rule)
        {
            return null;
        }

        if (rule.Kind != FaultKind.Error || !TryParse(rule.Reply, out var responseCode))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply is not an RCODE 1 to 5" : $"a {rule.Kind} rule is not applied at a query";
            context.Log.Note($"Fault rule {rule.Id} not applied to the query: {why}.");

            return null;
        }

        context.Log.Note($"The query was answered RCODE {(int)responseCode} by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path));

        return DnsQueryAnswerer.ChosenError(standard, responseCode);
    }

    /// <summary>
    /// The QTYPE as a fault rule's <c>command</c> names it: its mnemonic when it has one here
    /// (<see cref="DnsRecordType"/>), and RFC 3597 section 5's <c>TYPE</c><i>n</i> otherwise.
    /// </summary>
    /// <param name="type">A QTYPE.</param>
    /// <returns>The command.</returns>
    public static string CommandOf(DnsRecordType type) =>
        Enum.IsDefined(type) ? type.ToString() : $"TYPE{(ushort)type}";

    /// <summary>
    /// The QNAME as a fault rule's <c>path</c> names it: its labels joined by dots, ASCII letters
    /// in lower case since names compare without case (RFC 4343), and <c>.</c> for the root.
    /// </summary>
    /// <param name="name">A QNAME.</param>
    /// <returns>The path.</returns>
    public static string PathOf(DnsName name)
    {
        if (name.LabelCount == 0)
        {
            return ".";
        }

        var text = name.ToString();

        return string.Create(text.Length - 1, text, static (path, source) =>
        {
            for (var index = 0; index < path.Length; index++)
            {
                path[index] = char.IsAsciiLetterUpper(source[index]) ? (char)(source[index] | 0x20) : source[index];
            }
        });
    }

    /// <summary>
    /// Reads a reply in the <c>dns</c> error shape: one digit, an RCODE from <c>1</c> (FORMERR) to
    /// <c>5</c> (REFUSED), RFC 1035 section 4.1.1's error codes.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <param name="responseCode">The RCODE the reply names.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryParse(string? reply, out DnsResponseCode responseCode)
    {
        var fits = reply is { Length: 1 } && reply[0] is >= '1' and <= '5';
        responseCode = fits ? (DnsResponseCode)(reply![0] - '0') : DnsResponseCode.NoError;

        return fits;
    }
}
