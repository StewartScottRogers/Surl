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
        Assert.IsEmpty(MqttUpstreamTestCaseReplies.SupportedServerCommands);
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
