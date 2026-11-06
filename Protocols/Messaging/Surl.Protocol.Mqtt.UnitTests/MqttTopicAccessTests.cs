using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The MQTT 3.1.1 server passes the connection's checked account to the bus's
/// <c>--topic-access</c> check on every publish and subscribe, and answers a refusal as
/// ADR-0102 decision 3.4 says: a refused subscribe is <c>SUBACK</c> <c>0x80</c> then
/// <c>DISCONNECT</c> (measured R12), a refused publish is dropped and noted (measured R14).
/// </summary>
[TestClass]
public sealed class MqttTopicAccessTests
{
    private const string Administrator = "admin";

    private static readonly byte[] ConnackAccepted = [0x20, 0x02, 0x00, 0x00];
    private static readonly byte[] Disconnect = [0xE0, 0x00];

    // The SUBSCRIBE upstream curl 8.21.0 sends for mqtt://…/a/b (ADR-0102, R12).
    private static readonly byte[] CurlSubscribeToAB = [0x82, 0x08, 0x00, 0x01, 0x00, 0x03, 0x61, 0x2F, 0x62, 0x00];

    // The PUBLISH upstream curl 8.21.0 sends for -d hi mqtt://…/a/b (ADR-0102, R14).
    private static readonly byte[] CurlPublishHiToAB = [0x30, 0x07, 0x00, 0x03, 0x61, 0x2F, 0x62, 0x68, 0x69];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_AnAccountWithSubOnAHashFilter_SubscribesToATopicUnderItAndGetsItsRetainedMessage()
    {
        var bus = BusGranting(new TopicAccessGrant("a/#", "ann", TopicAccessRights.Subscribe));
        RetainAsAdministrator(bus, "a/b", "hi");

        var (written, log) = await ServeAsync(bus, LoginAs("ann"), CurlSubscribeToAB);

        byte[] expected = [.. ConnackAccepted, 0x90, 0x03, 0x00, 0x01, 0x00, 0x31, 0x07, 0x00, 0x03, 0x61, 0x2F, 0x62, 0x68, 0x69, .. Disconnect];
        CollectionAssert.AreEqual(expected, written);
        Assert.IsFalse(log.Notes.Any(note => note.Contains("--topic-access", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_AnAccountWithoutSub_GetsSubAck0x80ThenDisconnectAndNoRetainedMessage()
    {
        var bus = BusGranting(new TopicAccessGrant("a/#", "ann", TopicAccessRights.Publish));
        RetainAsAdministrator(bus, "a/b", "hi");

        var (written, log) = await ServeAsync(bus, LoginAs("ann"), CurlSubscribeToAB);

        // R12: CONNACK, SUBACK 90 03 00 01 80, DISCONNECT - upstream curl answers it with exit 8.
        byte[] expected = [.. ConnackAccepted, 0x90, 0x03, 0x00, 0x01, 0x80, .. Disconnect];
        CollectionAssert.AreEqual(expected, written);
        CollectionAssert.Contains(log.Notes.ToArray(), "SUBSCRIBE: 1 topic filters refused by --topic-access for account 'ann'; answered SUBACK 0x80 for each.");
    }

    [TestMethod]
    public async Task ServeAsync_ALoginAcceptedUnchecked_IsAnAnonymousCallerAndIsRefusedTheSubscribe()
    {
        var bus = BusGranting(new TopicAccessGrant("a/#", "ann", TopicAccessRights.Subscribe));

        var (written, log) = await ServeAsync(bus, new AnonymousAuthenticationPolicy(), ClientPackets.ConnectWithFields(0xC2, "curlTESTTEST", ClientPackets.String("ann"), ClientPackets.String("pw")), CurlSubscribeToAB);

        byte[] expected = [.. ConnackAccepted, 0x90, 0x03, 0x00, 0x01, 0x80, .. Disconnect];
        CollectionAssert.AreEqual(expected, written);
        CollectionAssert.Contains(log.Notes.ToArray(), "SUBSCRIBE: 1 topic filters refused by --topic-access for an anonymous caller; answered SUBACK 0x80 for each.");
    }

    [TestMethod]
    public async Task ServeAsync_ASubscribeWithAGrantedADeniedAndAnInvalidFilter_AnswersEachInOrder()
    {
        var bus = BusGranting(new TopicAccessGrant("a/#", "ann", TopicAccessRights.Subscribe));
        RetainAsAdministrator(bus, "a/b", "hi");
        RetainAsAdministrator(bus, "c", "no");

        var (written, log) = await ServeAsync(bus, LoginAs("ann"), ClientPackets.Subscribe(7, ("c", 0), ("a/+", 0), ("a/#/b", 0)));

        byte[] expected = [.. ConnackAccepted, 0x90, 0x05, 0x00, 0x07, 0x80, 0x00, 0x80, 0x31, 0x07, 0x00, 0x03, 0x61, 0x2F, 0x62, 0x68, 0x69, .. Disconnect];
        CollectionAssert.AreEqual(expected, written);
        CollectionAssert.Contains(log.Notes.ToArray(), "SUBSCRIBE: 1 topic filters refused by --topic-access for account 'ann'; answered SUBACK 0x80 for each.");
    }

    [TestMethod]
    public async Task ServeAsync_AnAccountWithoutPub_HasItsQos0PublishDroppedNotedAndNotRetained()
    {
        var bus = BusGranting(new TopicAccessGrant("a/#", "ann", TopicAccessRights.Subscribe));

        var (written, log) = await ServeAsync(bus, LoginAs("ann"), CurlPublishHiToAB, ClientPackets.Disconnect);

        // R14: a QoS 0 publish waits for nothing, so CONNACK is all upstream curl sees (exit 0).
        CollectionAssert.AreEqual(ConnackAccepted, written);
        Assert.IsEmpty(RetainedAsAdministrator(bus, "#"));
        CollectionAssert.Contains(log.Notes.ToArray(), "PUBLISH to 'a/b' refused by --topic-access for account 'ann'; dropped.");
    }

    [TestMethod]
    public async Task ServeAsync_AnAccountWithoutPub_HasItsQos1PublishAcknowledgedButNotRetainedAndTheConnectionGoesOn()
    {
        var bus = BusGranting(new TopicAccessGrant("a/#", "ann", TopicAccessRights.Subscribe));

        var (written, _) = await ServeAsync(bus, LoginAs("ann"), ClientPackets.Publish(0x32, "a/b", 1, "hi"), ClientPackets.PingRequest, ClientPackets.Disconnect);

        byte[] expected = [.. ConnackAccepted, 0x40, 0x02, 0x00, 0x01, 0xD0, 0x00];
        CollectionAssert.AreEqual(expected, written);
        Assert.IsEmpty(RetainedAsAdministrator(bus, "#"));
    }

    [TestMethod]
    public async Task ServeAsync_AnAccountWithPub_HasItsPublishRetained()
    {
        var bus = BusGranting(new TopicAccessGrant("a/#", "ann", TopicAccessRights.Publish));

        var (written, log) = await ServeAsync(bus, LoginAs("ann"), CurlPublishHiToAB, ClientPackets.Disconnect);

        CollectionAssert.AreEqual(ConnackAccepted, written);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(RetainedAsAdministrator(bus, "a/b").Single().Value));
        Assert.IsEmpty(log.Notes.Where(note => note.Contains("--topic-access", StringComparison.Ordinal)));
    }

    private static MessageBus.MessageBus BusGranting(TopicAccessGrant grant) =>
        new(TimeProvider.System, configuration: new MessageBusConfiguration
        {
            AccessGrants = [grant, new TopicAccessGrant("#", Administrator, TopicAccessRights.PublishAndSubscribe)],
        });

    private static void RetainAsAdministrator(MessageBus.MessageBus bus, string topic, string payload)
    {
        var published = bus.Publish(new BusMessage(topic, Encoding.ASCII.GetBytes(payload)) { Retain = true }, Administrator);

        Assert.AreEqual(MessageBusOutcome.Succeeded, published.Outcome);
    }

    private static IReadOnlyList<KeyValuePair<string, byte[]>> RetainedAsAdministrator(MessageBus.MessageBus bus, string filter) =>
        MqttBusRetainedMessages.Read(bus, Administrator, [filter]).Matching;

    // The CONNECT upstream curl sends with -u <user>:pw: user name and password flags, clean session.
    private static byte[] LoginAs(string userName) =>
        ClientPackets.ConnectWithFields(0xC2, "curlTESTTEST", ClientPackets.String(userName), ClientPackets.String("pw"));

    private Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(MessageBus.MessageBus bus, byte[] connect, params byte[][] packets) =>
        ServeAsync(bus, new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted), connect, packets);

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(MessageBus.MessageBus bus, IAuthenticationPolicy policy, byte[] connect, params byte[][] packets)
    {
        var server = new MqttProtocolServer(bus, policy);
        var connection = new InMemoryConnection(RecordedFixture.Whole(ClientPackets.Join([connect, .. packets])));
        var log = new RecordingExchangeLog();

        await server.ServeAsync(connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken, log: log));
        await connection.DisposeAsync();

        return (connection.WrittenBytes, log);
    }
}
