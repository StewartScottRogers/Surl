using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The MQTT 3.1.1 server keeps, replaces and removes a topic's retained message only as the
/// <c>RETAIN</c> flag of a <c>PUBLISH</c> says (section 3.3.1.3, ADR-0176), and a
/// <c>--topic &lt;filter&gt;;retain</c> declaration retains a matching publish whatever its flag.
/// Each test publishes, then fetches with a <c>SUBSCRIBE</c> on the same bus.
/// </summary>
[TestClass]
public sealed class MqttRetainFlagTests
{
    // A publish from upstream curl carries 0x30 (RETAIN 0); a client that sets RETAIN sends 0x31.
    private const byte RetainClear = 0x30;
    private const byte RetainSet = 0x31;

    private static readonly byte[] ConnackAccepted = [0x20, 0x02, 0x00, 0x00];
    private static readonly byte[] SubackGranted = [0x90, 0x03, 0x00, 0x01, 0x00];
    private static readonly byte[] Disconnect = [0xE0, 0x00];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_PublishWithRetainClear_IsNotFetchedBySubscribe()
    {
        var bus = TestMessageBus.New();

        await PublishAsync(bus, RetainClear, "hi");

        CollectionAssert.AreEqual(Fetched(), await SubscribeAsync(bus));
    }

    [TestMethod]
    public async Task ServeAsync_PublishWithRetainSet_IsFetchedBySubscribe()
    {
        var bus = TestMessageBus.New();

        await PublishAsync(bus, RetainSet, "hi");

        CollectionAssert.AreEqual(Fetched("hi"), await SubscribeAsync(bus));
    }

    [TestMethod]
    public async Task ServeAsync_PublishWithRetainClearOverARetainedMessage_LeavesTheRetainedMessage()
    {
        var bus = TestMessageBus.New();
        await PublishAsync(bus, RetainSet, "old");

        await PublishAsync(bus, RetainClear, "new");

        CollectionAssert.AreEqual(Fetched("old"), await SubscribeAsync(bus));
    }

    [TestMethod]
    public async Task ServeAsync_PublishWithRetainSetOverARetainedMessage_ReplacesIt()
    {
        var bus = TestMessageBus.New();
        await PublishAsync(bus, RetainSet, "old");

        await PublishAsync(bus, RetainSet, "new");

        CollectionAssert.AreEqual(Fetched("new"), await SubscribeAsync(bus));
    }

    [TestMethod]
    public async Task ServeAsync_EmptyPublishWithRetainClear_LeavesTheRetainedMessage()
    {
        var bus = TestMessageBus.New();
        await PublishAsync(bus, RetainSet, "hi");

        await PublishAsync(bus, RetainClear, "");

        CollectionAssert.AreEqual(Fetched("hi"), await SubscribeAsync(bus));
    }

    [TestMethod]
    public async Task ServeAsync_EmptyPublishWithRetainSet_RemovesTheRetainedMessage()
    {
        var bus = TestMessageBus.New();
        await PublishAsync(bus, RetainSet, "hi");

        await PublishAsync(bus, RetainSet, "");

        CollectionAssert.AreEqual(Fetched(), await SubscribeAsync(bus));
    }

    [TestMethod]
    public async Task ServeAsync_PublishWithRetainClearOnADeclaredRetainTopic_IsFetchedBySubscribe()
    {
        var bus = TestMessageBus.RetainingEveryPublish();

        await PublishAsync(bus, RetainClear, "hi");

        CollectionAssert.AreEqual(Fetched("hi"), await SubscribeAsync(bus));
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeWhileAnEarlierPublisherIsStillBeingHandled_FetchesWhatItPublishedWithNoClockAdvance()
    {
        var bus = TestMessageBus.RetainingEveryPublish();
        var server = new MqttProtocolServer(bus, new AnonymousAuthenticationPolicy());
        var context = MqttTestExchange.Context(new ManualTimeProvider(), TestContext.CancellationToken);
        // The publisher has sent its PUBLISH and DISCONNECT; the server has not read them yet.
        var publisher = new MqttHeldWriteConnection(PublishRequest(RetainClear, "hi"));
        var publishing = server.ServeAsync(publisher, context);
        var subscriber = new InMemoryConnection(RecordedFixture.Whole(SubscribeRequest()));

        var subscribing = server.ServeAsync(subscriber, context);
        publisher.Release();
        await publishing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        await subscribing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(Fetched("hi"), subscriber.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeWhenAnEarlierConnectionStaysBusy_IsAnsweredAfterTwoSecondsWithANote()
    {
        var server = new MqttProtocolServer(TestMessageBus.New(), new AnonymousAuthenticationPolicy());
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var publisher = new MqttHeldWriteConnection(PublishRequest(RetainSet, "hi"));
        var publishing = server.ServeAsync(publisher, MqttTestExchange.Context(clock, TestContext.CancellationToken));
        var subscriber = new InMemoryConnection(RecordedFixture.Whole(SubscribeRequest()));
        var subscribing = server.ServeAsync(subscriber, MqttTestExchange.Context(clock, TestContext.CancellationToken, log: log));

        clock.Advance(TimeSpan.FromMilliseconds(1999));
        var writtenBeforeTwoSeconds = subscriber.WrittenBytes;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await subscribing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(ConnackAccepted, writtenBeforeTwoSeconds);
        CollectionAssert.AreEqual(Fetched(), subscriber.WrittenBytes);
        CollectionAssert.Contains(
            log.Notes.ToArray(),
            "An earlier connection was still handling what its client sent after 2 seconds; the SUBSCRIBE is answered without waiting longer.");
        publisher.Release();
        await publishing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
    }

    private static byte[] PublishRequest(byte firstByte, string payload) =>
        ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.Publish(firstByte, "t", payload), ClientPackets.Disconnect);

    private static byte[] SubscribeRequest() =>
        ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.Subscribe(1, ("t", 0)));

    private static byte[] Fetched(string? payload = null) =>
        payload is null
            ? [.. ConnackAccepted, .. SubackGranted, .. Disconnect]
            : [.. ConnackAccepted, .. SubackGranted, 0x31, (byte)(3 + payload.Length), 0x00, 0x01, (byte)'t', .. System.Text.Encoding.ASCII.GetBytes(payload), .. Disconnect];

    private Task PublishAsync(MessageBus.MessageBus bus, byte firstByte, string payload) =>
        ServeAsync(bus, PublishRequest(firstByte, payload));

    private async Task<byte[]> SubscribeAsync(MessageBus.MessageBus bus) =>
        await ServeAsync(bus, SubscribeRequest());

    private async Task<byte[]> ServeAsync(MessageBus.MessageBus bus, byte[] request)
    {
        var connection = new InMemoryConnection(RecordedFixture.Whole(request));

        await new MqttProtocolServer(bus, new AnonymousAuthenticationPolicy()).ServeAsync(connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken));
        await connection.DisposeAsync();

        return connection.WrittenBytes;
    }
}
