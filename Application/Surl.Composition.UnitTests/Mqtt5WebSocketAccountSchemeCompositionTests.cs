using System.Text;
using Surl.Cli;
using Surl.Extended.Protocol.Mqtt5;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Which of <c>mqtt5</c>, <c>mqtt5s</c> and <c>mqtt5+ws</c> an account restricted by <c>protocols=</c> logs in on
/// (ADR-0102 decision 4.3, amendment of 2026-11-27, BL-1911), against the servers <c>surl</c> composes, with no
/// network. The packets are written from MQTT 5.0 sections 3.1 and 3.2 and the upgrade and frames from RFC 6455
/// sections 1.3 and 5.2.
/// </summary>
[TestClass]
public sealed class Mqtt5WebSocketAccountSchemeCompositionTests
{
    private const string UserName = "alice@example.com";

    private const string Password = "s3cret";

    private const byte Success = 0x00;

    private const byte BadUserNameOrPassword = 0x86;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("mqtt5+ws", "mqtt5+ws", Success)]
    [DataRow("mqtt5+ws", "mqtt5", BadUserNameOrPassword)]
    [DataRow("mqtt5+ws", "mqtt5s", BadUserNameOrPassword)]
    [DataRow("mqtt5", "mqtt5", Success)]
    [DataRow("mqtt5", "mqtt5s", Success)]
    [DataRow("mqtt5", "mqtt5+ws", BadUserNameOrPassword)]
    [DataRow("mqtt5,mqtt5+ws", "mqtt5+ws", Success)]
    [DataRow("mqtt5,mqtt5+ws", "mqtt5", Success)]
    public async Task ComposeAsync_AMailboxRestrictedByProtocols_AnswersTheConnectOnTheSchemeWithTheReasonCode(
        string restrictedTo, string connectsOver, byte reasonCode)
    {
        var connect = Connect();
        var isWebSocket = connectsOver == "mqtt5+ws";
        var connection = new FakeConnection(isWebSocket ? [.. Upgrade(), .. MaskedBinaryFrame(connect)] : connect);

        await ServeOneAsync(connection, connectsOver, restrictedTo);

        var written = connection.WrittenBytes;
        // Over WebSocket the CONNACK follows the 101 head, in a binary frame of two header bytes.
        var connack = isWebSocket ? written[(written.AsSpan().IndexOf("\r\n\r\n"u8) + 6)..] : written;
        Assert.AreEqual(0x20, connack[0]);
        Assert.AreEqual(reasonCode, connack[3]);
    }

    // Starts surl's composition with one mailbox restricted to the schemes, and serves the connection with the
    // server it composed for the scheme (mqtt5 and mqtt5s are one server).
    private async Task ServeOneAsync(FakeConnection connection, string scheme, string restrictedTo)
    {
        IReadOnlyList<IProtocolServer> servers = [];
        var seams = new StartTestSeams().Seams with
        {
            AreExtensionsTurnedOn = true,
            ComposeExtendedProtocolServers = services =>
            {
                servers = [.. ExtendedProtocolServers.Compose(services).Where(server => server is Mqtt5Broker or Mqtt5WebSocketServer)];
                return servers;
            },
        };
        string[] arguments =
        [
            "--mail-domain", "example.com", "--mailbox", $"{UserName}:{Password};protocols={restrictedTo}",
            "--allow-plaintext-auth", "mqtt5://127.0.0.1:0",
        ];
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(arguments, ["mqtt5", "mqtt5s", "mqtt5+ws"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var serving = start.Serving!;
        var context = new ExchangeContext(
            1,
            new ListenUrl(scheme, "127.0.0.1", FakeListenerFactory.BoundPort),
            connection.LocalEndPoint,
            connection.RemoteEndPoint,
            new RecordingExchangeLog(),
            TimeProvider.System,
            TestContext.CancellationToken);

        await servers.OfType<IConnectionProtocolServer>().Single(server => server.Schemes.Contains(scheme)).ServeAsync(connection, context);
    }

    // CONNECT 5.0, User Name and Password flags and Clean Start, Keep Alive 30, no properties, client "c".
    private static byte[] Connect()
    {
        byte[] body = [0, 4, .. "MQTT"u8, 5, 0xC2, 0, 30, 0, .. Text("c"), .. Text(UserName), .. Text(Password)];
        return [0x10, (byte)body.Length, .. body];
    }

    private static byte[] Text(string value) => [0, (byte)value.Length, .. Encoding.ASCII.GetBytes(value)];

    private static byte[] Upgrade() => Encoding.ASCII.GetBytes(
        "GET /mqtt HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
        + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Protocol: mqtt\r\n\r\n");

    // One final binary frame under the all-zero masking key, which leaves the payload as it is.
    private static byte[] MaskedBinaryFrame(byte[] payload) => [0x82, (byte)(0x80 | payload.Length), 0, 0, 0, 0, .. payload];
}
