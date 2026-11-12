using System.Buffers.Binary;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A hub client written by hand from RFC 6455 and HubProtocol.md, driving a
/// <see cref="SignalRServerHarness"/>'s server through a <see cref="UnitTestDuplexConnection"/>:
/// the WebSocket upgrade, masked client frames, and the server's frames read back.
/// </summary>
internal sealed class SignalRHubClient
{
    /// <summary>RFC 6455 section 1.3's sample key, and the accept value it gives.</summary>
    public const string Key = "dGhlIHNhbXBsZSBub25jZQ==";

    public const string Accept = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";

    public const string HandshakeRequest = "{\"protocol\":\"json\",\"version\":1}\u001e";

    public const string HandshakeResponse = "{}\u001e";

    public const char RecordSeparator = '\u001e';

    // RFC 6455 section 5.7's masking key.
    private static readonly byte[] MaskingKey = [0x37, 0xfa, 0x21, 0x3d];

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private SignalRHubClient(UnitTestDuplexConnection connection, Task serving)
    {
        Connection = connection;
        Serving = serving;
    }

    public UnitTestDuplexConnection Connection { get; }

    public Task Serving { get; }

    /// <summary>
    /// The upgrade request for <paramref name="target"/>, with the bearer token.
    /// </summary>
    public static string UpgradeRequest(string target, string authorization = SignalRServerHarness.BearerField) =>
        $"GET {target} HTTP/1.1\r\nHost: 127.0.0.1:5000\r\n{authorization}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
        + $"Sec-WebSocket-Key: {Key}\r\nSec-WebSocket-Version: 13\r\n\r\n";

    /// <summary>
    /// Upgrades <c>/chat</c> with no <c>id</c> and, unless told not to, completes the handshake.
    /// </summary>
    public static async Task<SignalRHubClient> ConnectAsync(SignalRServerHarness harness, ExchangeContext? context = null, bool completesHandshake = true, string target = "/chat")
    {
        var connection = new UnitTestDuplexConnection();
        connection.Send(Encoding.ASCII.GetBytes(UpgradeRequest(target)));
        var client = new SignalRHubClient(connection, harness.Server.ServeAsync(connection, context ?? harness.Context()));
        await connection.WaitForWrittenAsync(bytes => HeadLength(bytes) > 0);
        if (completesHandshake)
        {
            client.SendText(HandshakeRequest);
            await client.WaitForRecordsAsync(1);
        }

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
        else
        {
            header.Add((byte)(maskBit | 126));
            header.Add((byte)(payload.Length >> 8));
            header.Add((byte)payload.Length);
        }

        if (!masked)
        {
            return [.. header, .. payload];
        }

        return [.. header, .. MaskingKey, .. payload.Select((value, index) => (byte)(value ^ MaskingKey[index % 4]))];
    }

    /// <summary>
    /// Upgrades <c>/chat</c>, then completes the handshake asking for <c>messagepack</c>.
    /// </summary>
    public static async Task<SignalRHubClient> ConnectMessagePackAsync(SignalRServerHarness harness, ExchangeContext? context = null)
    {
        var client = await ConnectAsync(harness, context, completesHandshake: false);
        client.SendText("{\"protocol\":\"messagepack\",\"version\":1}" + RecordSeparator);
        await client.WaitForRecordsAsync(1);

        return client;
    }

    /// <summary>Sends <paramref name="payload"/> as one binary message.</summary>
    public void SendBinary(byte[] payload) => Connection.Send(ClientFrame(0x82, payload));

    /// <summary>The binary messages the server sent, each with its length prefix.</summary>
    public byte[][] BinaryMessages() => [.. Frames(Connection.WrittenBytes).Where(frame => frame.Opcode == 0x2).Select(frame => frame.Payload)];

    public async Task<byte[][]> WaitForBinaryMessagesAsync(int count)
    {
        await Connection.WaitForWrittenAsync(bytes => Frames(bytes).Count(frame => frame.Opcode == 0x2) >= count);

        return BinaryMessages();
    }

    /// <summary>Sends <paramref name="text"/> as one text message.</summary>
    public void SendText(string text) => Connection.Send(ClientFrame(0x81, Encoding.UTF8.GetBytes(text)));

    /// <summary>Sends each record, a <c>0x1E</c> after each, in one text message.</summary>
    public void SendRecords(params string[] records) => SendText(string.Concat(records.Select(record => record + RecordSeparator)));

    /// <summary>The text messages the server sent, each one record with its <c>0x1E</c>.</summary>
    public string[] Records() => [.. Frames(Connection.WrittenBytes).Where(frame => frame.Opcode == 0x1).Select(frame => Encoding.UTF8.GetString(frame.Payload))];

    public async Task<string[]> WaitForRecordsAsync(int count)
    {
        await Connection.WaitForWrittenAsync(bytes => Frames(bytes).Count(frame => frame.Opcode == 0x1) >= count);

        return Records();
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
            var length = written[at + 1] & 0x7f;
            var headerLength = 2;
            if (length == 126)
            {
                length = at + 4 <= written.Length ? BinaryPrimitives.ReadUInt16BigEndian(written.AsSpan(at + 2)) : int.MaxValue;
                headerLength = 4;
            }

            if (at + headerLength + length > written.Length)
            {
                break;
            }

            frames.Add((written[at] & 0x0f, written.AsSpan(at + headerLength, length).ToArray()));
            at += headerLength + length;
        }

        return frames;
    }

    private static int HeadLength(byte[] written)
    {
        var end = written.AsSpan().IndexOf("\r\n\r\n"u8);

        return end < 0 ? 0 : end + 4;
    }
}
