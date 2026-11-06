using System.Globalization;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// Turns bus messages into MESSAGE frames and SEND frames into bus messages (ADR-0118 decisions 4
/// and 5; ADR-0100 decision 3.4).
/// </summary>
internal static class StompMessageFrames
{
    // STOMP's own headers on SEND: never passed on to the bus as string headers.
    private static readonly HashSet<string> SendHeaders = new(StringComparer.Ordinal)
    {
        "destination", "content-length", "content-type", "receipt", "transaction",
    };

    // STOMP's own headers on MESSAGE: a bus header so named is never written.
    private static readonly HashSet<string> MessageHeaders = new(StringComparer.Ordinal)
    {
        "destination", "message-id", "subscription", "ack", "content-type", "content-length", "receipt", "transaction",
    };

    /// <summary>
    /// The bus message a SEND publishes: at quality of service 1, the body exact, its
    /// <c>content-type</c> as the content type and every other header but STOMP's own as a string
    /// header, names and order kept.
    /// </summary>
    /// <param name="topic">The canonical topic, or for a queue its name.</param>
    /// <param name="send">The SEND frame.</param>
    public static BusMessage ToBusMessage(string topic, StompFrame send) => new(topic, send.Body)
    {
        QualityOfService = BusQualityOfService.AtLeastOnce,
        ContentType = send.Header("content-type"),
        Headers = [.. send.Headers.Where(header => !SendHeaders.Contains(header.Key)).Select(header => new BusMessageHeader(header.Key, header.Value))],
    };

    /// <summary>
    /// The MESSAGE frame for <paramref name="message"/>: <c>destination</c>, <c>message-id</c>,
    /// <c>subscription</c>, <c>ack</c> (1.2 under a client mode), <c>content-type</c> when known,
    /// <c>content-length</c>, then the bus headers the version can carry, and the body exact.
    /// </summary>
    /// <param name="version">The connection's version.</param>
    /// <param name="destination">The STOMP destination, <c>/topic/&lt;t&gt;</c> or <c>/queue/&lt;q&gt;</c>.</param>
    /// <param name="messageId">The connection's next message id.</param>
    /// <param name="subscription">The subscription it is delivered to.</param>
    /// <param name="message">The bus message.</param>
    public static StompFrame ToMessageFrame(StompVersion version, string destination, long messageId, StompSubscription subscription, BusMessage message)
    {
        var id = messageId.ToString(CultureInfo.InvariantCulture);
        var headers = new List<KeyValuePair<string, string>>
        {
            new("destination", destination),
            new("message-id", id),
            new("subscription", subscription.Id),
        };
        if (version == StompVersion.V1_2 && subscription.AckMode != StompAckMode.Auto)
        {
            headers.Add(new("ack", id));
        }

        if (message.ContentType is { } contentType)
        {
            headers.Add(new("content-type", contentType));
        }

        headers.Add(new("content-length", message.Payload.Length.ToString(CultureInfo.InvariantCulture)));
        headers.AddRange(message.Headers.Where(header => IsWritable(header, version)).Select(header => new KeyValuePair<string, string>(header.Name, header.Value)));
        return new StompFrame("MESSAGE", headers, message.Payload);
    }

    // A header named like STOMP's own, or holding U+0000, is never written; 1.0 cannot escape a
    // colon in a name or a line break anywhere.
    private static bool IsWritable(BusMessageHeader header, StompVersion version) =>
        !MessageHeaders.Contains(header.Name)
        && !header.Name.Contains('\0', StringComparison.Ordinal)
        && !header.Value.Contains('\0', StringComparison.Ordinal)
        && (version != StompVersion.V1_0 || IsWritableUnescaped(header));

    private static bool IsWritableUnescaped(BusMessageHeader header) =>
        header.Name.AsSpan().IndexOfAny(":\r\n") < 0 && header.Value.AsSpan().IndexOfAny("\r\n") < 0;
}
