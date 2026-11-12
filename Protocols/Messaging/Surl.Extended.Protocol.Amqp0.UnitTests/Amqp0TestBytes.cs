using System.Text;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// AMQP 0-9-1 bytes laid out by hand from sections 4.2.2 to 4.2.5 and the XML's connection class,
/// independently of the library's own writer, for the broker's tests.
/// </summary>
internal static class Amqp0TestBytes
{
    /// <summary>The protocol header: <c>A M Q P 0 0 9 1</c>.</summary>
    public static readonly byte[] ProtocolHeader = [0x41, 0x4D, 0x51, 0x50, 0x00, 0x00, 0x09, 0x01];

    /// <summary>A heartbeat frame on channel 0.</summary>
    public static readonly byte[] Heartbeat = [0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xCE];

    public static byte[] Short(int value) => [(byte)(value >> 8), (byte)value];

    public static byte[] Long(long value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    public static byte[] ShortString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return [(byte)bytes.Length, .. bytes];
    }

    public static byte[] LongString(byte[] bytes) => [.. Long(bytes.Length), .. bytes];

    public static byte[] LongString(string text) => LongString(Encoding.UTF8.GetBytes(text));

    /// <summary>A frame: type, channel, size, payload, <c>0xCE</c>.</summary>
    public static byte[] Frame(byte type, int channel, byte[] payload) =>
        [type, .. Short(channel), .. Long(payload.Length), .. payload, 0xCE];

    /// <summary>A method frame: class ID, method ID, then <paramref name="arguments"/>.</summary>
    public static byte[] Method(int channel, int classId, int methodId, params byte[] arguments) =>
        Frame(1, channel, [.. Short(classId), .. Short(methodId), .. arguments]);

    /// <summary><c>connection.start-ok</c> with empty client properties and locale <c>en_US</c>.</summary>
    public static byte[] StartOk(string mechanism, byte[] response) =>
        Method(0, 10, 11, [.. Long(0), .. ShortString(mechanism), .. LongString(response), .. ShortString("en_US")]);

    /// <summary>
    /// <c>connection.start-ok</c> whose client properties hold a <c>capabilities</c> table with
    /// <c>consumer_cancel_notify</c> set to <paramref name="announced"/>.
    /// </summary>
    public static byte[] StartOkAnnouncingConsumerCancel(string mechanism, byte[] response, bool announced = true)
    {
        byte[] capabilities = [.. ShortString("consumer_cancel_notify"), (byte)'t', (byte)(announced ? 1 : 0)];
        byte[] properties = [.. ShortString("capabilities"), (byte)'F', .. Long(capabilities.Length), .. capabilities];
        return Method(0, 10, 11, [.. Long(properties.Length), .. properties, .. ShortString(mechanism), .. LongString(response), .. ShortString("en_US")]);
    }

    /// <summary>A <c>PLAIN</c> response:<c>NUL user NUL password</c>.</summary>
    public static byte[] PlainResponse(string user, string password) => Encoding.UTF8.GetBytes($"\0{user}\0{password}");

    /// <summary>An <c>AMQPLAIN</c> response: <c>LOGIN</c> and <c>PASSWORD</c> long strings, no table size.</summary>
    public static byte[] AmqplainResponse(string login, string password) =>
        [.. ShortString("LOGIN"), (byte)'S', .. LongString(login), .. ShortString("PASSWORD"), (byte)'S', .. LongString(password)];

    public static byte[] SecureOk(string response) => Method(0, 10, 21, LongString(response));

    public static byte[] TuneOk(int channelMax, long frameMax, int heartbeat) =>
        Method(0, 10, 31, [.. Short(channelMax), .. Long(frameMax), .. Short(heartbeat)]);

    /// <summary><c>connection.open</c>: the virtual host, an empty <c>reserved-1</c> and a zero <c>reserved-2</c> bit.</summary>
    public static byte[] Open(string virtualHost) => Method(0, 10, 40, [.. ShortString(virtualHost), 0x00, 0x00]);

    /// <summary><c>connection.close</c> 200 from the client.</summary>
    public static byte[] Close() => Method(0, 10, 50, [.. Short(200), .. ShortString("bye"), .. Short(0), .. Short(0)]);

    public static byte[] CloseOk() => Method(0, 10, 51);

    /// <summary>
    /// <c>connection.start</c> as ADR-0112 decision 2 fixes it, offering <paramref name="mechanisms"/>.
    /// </summary>
    public static byte[] Start(string mechanisms)
    {
        byte[] capabilities =
        [
            .. ShortString("basic.nack"), (byte)'t', 1,
            .. ShortString("consumer_cancel_notify"), (byte)'t', 1,
            .. ShortString("authentication_failure_close"), (byte)'t', 1,
            .. ShortString("publisher_confirms"), (byte)'t', 1,
        ];
        byte[] serverProperties =
        [
            .. ShortString("product"), (byte)'S', .. LongString("surl"),
            .. ShortString("capabilities"), (byte)'F', .. Long(capabilities.Length), .. capabilities,
        ];
        return Method(0, 10, 10, [0, 9, .. Long(serverProperties.Length), .. serverProperties, .. LongString(mechanisms), .. LongString("en_US")]);
    }

    /// <summary><c>connection.tune</c>: channel-max 256, the frame-max, the heartbeat.</summary>
    public static byte[] Tune(long frameMax = 131072, int heartbeat = 60) =>
        Method(0, 10, 30, [.. Short(256), .. Long(frameMax), .. Short(heartbeat)]);

    /// <summary><c>connection.open-ok</c> with an empty <c>reserved-1</c>.</summary>
    public static byte[] OpenOk() => Method(0, 10, 41, 0x00);

    /// <summary><c>connection.close</c> from the broker.</summary>
    public static byte[] BrokerClose(int replyCode, string replyText, int classId, int methodId) =>
        Method(0, 10, 50, [.. Short(replyCode), .. ShortString(replyText), .. Short(classId), .. Short(methodId)]);

    public static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
