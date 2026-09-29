using System.Text;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// Hand-built packets a client sends, for the cases no recording covers. Each is written out
/// from MQTT 3.1.1 directly, not with the server's own encoder.
/// </summary>
internal static class ClientPackets
{
    public static readonly byte[] PingRequest = [0xC0, 0x00];
    public static readonly byte[] Disconnect = [0xE0, 0x00];

    /// <summary>The CONNECT upstream curl 8.21.0 sends, with client identifier <c>curlTESTTEST</c>.</summary>
    public static byte[] CurlConnect() => Connect("MQTT", 4, 0x02, "curlTESTTEST");

    public static byte[] Connect(string protocolName, byte level, byte connectFlags, string clientIdentifier) =>
        Packet(0x10, [.. String(protocolName), level, connectFlags, 0x00, 0x3C, .. String(clientIdentifier)]);

    public static byte[] Publish(byte firstByte, string topic, string payload) =>
        Packet(firstByte, [.. String(topic), .. Encoding.UTF8.GetBytes(payload)]);

    public static byte[] Publish(byte firstByte, string topic, ushort packetIdentifier, string payload) =>
        Packet(firstByte, [.. String(topic), (byte)(packetIdentifier >> 8), (byte)packetIdentifier, .. Encoding.UTF8.GetBytes(payload)]);

    public static byte[] Subscribe(ushort packetIdentifier, params (string Filter, byte QualityOfService)[] subscriptions) =>
        Packet(0x82, [(byte)(packetIdentifier >> 8), (byte)packetIdentifier, .. subscriptions.SelectMany(s => (byte[])[.. String(s.Filter), s.QualityOfService])]);

    public static byte[] Unsubscribe(ushort packetIdentifier, params string[] filters) =>
        Packet(0xA2, [(byte)(packetIdentifier >> 8), (byte)packetIdentifier, .. filters.SelectMany(String)]);

    public static byte[] String(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return [(byte)(bytes.Length >> 8), (byte)bytes.Length, .. bytes];
    }

    /// <summary>A packet whose body is under 128 bytes, so its remaining length is one byte.</summary>
    public static byte[] Packet(byte firstByte, byte[] body)
    {
        if (body.Length > 127)
        {
            throw new ArgumentException("Use a recording for a body of 128 bytes or more.", nameof(body));
        }

        return [firstByte, (byte)body.Length, .. body];
    }

    public static byte[] Join(params byte[][] packets) => [.. packets.SelectMany(packet => packet)];
}
