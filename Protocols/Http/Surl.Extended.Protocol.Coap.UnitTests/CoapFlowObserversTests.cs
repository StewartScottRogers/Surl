using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Observation through the datagram seam (RFC 7641 sections 3, 4 and 6; RFC 7959 section 2.6;
/// ADR-0097 decisions 5 and 7): registration, notifications on a change from another flow and on
/// Max-Age, and every way an observer is removed, on a clock that moves only when the test moves it.
/// </summary>
[TestClass]
public sealed class CoapFlowObserversTests
{
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(10);

    private static readonly CoapAccessRules Anonymous = new(AllowsAnonymous: true, HasAccounts: false);

    // RFC 7641 section 2 and RFC 7252 section 3: CON GET, Message ID 1, token 4A, Observe 0 (delta 6,
    // length 0), Uri-Path "a.txt" (delta 5, length 5).
    private static readonly byte[] Registration = [0x41, 0x01, 0x00, 0x01, 0x4A, 0x60, 0x55, (byte)'a', (byte)'.', (byte)'t', (byte)'x', (byte)'t'];

    [TestMethod]
    public async Task ServeAsync_Registration_IsAnswered205WithObserveOne()
    {
        await using var harness = new Harness(new CoapTestStore().WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);

        var response = await observer.ReceiveAsync(1);

        Assert.AreEqual(CoapMessageType.Acknowledgement, response.Type);
        Assert.AreEqual(CoapCode.Content, response.Code);
        Assert.AreEqual((ushort)1, response.MessageId);
        CollectionAssert.AreEqual(new byte[] { 0x4A }, response.Token.ToArray());
        Assert.AreEqual(1u, ObserveOf(response));
        Assert.AreEqual("one", Encoding.UTF8.GetString(response.Payload.Span));
        Assert.AreEqual(1, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_PutFromAnotherFlow_NotifiesTheObserverWithAHigherNumberAndTheNewState()
    {
        await using var harness = new Harness(new CoapTestStore(CoapTestStore.Uploads).WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);

        var writer = harness.Open(Request(CoapCode.Put, 7, 0x77, "a.txt", "two"));
        Assert.AreEqual(CoapCode.Changed, (await writer.ReceiveAsync(1)).Code);
        var notification = await observer.ReceiveAsync(2);

        Assert.AreEqual(CoapMessageType.Confirmable, notification.Type);
        Assert.AreEqual(CoapCode.Content, notification.Code);
        Assert.AreEqual((ushort)0xFFFF, notification.MessageId);
        CollectionAssert.AreEqual(new byte[] { 0x4A }, notification.Token.ToArray());
        Assert.AreEqual(2u, ObserveOf(notification));
        Assert.AreEqual(60u, UIntOf(notification, CoapOptionNumber.MaxAge));
        Assert.AreEqual("two", Encoding.UTF8.GetString(notification.Payload.Span));

        observer.Flow.DeliverFromClient(new byte[] { 0x60, 0x00, 0xFF, 0xFF });
        await observer.WaitForNoteAsync("The client's Acknowledgement with Message ID 65535 ends its retransmission.");
        Assert.AreEqual(1, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_MaxAgeLapsing_SendsANotificationWhetherOrNotTheResourceChanged()
    {
        await using var harness = new Harness(new CoapTestStore().WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);

        await harness.AdvanceWhenTimerIsPendingAsync(TimeSpan.FromSeconds(59.999));
        Assert.HasCount(1, observer.Flow.SentDatagrams);
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var notification = await observer.ReceiveAsync(2);

        Assert.AreEqual(CoapMessageType.Confirmable, notification.Type);
        Assert.AreEqual(2u, ObserveOf(notification));
        Assert.AreEqual("one", Encoding.UTF8.GetString(notification.Payload.Span));
    }

    [TestMethod]
    public async Task ServeAsync_UnacknowledgedConNotification_IsGivenUpAfterMaxRetransmitAndRemovesTheObserver()
    {
        await using var harness = new Harness(new CoapTestStore().WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);
        await harness.AdvanceWhenTimerIsPendingAsync(TimeSpan.FromSeconds(60));
        await observer.ReceiveAsync(2);

        foreach (var (timeout, sends) in new[] { (2, 3), (4, 4), (8, 5), (16, 6) })
        {
            await harness.AdvanceWhenTimerIsPendingAsync(TimeSpan.FromSeconds(timeout));
            await observer.ReceiveAsync(sends);
        }

        await harness.AdvanceWhenTimerIsPendingAsync(TimeSpan.FromSeconds(32));
        await observer.WaitForNoteAsync("Gave up: the notification with Message ID 65535 was not acknowledged after 4 retransmissions.");
        await observer.WaitForNoteAsync("Observation ended: token 4A, notification 2.05 GivenUp.");

        Assert.AreEqual(0, harness.Observations.Count);
        Assert.HasCount(6, observer.Flow.SentDatagrams);
    }

    [TestMethod]
    public async Task ServeAsync_RstMatchingANotification_RemovesTheObserver()
    {
        await using var harness = new Harness(new CoapTestStore().WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);
        await harness.AdvanceWhenTimerIsPendingAsync(TimeSpan.FromSeconds(60));
        await observer.ReceiveAsync(2);

        observer.Flow.DeliverFromClient(new byte[] { 0x70, 0x00, 0xFF, 0xFF });
        await observer.WaitForNoteAsync("Observation ended: token 4A, notification 2.05 Reset.");

        Assert.AreEqual(0, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_GetWithObserveOne_RemovesTheObserverAndIsAnsweredPlainly()
    {
        await using var harness = new Harness(new CoapTestStore().WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);

        observer.Flow.DeliverFromClient(Request(CoapCode.Get, 2, 0x4A, "a.txt", observe: 1));
        var response = await observer.ReceiveAsync(2);
        await observer.WaitForNoteAsync("Deregistered: token 4A asked with Observe 1.");

        Assert.AreEqual(CoapCode.Content, response.Code);
        Assert.IsNull(ObserveOf(response));
        Assert.AreEqual(0, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_ServerAtItsObservationLimit_ServesANewRegistrationAsAPlainGet()
    {
        await using var harness = new Harness(new CoapTestStore().WithFile("a.txt", "one"), new CoapObservations(1));
        var first = harness.Open(Registration);
        await first.ReceiveAsync(1);

        var second = harness.Open(Request(CoapCode.Get, 1, 0x4B, "a.txt", observe: 0));
        var response = await second.ReceiveAsync(1);

        Assert.AreEqual(CoapCode.Content, response.Code);
        Assert.IsNull(ObserveOf(response));
        await second.WaitForNoteAsync("Not observed: the server already has its most observations.");
        Assert.AreEqual(1, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_FlowAtItsObservationLimit_ServesASeventeenthRegistrationAsAPlainGet()
    {
        await using var harness = new Harness(new CoapTestStore().WithFile("a.txt", "one"));
        var observer = harness.Open(Request(CoapCode.Get, 100, 0, "a.txt", observe: 0));
        await observer.ReceiveAsync(1);

        for (byte token = 1; token <= CoapFlowObservers.DefaultCapacity; token++)
        {
            observer.Flow.DeliverFromClient(Request(CoapCode.Get, (ushort)(100 + token), token, "a.txt", observe: 0));
            await observer.ReceiveAsync(token + 1);
        }

        Assert.IsNull(ObserveOf(observer.Decode(CoapFlowObservers.DefaultCapacity)));
        await observer.WaitForNoteAsync("Not observed: the flow already has 16 observations.");
        Assert.AreEqual(CoapFlowObservers.DefaultCapacity, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_LargeObservedResource_IsNotifiedInItsFirstBlockWithSize2()
    {
        var body = CoapTestExchange.Body(1500);
        await using var harness = new Harness(new CoapTestStore(CoapTestStore.Uploads).WithFile("a.txt", body));
        var observer = harness.Open(Request(CoapCode.Get, 1, 0x4A, "a.txt", observe: 0, block2: "0/0/256"));
        var registered = await observer.ReceiveAsync(1);
        Assert.AreEqual("0/1/256", BlockOf(registered));

        var writer = harness.Open(Request(CoapCode.Put, 7, 0x77, "a.txt", body + "!"));
        await writer.ReceiveAsync(1);
        var notification = await observer.ReceiveAsync(2);

        Assert.AreEqual(CoapCode.Content, notification.Code);
        Assert.AreEqual(2u, ObserveOf(notification));
        Assert.AreEqual("0/1/256", BlockOf(notification));
        Assert.AreEqual(1501u, UIntOf(notification, CoapOptionNumber.Size2));
        Assert.AreEqual(body[..256], Encoding.UTF8.GetString(notification.Payload.Span));
    }

    [TestMethod]
    public async Task ServeAsync_ReRegistrationWithTheSameToken_ReplacesTheEntryAndKeepsItsNumbers()
    {
        await using var harness = new Harness(new CoapTestStore().WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);

        observer.Flow.DeliverFromClient(Request(CoapCode.Get, 2, 0x4A, "a.txt", observe: 0));
        var again = await observer.ReceiveAsync(2);

        Assert.AreEqual(2u, ObserveOf(again));
        Assert.AreEqual(1, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_DeleteFromAnotherFlow_Notifies404WithoutObserveAndRemovesTheObserver()
    {
        await using var harness = new Harness(new CoapTestStore(CoapTestStore.Uploads).WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);

        var writer = harness.Open(Request(CoapCode.Delete, 7, 0x77, "a.txt"));
        Assert.AreEqual(CoapCode.Deleted, (await writer.ReceiveAsync(1)).Code);
        var notification = await observer.ReceiveAsync(2);
        observer.Flow.DeliverFromClient(new byte[] { 0x60, 0x00, 0xFF, 0xFF });
        await observer.WaitForNoteAsync("Observation ended: token 4A, notification 4.04 Acknowledged.");

        Assert.AreEqual(CoapCode.NotFound, notification.Code);
        Assert.IsNull(ObserveOf(notification));
        Assert.AreEqual(0, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_ChangesWhileANotificationIsOutstanding_AreSentOnceAsTheNewestState()
    {
        await using var harness = new Harness(new CoapTestStore(CoapTestStore.Uploads).WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);
        var writer = harness.Open(Request(CoapCode.Put, 7, 0x77, "a.txt", "two"));
        await writer.ReceiveAsync(1);
        await observer.ReceiveAsync(2);

        writer.Flow.DeliverFromClient(Request(CoapCode.Put, 8, 0x78, "a.txt", "three"));
        await writer.ReceiveAsync(2);
        writer.Flow.DeliverFromClient(Request(CoapCode.Put, 9, 0x79, "a.txt", "four"));
        await writer.ReceiveAsync(3);
        Assert.HasCount(2, observer.Flow.SentDatagrams);
        observer.Flow.DeliverFromClient(new byte[] { 0x60, 0x00, 0xFF, 0xFF });
        var newest = await observer.ReceiveAsync(3);

        Assert.AreEqual(3u, ObserveOf(newest));
        Assert.AreEqual("four", Encoding.UTF8.GetString(newest.Payload.Span));
    }

    [TestMethod]
    public async Task ServeAsync_UnobservableRegistrations_AreAnsweredPlainly()
    {
        await using var harness = new Harness(new CoapTestStore(CoapTestStore.Listings).WithFile("a.txt", "one"));
        var observer = harness.Open(Request(CoapCode.Get, 1, 1, "nope.txt", observe: 0));

        observer.Flow.DeliverFromClient(Request(CoapCode.Get, 2, 2, ".well-known/core", observe: 0));
        observer.Flow.DeliverFromClient(Request(CoapCode.Get, 3, 3, "a.txt", observe: 0, block2: "1/0/16"));
        observer.Flow.DeliverFromClient(Request(CoapCode.Get, 4, 4, "a.txt", observe: 2));
        observer.Flow.DeliverFromClient(Request(CoapCode.Get, 5, 5, "a.txt", observe: 1));
        observer.Flow.DeliverFromClient(Request(CoapCode.Get, 6, 6, "a.txt", observe: 0, entityTag: CoapTestStore.EntityTagOf(3)));
        await observer.ReceiveAsync(6);

        Assert.AreEqual(CoapCode.NotFound, observer.Decode(0).Code);
        Assert.AreEqual(CoapCode.Content, observer.Decode(1).Code);
        Assert.AreEqual(CoapCode.BadOption, observer.Decode(2).Code);
        Assert.AreEqual(CoapCode.Content, observer.Decode(3).Code);
        Assert.AreEqual(CoapCode.Content, observer.Decode(4).Code);
        Assert.AreEqual(CoapCode.Valid, observer.Decode(5).Code);
        Assert.IsTrue(Enumerable.Range(0, 6).All(index => ObserveOf(observer.Decode(index)) is null));
        Assert.AreEqual(0, harness.Observations.Count);
    }

    [TestMethod]
    public async Task ServeAsync_WritesThatChangeNothing_NotifyNoObserver()
    {
        await using var harness = new Harness(new CoapTestStore(CoapTestStore.Uploads).WithFile("a.txt", "one"));
        var observer = harness.Open(Registration);
        await observer.ReceiveAsync(1);

        var writer = harness.Open(Request(CoapCode.Delete, 7, 0x77, "CON"));
        writer.Flow.DeliverFromClient(Request(CoapCode.Put, 8, 0x78, "a.txt", "two", ifNoneMatch: true));
        writer.Flow.DeliverFromClient(Request(CoapCode.Put, 9, 0x79, "a.txt", "two", block1: "0/1/16"));
        await writer.ReceiveAsync(3);

        Assert.AreEqual(CoapCode.Deleted, writer.Decode(0).Code);
        Assert.AreEqual(CoapCode.PreconditionFailed, writer.Decode(1).Code);
        Assert.AreEqual(CoapCode.Continue, writer.Decode(2).Code);
        await Task.Delay(50);
        Assert.HasCount(1, observer.Flow.SentDatagrams);
    }

    [TestMethod]
    public void CoapObservations_DefaultCapacity_IsAThousand() =>
        Assert.AreEqual(1000, CoapObservations.DefaultCapacity);

    [TestMethod]
    public async Task Constructor_WithObservations_SharesThemAndRejectsNull()
    {
        var store = new CoapTestStore().WithFile("a.txt", "one");
        var observations = new CoapObservations();
        var server = new CoapProtocolServer(store.Store, Anonymous, observations);
        await using var flow = new InMemoryDatagramFlow(Registration);
        using var exchange = new CancellationTokenSource();

        var serving = server.ServeAsync(flow, CoapProtocolServerTests.Context(new RecordingExchangeLog(), new ManualTimeProvider(), exchange.Token));
        await WaitUntilAsync(() => flow.SentDatagrams.Count == 1, "the registration's answer");

        Assert.AreEqual(1, observations.Count);
        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(0, observations.Count);
        Assert.ThrowsExactly<ArgumentNullException>(() => new CoapProtocolServer(store.Store, null!, observations));
    }

    private static byte[] Request(
        CoapCode method,
        ushort messageId,
        byte token,
        string path,
        string body = "",
        uint? observe = null,
        string? block2 = null,
        string? block1 = null,
        bool ifNoneMatch = false,
        byte[]? entityTag = null)
    {
        var options = path.Split('/').Select(segment => CoapOption.FromString(CoapOptionNumber.UriPath, segment)).ToList();

        if (observe is { } number)
        {
            options.Add(CoapOption.FromUInt(CoapOptionNumber.Observe, number));
        }

        if (block2 is not null)
        {
            options.Add(CoapTestExchange.Block(CoapOptionNumber.Block2, block2));
        }

        if (block1 is not null)
        {
            options.Add(CoapTestExchange.Block(CoapOptionNumber.Block1, block1));
        }

        if (entityTag is not null)
        {
            options.Add(new CoapOption(CoapOptionNumber.ETag, entityTag));
        }

        if (ifNoneMatch)
        {
            options.Add(new CoapOption(CoapOptionNumber.IfNoneMatch, ReadOnlyMemory<byte>.Empty));
        }

        return CoapMessageWriter.Write(new CoapMessage(CoapMessageType.Confirmable, method, messageId, new[] { token }, options, Encoding.UTF8.GetBytes(body)));
    }

    private static uint? ObserveOf(CoapMessage message) => UIntOf(message, CoapOptionNumber.Observe);

    private static uint? UIntOf(CoapMessage message, int number) =>
        message.Options.SingleOrDefault(option => option.Number == number)?.ReadUInt();

    private static string? BlockOf(CoapMessage message) =>
        message.Options.SingleOrDefault(option => option.Number == CoapOptionNumber.Block2) is { } option ? CoapBlockOption.Read(option).ToString() : null;

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var started = DateTime.UtcNow;

        while (!condition())
        {
            Assert.IsLessThan(PatienceLimit, DateTime.UtcNow - started, $"Waited for {what}.");
            await Task.Delay(5);
        }
    }

    private sealed class Harness(CoapTestStore store, CoapObservations? observations = null) : IAsyncDisposable
    {
        private readonly CancellationTokenSource exchange = new();
        private readonly List<ServedFlow> flows = [];

        public ManualTimeProvider Clock { get; } = new();

        public CoapObservations Observations { get; } = observations ?? new CoapObservations();

        private CoapProtocolServer Server => field ??= new CoapProtocolServer(store.Store, Anonymous, new UnitTestCoapRandomSource(), observations: Observations);

        public ServedFlow Open(byte[] firstDatagram)
        {
            var flow = new InMemoryDatagramFlow(firstDatagram);
            var log = new RecordingExchangeLog();
            var served = new ServedFlow(flow, log, Server.ServeAsync(flow, CoapProtocolServerTests.Context(log, Clock, exchange.Token)));
            flows.Add(served);
            return served;
        }

        public async Task AdvanceWhenTimerIsPendingAsync(TimeSpan by)
        {
            await WaitUntilAsync(() => Clock.PendingTimers >= 1, "a timer");
            Clock.Advance(by);
        }

        public async ValueTask DisposeAsync()
        {
            await exchange.CancelAsync();

            foreach (var served in flows)
            {
                await Assert.ThrowsAsync<OperationCanceledException>(() => served.Serving);
                await served.Flow.DisposeAsync();
            }

            Assert.AreEqual(0, Observations.Count);
            exchange.Dispose();
        }
    }

    private sealed record ServedFlow(InMemoryDatagramFlow Flow, RecordingExchangeLog Log, Task Serving)
    {
        public CoapMessage Decode(int index) => CoapMessageReader.Read(Flow.SentDatagrams[index]).Message!;

        public async Task<CoapMessage> ReceiveAsync(int count)
        {
            await WaitUntilAsync(() => Flow.SentDatagrams.Count >= count, $"{count} datagrams sent");
            return Decode(count - 1);
        }

        public Task WaitForNoteAsync(string note) => WaitUntilAsync(() => Log.Notes.Contains(note), note);
    }
}
