namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The arguments of <c>PUB &lt;subject&gt; [reply-to] &lt;#bytes&gt;</c> or
/// <c>HPUB &lt;subject&gt; [reply-to] &lt;#header bytes&gt; &lt;#total bytes&gt;</c> (the protocol
/// page; ADR-0113 decision 4).
/// </summary>
/// <param name="Subject">The subject, as the client sent it.</param>
/// <param name="ReplyTo">The reply-to subject; null when none was given.</param>
/// <param name="HeaderBytes">The size of the header block; 0 for PUB.</param>
/// <param name="TotalBytes">The size of the header block and payload together.</param>
internal sealed record NatsPublishArguments(string Subject, string? ReplyTo, long HeaderBytes, long TotalBytes)
{
    /// <summary>Reads PUB's arguments (<paramref name="withHeaders"/> false) or HPUB's.</summary>
    /// <exception cref="NatsProtocolViolationException">The wrong number of fields, a size that is
    /// not a non-negative decimal integer, or a header size above the total: <c>'Parser Error'</c>.</exception>
    public static NatsPublishArguments Parse(string arguments, bool withHeaders) =>
        withHeaders ? ParseHpub(NatsOperationFields.Split(arguments)) : ParsePub(NatsOperationFields.Split(arguments));

    private static NatsPublishArguments ParsePub(string[] fields) => fields.Length switch
    {
        2 => new NatsPublishArguments(fields[0], null, 0, NatsOperationFields.ReadCount("PUB", fields[1])),
        3 => new NatsPublishArguments(fields[0], fields[1], 0, NatsOperationFields.ReadCount("PUB", fields[2])),
        _ => throw NatsOperationFields.WrongFieldCount("PUB", fields.Length),
    };

    private static NatsPublishArguments ParseHpub(string[] fields) => fields.Length switch
    {
        3 => ParseHeaderSizes(fields[0], null, fields[1], fields[2]),
        4 => ParseHeaderSizes(fields[0], fields[1], fields[2], fields[3]),
        _ => throw NatsOperationFields.WrongFieldCount("HPUB", fields.Length),
    };

    private static NatsPublishArguments ParseHeaderSizes(string subject, string? replyTo, string headerField, string totalField)
    {
        var header = NatsOperationFields.ReadCount("HPUB", headerField);
        var total = NatsOperationFields.ReadCount("HPUB", totalField);
        return header <= total
            ? new NatsPublishArguments(subject, replyTo, header, total)
            : throw new NatsProtocolViolationException(
                NatsErrors.ParserError, $"HPUB's header size {header} was above its total size {total}");
    }
}
