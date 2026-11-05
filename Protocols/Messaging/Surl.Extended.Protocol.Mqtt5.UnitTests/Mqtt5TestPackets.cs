using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// Builds packets and property lists for the codec tests, and runs bytes through
/// <see cref="Mqtt5PacketReader"/> over an in-memory connection.
/// </summary>
internal static class Mqtt5TestPackets
{
    /// <summary>
    /// Every property table 2-4 allows in <paramref name="context"/>, each with a value of its
    /// type, and a repeatable property a second time.
    /// </summary>
    public static IReadOnlyList<Mqtt5Property> EveryAllowedProperty(Mqtt5PropertyContext context)
    {
        var properties = new List<Mqtt5Property>();
        foreach (var definition in Mqtt5PropertyDefinition.All.Where(definition => definition.IsAllowedIn(context)))
        {
            properties.Add(SampleOf(definition));
            if (definition.IsRepeatableIn(context))
            {
                properties.Add(SampleOf(definition));
            }
        }

        return properties;
    }

    /// <summary>
    /// A property with a value of its type.
    /// </summary>
    public static Mqtt5Property SampleOf(Mqtt5PropertyDefinition definition) => definition.Type switch
    {
        Mqtt5PropertyType.Byte => Mqtt5Property.FromInteger(definition.Identifier, 1),
        Mqtt5PropertyType.TwoByteInteger => Mqtt5Property.FromInteger(definition.Identifier, 0x1234),
        Mqtt5PropertyType.FourByteInteger => Mqtt5Property.FromInteger(definition.Identifier, 0x12345678),
        Mqtt5PropertyType.VariableByteInteger => Mqtt5Property.FromInteger(definition.Identifier, 268_435_455),
        Mqtt5PropertyType.Utf8String => Mqtt5Property.FromText(definition.Identifier, "text-é"),
        Mqtt5PropertyType.BinaryData => Mqtt5Property.FromBinary(definition.Identifier, [1, 2, 3]),
        _ => Mqtt5Property.FromUserProperty("name", "value"),
    };

    /// <summary>
    /// Reads one packet from <paramref name="chunks"/>, each delivered by its own read.
    /// </summary>
    public static async Task<Mqtt5PacketReadResult> ReadAsync(
        IEnumerable<byte[]> chunks,
        long maxPacketBytes = 0,
        long maxPublishPayloadBytes = 0)
    {
        var connection = new InMemoryConnection(chunks.Select(chunk => (ReadOnlyMemory<byte>)chunk));
        var reader = new Mqtt5PacketReader(connection, maxPacketBytes, maxPublishPayloadBytes);
        return await reader.ReadPacketAsync(CancellationToken.None);
    }

    /// <summary>
    /// Reads one packet from <paramref name="bytes"/>, delivered whole.
    /// </summary>
    public static Task<Mqtt5PacketReadResult> ReadAsync(byte[] bytes, long maxPacketBytes = 0, long maxPublishPayloadBytes = 0) =>
        ReadAsync([bytes], maxPacketBytes, maxPublishPayloadBytes);

    /// <summary>
    /// Splits <paramref name="bytes"/> into one-byte chunks.
    /// </summary>
    public static IEnumerable<byte[]> OneByteAtATime(byte[] bytes) => bytes.Select(value => new[] { value });

    /// <summary>
    /// The fault that decoding <paramref name="bytes"/> (a whole packet, one-byte Remaining
    /// Length) reports.
    /// </summary>
    public static Mqtt5PacketFault? FaultOf(params byte[] bytes) =>
        Mqtt5PacketDecoder.Decode(bytes[0], bytes.AsMemory(2)).Fault;

    /// <summary>
    /// A packet's bytes: <paramref name="firstByte"/>, a one-byte Remaining Length, then
    /// <paramref name="body"/>.
    /// </summary>
    public static byte[] Packet(byte firstByte, params byte[] body) => [firstByte, (byte)body.Length, .. body];
}
