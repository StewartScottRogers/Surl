using System.Buffers.Binary;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// An Engine.IO client written by hand from RFC 6455, EIO and SIO, driving a
/// <see cref="SocketIoServerHarness"/>'s server through a <see cref="UnitTestDuplexConnection"/>:
/// the WebSocket upgrade, masked client frames, and the server's frames read back.
/// </summary>
internal sealed class SocketIoClient
{
    /// <summary>RFC 6455 section 1.3's sample key, and the accept value it gives.</summary>
    public const string Key = "dGhlIHNhbXBsZSBub25jZQ==";

    public const string Accept = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";

    public const string WebSocketTarget = "/socket.io/?EIO=4&transport=websocket";

    // RFC 6455 section 5.7's masking key.
    private static readonly byte[] MaskingKey = [0x37, 0xfa, 0x21, 0x3d];

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private SocketIoClient(UnitTestDuplexConnection connection, Task serving)
    {
        Connection = connection;
        Serving = serving;
    }

    public UnitTestDuplexConnection Connection { get; }

    public Task Serving { get; }

    /// <summary>
    /// The upgrade request for <paramref name="target"/>.
    /// </summary>
    public static string UpgradeRequest(string target = WebSocketTarget, string extraFields = "") =>
        $"GET {target} HTTP/1.1\r\nHost: 127.0.0.1:5000\r\n{extraFields}Upgrade: websocket\r\nConnection: Upgrade\r\n"
        + $"Sec-WebSocket-Key: {Key}\r\nSec-WebSocket-Version: 13\r\n\r\n";

    /// <summary>
    /// Upgrades and waits for the open packet.
    /// </summary>
    public static async Task<SocketIoClient> ConnectAsync(SocketIoServerHarness harness, ExchangeContext? context = null, UnitTestDuplexConnection? connection = null, string extraFields = "")
    {
        connection ??= new UnitTestDuplexConnection();
        connection.Send(Encoding.ASCII.GetBytes(UpgradeRequest(extraFields: extraFields)));
        var client = new SocketIoClient(connection, harness.Server.ServeAsync(connection, context ?? harness.Context()));
        await connection.WaitForWrittenAsync(bytes => HeadLength(bytes) > 0 && DataFrames(bytes).Count > 0);

        return client;
    }

    /// <summary>
    /// Upgrades, then connects <paramref name="name"/> with <paramref name="payload"/>, and waits for the answer.
    /// </summary>
    public static async Task<SocketIoClient> ConnectNamespaceAsync(SocketIoServerHarness harness, string name = "/", string payload = "")
    {
        var client = await ConnectAsync(harness);
        client.SendText("40" + (name == "/" ? string.Empty : name + ",") + payload);
        await client.WaitForMessagesAsync(2);

        return client;
    }

    /// <summary>
    /// A masked client frame (RFC 6455 section 5.2) whose first byte is <paramref name="finAndOpcode"/>.
    /// </summary>
    public static byte[] ClientFrame(byte finAndOpcode, byte[] payload, bool masked = true)
    {
        var header = new List<byte> { finAndOpcode };
        var maskBit = masked ? (byte)0x80 : (byte)0;
        if (payload.Length < 126)
        {
            header.Add((byte)(maskBit | payload.Length));
        }
        else if (payload.Length <= ushort.MaxValue)
        {
            header.Add((byte)(maskBit | 126));
            var length = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)payload.Length);
            header.AddRange(length);
        }
        else
        {
            header.Add((byte)(maskBit | 127));
            var length = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(length, (ulong)payload.Length);
            header.AddRange(length);
        }

        if (!masked)
        {
            return [.. header, .. payload];
        }

        return [.. header, .. MaskingKey, .. payload.Select((value, index) => (byte)(value ^ MaskingKey[index % 4]))];
    }

    /// <summary>Sends <paramref name="text"/> as one text message.</summary>
    public void SendText(string text) => Connection.Send(ClientFrame(0x81, Encoding.UTF8.GetBytes(text)));

    /// <summary>Sends <paramref name="bytes"/> as one binary message.</summary>
    public void SendBinary(byte[] bytes) => Connection.Send(ClientFrame(0x82, bytes));

    /// <summary>The text and binary messages the server sent, text as its string and binary as <c>&lt;binary:hex&gt;</c>.</summary>
    public string[] Messages() => Describe(Connection.WrittenBytes);

    /// <summary>The open packet's JSON.</summary>
    public string OpenPacket() => Messages()[0];

    public async Task<string[]> WaitForMessagesAsync(int count)
    {
        var bytes = await Connection.WaitForWrittenAsync(written => DataFrames(written).Count >= count);

        return Describe(bytes);
    }

    /// <summary>Waits for the server's WebSocket Close and returns its code, or -1 for an empty Close.</summary>
    public async Task<int> WaitForCloseCodeAsync()
    {
        var bytes = await Connection.WaitForWrittenAsync(written => Frames(written).Any(frame => frame.Opcode == 0x8));
        var close = Frames(bytes).First(frame => frame.Opcode == 0x8);

        return close.Payload.Length < 2 ? -1 : BinaryPrimitives.ReadUInt16BigEndian(close.Payload);
    }

    /// <summary>Ends the client's side and waits for the server to finish with the connection.</summary>
    public async Task EndAsync()
    {
        Connection.HalfClose();
        await Serving.WaitAsync(WaitLimit);
    }

    /// <summary>The frames the server wrote after its response head, complete ones only.</summary>
    public static List<(int Opcode, byte[] Payload)> Frames(byte[] written)
    {
        var frames = new List<(int, byte[])>();
        var at = HeadLength(written);
        while (at > 0 && at + 2 <= written.Length)
        {
            var (headerLength, length) = PayloadLength(written, at);
            if (at + headerLength + length > written.Length)
            {
                break;
            }

            frames.Add((written[at] & 0x0f, written.AsSpan(at + headerLength, (int)length).ToArray()));
            at += headerLength + (int)length;
        }

        return frames;
    }

    private static (int HeaderLength, long Length) PayloadLength(byte[] written, int at)
    {
        var length = written[at + 1] & 0x7f;
        if (length == 126)
        {
            return at + 4 <= written.Length ? (4, BinaryPrimitives.ReadUInt16BigEndian(written.AsSpan(at + 2))) : (4, long.MaxValue / 2);
        }

        if (length == 127)
        {
            return at + 10 <= written.Length ? (10, (long)BinaryPrimitives.ReadUInt64BigEndian(written.AsSpan(at + 2))) : (10, long.MaxValue / 2);
        }

        return (2, length);
    }

    private static List<(int Opcode, byte[] Payload)> DataFrames(byte[] written) =>
        [.. Frames(written).Where(frame => frame.Opcode is 0x1 or 0x2)];

    private static string[] Describe(byte[] written) =>
        [.. DataFrames(written).Select(frame => frame.Opcode == 0x1 ? Encoding.UTF8.GetString(frame.Payload) : "<binary:" + Convert.ToHexString(frame.Payload) + ">")];

    private static int HeadLength(byte[] written)
    {
        var end = written.AsSpan().IndexOf("\r\n\r\n"u8);

        return end < 0 ? 0 : end + 4;
    }
}
