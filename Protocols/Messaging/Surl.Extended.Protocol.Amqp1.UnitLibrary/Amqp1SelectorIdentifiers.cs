using System.Collections.Frozen;
using System.Globalization;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// What a selector's identifiers read (ADR-0146 decision 2.2, after the AMQP JMS Mapping draft):
/// <c>JMSMessageID</c> the <c>message-id</c>, <c>JMSCorrelationID</c> the <c>correlation-id</c>,
/// <c>JMSType</c> the <c>subject</c>, <c>JMSPriority</c> the header's <c>priority</c> (4 when there is no
/// header), <c>JMSDeliveryMode</c> <c>'PERSISTENT'</c> when the header's <c>durable</c> is true and
/// <c>'NON_PERSISTENT'</c> otherwise, <c>JMSTimestamp</c> the <c>creation-time</c> and
/// <c>JMSExpiration</c> the <c>absolute-expiry-time</c> in milliseconds; any other identifier the
/// application property of that name. A field or property the message lacks is NULL.
/// </summary>
internal static class Amqp1SelectorIdentifiers
{
    /// <summary>The value <c>JMSDeliveryMode</c> has when the header's <c>durable</c> is true.</summary>
    public const string Persistent = "PERSISTENT";

    /// <summary>The value <c>JMSDeliveryMode</c> has otherwise.</summary>
    public const string NonPersistent = "NON_PERSISTENT";

    private static readonly FrozenDictionary<string, Func<IAmqp1SelectorMessage, object?>> HeaderFields =
        new Dictionary<string, Func<IAmqp1SelectorMessage, object?>>(StringComparer.Ordinal)
        {
            ["JMSMessageID"] = message => IdentifierText(message.Properties?.MessageId),
            ["JMSCorrelationID"] = message => IdentifierText(message.Properties?.CorrelationId),
            ["JMSType"] = message => message.Properties?.Subject,
            ["JMSPriority"] = message => (long)(message.Header?.Priority ?? Amqp1Header.DefaultPriority),
            ["JMSDeliveryMode"] = message => message.Header?.Durable == true ? Persistent : NonPersistent,
            ["JMSTimestamp"] = message => message.Properties?.CreationTime?.MillisecondsSinceUnixEpoch,
            ["JMSExpiration"] = message => message.Properties?.AbsoluteExpiryTime?.MillisecondsSinceUnixEpoch,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Reads an identifier's value from a message.</summary>
    /// <param name="name">The identifier, matched exactly.</param>
    /// <param name="message">The message.</param>
    /// <returns>The selector value; NULL when the message lacks it.</returns>
    public static object? Read(string name, IAmqp1SelectorMessage message) =>
        HeaderFields.TryGetValue(name, out var read)
            ? read(message)
            : Amqp1SelectorValues.Normalise(message.ApplicationProperty(name));

    /// <summary>
    /// A <c>message-id</c> or <c>correlation-id</c> as text: a string as it is, and the other three
    /// types with the AMQP JMS Mapping draft's prefixes, <c>ID:AMQP_ULONG:</c>, <c>ID:AMQP_UUID:</c> and
    /// <c>ID:AMQP_BINARY:</c> (upper-case hexadecimal).
    /// </summary>
    /// <param name="identifier">The identifier as decoded.</param>
    /// <returns>The text; NULL when there is none.</returns>
    private static string? IdentifierText(object? identifier) => identifier switch
    {
        string text => text,
        ulong number => "ID:AMQP_ULONG:" + number.ToString(CultureInfo.InvariantCulture),
        Guid uuid => "ID:AMQP_UUID:" + uuid.ToString("D", CultureInfo.InvariantCulture),
        byte[] binary => "ID:AMQP_BINARY:" + Convert.ToHexString(binary),
        _ => null,
    };
}
