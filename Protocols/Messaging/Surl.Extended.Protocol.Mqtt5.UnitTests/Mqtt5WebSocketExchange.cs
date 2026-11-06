using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// What the <c>mqtt5+ws</c> tests share: the client's upgrade request and frames, written from
/// RFC 6455 and MQTT 5.0 chapter 6, and the server's answer split into its HTTP head and frames.
/// </summary>
internal static class Mqtt5WebSocketExchange
{
    /// <summary>The masking key of RFC 6455 section 5.7's masked example.</summary>
    private static readonly byte[] MaskingKey = [0x37, 0xFA, 0x21, 0x3D];

    /// <summary>Opcode 0x0, continuation.</summary>
    public const byte Continuation = 0x0;

    /// <summary>Opcode 0x1, text.</summary>
    public const byte Text = 0x1;

    /// <summary>Opcode 0x2, binary.</summary>
    public const byte Binary = 0x2;

    /// <summary>Opcode 0x8, close.</summary>
    public const byte Close = 0x8;

    /// <summary>Opcode 0x9, ping.</summary>
    public const byte Ping = 0x9;

    /// <summary>Opcode 0xA, pong.</summary>
    public const byte Pong = 0xA;

    /// <summary>
    /// An upgrade request for <paramref name="path"/> offering <paramref name="subprotocols"/>
    /// (no <c>Sec-WebSocket-Protocol</c> field when <see langword="null"/>), with RFC 6455
    /// section 1.3's key.
    /// </summary>
    public static byte[] UpgradeRequest(string path = "/mqtt", string? subprotocols = "mqtt", string host = "Host: 127.0.0.1:8083\r\n") =>
        Encoding.ASCII.GetBytes(
            $"GET {path} HTTP/1.1\r\n{host}Upgrade: websocket\r\nConnection: Upgrade\r\n"
            + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n"
            + (subprotocols is null ? string.Empty : $"Sec-WebSocket-Protocol: {subprotocols}\r\n")
            + "\r\n");

    /// <summary>
    /// A client frame: <c>FIN</c> as given, the opcode, masked with RFC 6455's example key unless
    /// <paramref name="masked"/> is <see langword="false"/>, a payload under 126 bytes.
    /// </summary>
    public static byte[] ClientFrame(byte opcode, byte[] payload, bool fin = true, bool masked = true, byte rsvBits = 0)
    {
        var frame = new List<byte> { (byte)((fin ? 0x80 : 0x00) | rsvBits | opcode), (byte)((masked ? 0x80 : 0x00) | payload.Length) };
        if (!masked)
        {
            frame.AddRange(payload);
            return [.. frame];
        }

        frame.AddRange(MaskingKey);
        frame.AddRange(payload.Select((value, index) => (byte)(value ^ MaskingKey[index % 4])));
        return [.. frame];
    }

    /// <summary>A masked binary frame holding <paramref name="payload"/>.</summary>
    public static byte[] BinaryFrame(params byte[] payload) => ClientFrame(Binary, payload);

    /// <summary>The server's binary frame holding <paramref name="payload"/>: unmasked, unfragmented.</summary>
    public static byte[] ServerBinaryFrame(byte[] payload) => [0x82, (byte)payload.Length, .. payload];

    /// <summary>The server's close frame with <paramref name="code"/> and no reason.</summary>
    public static byte[] ServerClose(ushort code) => [0x88, 0x02, (byte)(code >> 8), (byte)code];

    /// <summary>
    /// Serves a connection whose client sends <paramref name="chunks"/>, one per read, then half-closes.
    /// </summary>
    public static async Task<InMemoryConnection> ServeAsync(ExchangeContext context, params byte[][] chunks)
    {
        var connection = Mqtt5BrokerExchange.ConnectionSending(staysOpen: false, chunks);
        await new Mqtt5WebSocketServer(Mqtt5BrokerExchange.Broker()).ServeAsync(connection, context);
        return connection;
    }

    /// <summary>The HTTP head the server wrote, up to and including its empty line.</summary>
    public static string Head(byte[] written) => Encoding.ASCII.GetString(written, 0, HeadLength(written));

    /// <summary>The bytes the server wrote after its HTTP head.</summary>
    public static byte[] AfterHead(byte[] written) => written[HeadLength(written)..];

    private static int HeadLength(byte[] written) =>
        written.AsSpan().IndexOf("\r\n\r\n"u8) + 4;
}
