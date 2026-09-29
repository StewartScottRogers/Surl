using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// What the limit tests share: the exchange context they serve under and the client packets
/// too long for <see cref="ClientPackets.Packet"/>.
/// </summary>
internal static class MqttTestExchange
{
    public static readonly byte[] ConnackAccepted = [0x20, 0x02, 0x00, 0x00];

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null) => new(
            1,
            new ListenUrl("mqtt", "127.0.0.1", 18883).WithBoundPort(18883),
            new IPEndPoint(IPAddress.Loopback, 18883),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    /// <summary>
    /// A packet of any length, its remaining length encoded as MQTT 3.1.1 section 2.2.3 says.
    /// </summary>
    public static byte[] LongPacket(byte firstByte, byte[] body)
    {
        var remainingLength = new List<byte>();
        var length = body.Length;
        do
        {
            var encodedByte = (byte)(length % 128);
            length /= 128;
            remainingLength.Add(length > 0 ? (byte)(encodedByte | 0x80) : encodedByte);
        }
        while (length > 0);

        return [firstByte, .. remainingLength, .. body];
    }

    /// <summary>A <c>PUBLISH</c> to <c>t</c> of <paramref name="payloadBytes"/> bytes of <c>x</c>, at QoS 0 or, with a packet identifier of 1, QoS 1.</summary>
    public static byte[] PublishOf(int payloadBytes, int qualityOfService = 0)
    {
        byte[] packetIdentifier = qualityOfService > 0 ? [0x00, 0x01] : [];
        byte[] body = [.. ClientPackets.String("t"), .. packetIdentifier, .. Enumerable.Repeat((byte)'x', payloadBytes)];

        return LongPacket((byte)(0x30 | (qualityOfService << 1)), body);
    }
}
