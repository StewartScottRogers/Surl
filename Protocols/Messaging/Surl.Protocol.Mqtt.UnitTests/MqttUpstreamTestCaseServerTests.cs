using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttUpstreamTestCaseServerTests
{
    // Upstream test 1190: <data> "hello\n", and the CONNECT and SUBSCRIBE its <protocol> shows
    // curl sending (the client identifier's random part, which the case strips, made up here).
    private static readonly byte[] Hello = "hello\n"u8.ToArray();
    private static readonly byte[] Connect = Convert.FromHexString("101800044d5154540402003c000c6375726c4142434445464748");
    private static readonly byte[] Subscribe = Convert.FromHexString("8209000100043131393000");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructors_Null_Throw()
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello);

        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttUpstreamTestCaseReplies(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttUpstreamTestCaseServer(null!, new AnonymousAuthenticationPolicy()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttUpstreamTestCaseServer(replies, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new MqttUpstreamTestCaseServer(new MqttUpstreamTestCaseReplies(Hello), new AnonymousAuthenticationPolicy());
        var context = MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public void SchemesAndServerCommands_AreMqttAndMqttsAndNone()
    {
        var server = new MqttUpstreamTestCaseServer(new MqttUpstreamTestCaseReplies(Hello), new AnonymousAuthenticationPolicy());

        CollectionAssert.AreEqual(new[] { "mqtt", "mqtts" }, server.Schemes.ToArray());
        CollectionAssert.AreEquivalent(
            new[]
            {
                "Testnum", "error-CONNACK", "remlen-CONNACK", "PINGRESP-as-CONNACK", "PUBLISH-before-SUBACK", "short-PUBLISH",
                "DISCONNECT-malformed", "excessive-remaining",
            },
            MqttUpstreamTestCaseReplies.SupportedServerCommands.ToArray());
    }

    // Upstream cases 2203 (error-CONNACK 5), 1132 (remlen-CONNACK 3) and 2206 (PINGRESP-as-CONNACK TRUE):
    // the bytes written and the log line their <protocol> expects (ADR-0155 decisions 1 to 3).
    [TestMethod]
    [DataRow("error-CONNACK 5", "20020005", "server CONNACK 2 20020005\n")]
    [DataRow("error-CONNACK 1", "20020001", "server CONNACK 2 20020001\n")]
    [DataRow("remlen-CONNACK 3", "20030000", "server CONNACK 3 20030000\n")]
    [DataRow("PINGRESP-as-CONNACK TRUE", "d0020000", "server PINGRESP-as-CONNACK 2 d0020000\n")]
    public async Task ServeAsync_AConnackCommand_AnswersTheConnectWithItsBytesLogsThemAndEnds(string command, string written, string logged)
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello, ["Testnum 1", "# a comment", "", command]);
        var connection = new InMemoryConnection([Connect, Subscribe]);

        await new MqttUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(
            connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));

        Assert.AreEqual(written, Convert.ToHexStringLower(connection.WrittenBytes));
        Assert.AreEqual(
            "client CONNECT 18 00044d5154540402003c000c6375726c4142434445464748\n" + logged,
            Encoding.ASCII.GetString(replies.ReceivedBytes));
    }

    // Upstream cases 1194, 1195, 2207 and 3017 (ADR-0155 decisions 4 to 7): the bytes written and the log.
    [TestMethod]
    [DataRow("PUBLISH-before-SUBACK TRUE", "300c00043131393068656c6c6f0a" + "9003000100" + "e000",
        "server PUBLISH c 300c00043131393068656c6c6f0a\nserver SUBACK 3 9003000100\nserver DISCONNECT 0 e000\n")]
    [DataRow("short-PUBLISH TRUE", "300c00043131393068656c6c",
        "server PUBLISH c 300c00043131393068656c6c\n")]
    [DataRow("DISCONNECT-malformed TRUE", "9003000100" + "300c00043131393068656c6c6f0a" + "e0020000",
        "server SUBACK 3 9003000100\nserver PUBLISH c 300c00043131393068656c6c6f0a\nserver DISCONNECT-malformed 2 e0020000\n")]
    [DataRow("excessive-remaining TRUE", "9003000100" + "30ffffff8000043131393068656c6c6f0a" + "e000",
        "server SUBACK 3 9003000100\nserver PUBLISH c 30ffffff8000043131393068656c6c6f0a\nserver DISCONNECT 0 e000\n")]
    public async Task ServeAsync_ASubscribeCommand_WritesItsPacketsInOrderAndLogsThem(string command, string written, string logged)
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello, [command]);
        var connection = new InMemoryConnection([Connect, Subscribe]);

        await new MqttUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(
            connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));

        Assert.AreEqual("20020000" + written, Convert.ToHexStringLower(connection.WrittenBytes));
        Assert.AreEqual(
            "client CONNECT 18 00044d5154540402003c000c6375726c4142434445464748\nserver CONNACK 2 20020000\nclient SUBSCRIBE 9 000100043131393000\n" + logged,
            Encoding.ASCII.GetString(replies.ReceivedBytes));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task ServeAsync_ASubscribeCommandAndTooLittleFromTheClient_WritesNoMoreThanTheConnack(int case_)
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello, ["short-PUBLISH TRUE"]);
        ReadOnlyMemory<byte>[] packets = case_ switch
        {
            0 => [],
            1 => [Connect],
            2 => [Connect, Array.Empty<byte>()],
            _ => [Connect, Convert.FromHexString("820200")],
        };
        var connection = new InMemoryConnection(packets);

        await new MqttUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(
            connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));

        Assert.AreEqual(case_ == 0 ? string.Empty : "20020000", Convert.ToHexStringLower(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_AConnackCommandAndNoConnect_WritesNothing()
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello, ["error-CONNACK 5"]);
        var connection = new InMemoryConnection([]);

        await new MqttUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(
            connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsEmpty(replies.ReceivedBytes);
    }

    [TestMethod]
    [DataRow("PUBLISH-before-SUBACK FALSE", "servercmd-unsupported:PUBLISH-before-SUBACK")]
    [DataRow("short-PUBLISH no", "servercmd-unsupported:short-PUBLISH")]
    [DataRow("DISCONNECT-malformed no", "servercmd-unsupported:DISCONNECT-malformed")]
    [DataRow("excessive-remaining no", "servercmd-unsupported:excessive-remaining")]
    [DataRow("upgrade", "servercmd-unsupported:upgrade")]
    [DataRow("PINGRESP-as-CONNACK FALSE", "servercmd-unsupported:PINGRESP-as-CONNACK")]
    public void Constructor_AnUnsupportedCommandOrFlagValue_IsRefusedByItsSkipReason(string line, string reason)
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => new MqttUpstreamTestCaseReplies(Hello, [line]));

        Assert.Contains(reason, exception.Message);
    }

    [TestMethod]
    [DataRow("error-CONNACK")]
    [DataRow("error-CONNACK 0")]
    [DataRow("error-CONNACK 6")]
    [DataRow("remlen-CONNACK x")]
    [DataRow("remlen-CONNACK 128")]
    public void Constructor_AMissingOrOutOfRangeNumber_IsAFormatException(string line)
    {
        Assert.ThrowsExactly<FormatException>(() => new MqttUpstreamTestCaseReplies(Hello, [line]));
    }

    [TestMethod]
    public void Constructor_NullServerCommands_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttUpstreamTestCaseReplies(Hello, null!));
    }

    [TestMethod]
    public async Task ServeAsync_Test1190Subscribe_DeliversTheCaseDataAndLogsEveryPacketAsUpstreamsServerDoes()
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello);
        var connection = new InMemoryConnection([Connect, Subscribe]);

        await new MqttUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(
            connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));

        // Surl's PUBLISH has RETAIN set (0x31), as MQTT 3.1.1 section 3.3.1.3 requires of a
        // message sent because a subscription was made; upstream's test server sends 0x30.
        Assert.AreEqual(
            "client CONNECT 18 00044d5154540402003c000c6375726c4142434445464748\n"
            + "server CONNACK 2 20020000\n"
            + "client SUBSCRIBE 9 000100043131393000\n"
            + "server SUBACK 3 9003000100\n"
            + "server PUBLISH c 310c00043131393068656c6c6f0a\n"
            + "server DISCONNECT 0 e000\n",
            Encoding.ASCII.GetString(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ClientDisconnect_LogsItsWholePacket()
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello);
        var connection = new InMemoryConnection([Connect, new byte[] { 0xE0, 0x00 }]);

        await new MqttUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(
            connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));

        StringAssert.EndsWith(Encoding.ASCII.GetString(replies.ReceivedBytes), "client DISCONNECT 0 e000\n");
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeWithTwoFilters_DeliversTheCaseDataOnEach()
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello);
        var connection = new InMemoryConnection([Connect, Convert.FromHexString("820a00010001610000016200")]);

        await new MqttUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(
            connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));

        var log = Encoding.ASCII.GetString(replies.ReceivedBytes);
        StringAssert.Contains(log, "server PUBLISH 9 3109000161" + "68656c6c6f0a\n");
        StringAssert.Contains(log, "server PUBLISH 9 3109000162" + "68656c6c6f0a\n");
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeWithAFilterCutShort_PublishesNothingAndSurlsServerAnswersIt()
    {
        var replies = new MqttUpstreamTestCaseReplies(Hello);
        var connection = new InMemoryConnection([Connect, Convert.FromHexString("8205000100097a")]);

        await new MqttUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(
            connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));

        var log = Encoding.ASCII.GetString(replies.ReceivedBytes);
        StringAssert.Contains(log, "client SUBSCRIBE 5 000100097a\n");
        Assert.DoesNotContain("PUBLISH", log);
    }
}
