namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A response <see cref="DnsQueryAnswerer"/> made, before a transport writes it: the message
/// without its OPT record, the OPT record it carries, and the query's OPT record, whose payload
/// size bounds a UDP answer (RFC 6891 section 6.2.3). The OPT is kept apart so a transport that
/// truncates the message still sends it last in the additional section (RFC 6891 section 7).
/// </summary>
/// <param name="Message">The response, its additional section holding no OPT record.</param>
/// <param name="ResponseOpt">The OPT record the response carries, or <see langword="null"/> when the query carried none.</param>
/// <param name="QueryOpt">The query's OPT record, or <see langword="null"/> when it carried none.</param>
public sealed record DnsResponse(DnsMessage Message, DnsOptRecord? ResponseOpt, DnsOptRecord? QueryOpt)
{
    /// <summary>
    /// Returns <see cref="Message"/> with <see cref="ResponseOpt"/>, if any, appended to its
    /// additional section: the whole response as it goes on the wire.
    /// </summary>
    public DnsMessage ToMessage() => WithOpt(Message);

    /// <summary>Returns <paramref name="message"/> with <see cref="ResponseOpt"/>, if any, appended to its additional section.</summary>
    internal DnsMessage WithOpt(DnsMessage message) => ResponseOpt is null
        ? message
        : message with { Additionals = [.. message.Additionals, ResponseOpt.ToResourceRecord()] };
}
