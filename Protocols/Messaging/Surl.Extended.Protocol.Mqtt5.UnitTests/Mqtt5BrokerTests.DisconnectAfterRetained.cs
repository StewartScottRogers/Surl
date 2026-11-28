using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The one-shot read (BL-976; ADR-0093 decision 9): with the option on, SUBACK and the retained messages are
/// followed by <c>DISCONNECT</c> <c>0x00</c> and a close, and the client's Will is discarded.
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    private static readonly byte[] RetainedHi = [0x31, 0x06, 0x00, 0x01, 0x74, 0x00, 0x68, 0x69];

    [TestMethod]
    public void DisconnectsAfterRetained_ByDefault_IsOff() =>
        Assert.IsFalse(Broker().DisconnectsAfterRetained);

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetained_SendsSubackTheRetainedMessageThenDisconnect0x00AndDiscardsTheWill()
    {
        var state = NewState();
        var published = PublishedOn(state);
        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { Retain = true });
        published.Clear();
        var clock = new ManualTimeProvider();
        var client = await Mqtt5TestClient.ConnectAsync(
            Broker(state, disconnectsAfterRetained: true),
            Context(clock, TestContext.CancellationToken),
            Connect("s", will: Will()));

        client.Send(Subscribe(1, ("t", 0)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await client.ReadPacketAsync());
        CollectionAssert.AreEqual(RetainedHi, await client.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, await client.ReadPacketAsync());
        Assert.IsEmpty(await client.ServingEndedAsync());
        Assert.IsEmpty(published);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedWithNothingRetained_SendsSubackThenDisconnect0x00()
    {
        var client = await ConnectClientAsync(Broker(disconnectsAfterRetained: true), "s");

        client.Send(Subscribe(1, ("t", 0)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await client.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, await client.ReadPacketAsync());
        Assert.IsEmpty(await client.ServingEndedAsync());
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedWhileAnEarlierPublisherIsStillBeingHandled_SendsWhatItPublishedWithNoClockAdvance()
    {
        var broker = Broker(disconnectsAfterRetained: true);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken);
        // The publisher has sent its retained PUBLISH and DISCONNECT; the broker has not read them yet.
        var publisher = new Mqtt5HeldWriteConnection(Connect("pub"), RetainedHi, DisconnectNormally);
        var publishing = broker.ServeAsync(publisher, context);
        var subscriber = ConnectionSending(staysOpen: false, Connect("sub"), Subscribe(1, ("t", 0)));

        var subscribing = broker.ServeAsync(subscriber, context);
        publisher.Release();
        await publishing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        await subscribing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            (byte[])[0x90, 0x04, 0x00, 0x01, 0x00, 0x00, .. RetainedHi, 0xE0, 0x00],
            AfterConnackSuccess(subscriber.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedWhenAnEarlierConnectionStaysBusy_AnswersAfterTwoSeconds()
    {
        var broker = Broker(disconnectsAfterRetained: true);
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var publisher = new Mqtt5HeldWriteConnection(Connect("pub"), RetainedHi, DisconnectNormally);
        var publishing = broker.ServeAsync(publisher, Context(clock, TestContext.CancellationToken));
        var subscriber = ConnectionSending(staysOpen: false, Connect("sub"), Subscribe(1, ("t", 0)));
        var subscribing = broker.ServeAsync(subscriber, Context(clock, TestContext.CancellationToken, log: log));

        clock.Advance(TimeSpan.FromMilliseconds(1999));
        var answeredBeforeTwoSeconds = subscriber.WrittenBytes.Length > ConnackSuccess.Length;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await subscribing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsFalse(answeredBeforeTwoSeconds);
        CollectionAssert.AreEqual(
            new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00, 0xE0, 0x00 },
            AfterConnackSuccess(subscriber.WrittenBytes));
        CollectionAssert.Contains(
            log.Notes.ToArray(),
            "An earlier connection was still handling what its client sent after 2 seconds; the SUBSCRIBE is answered without waiting longer.");
        publisher.Release();
        await publishing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedWithALaterConnectionBusy_DoesNotWaitForIt()
    {
        var broker = Broker(disconnectsAfterRetained: true);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken);
        var subscriber = await Mqtt5TestClient.ConnectAsync(broker, context, Connect("sub"));
        var later = new Mqtt5HeldWriteConnection(Connect("later"), DisconnectNormally);
        var servingLater = broker.ServeAsync(later, context);

        subscriber.Send(Subscribe(1, ("t", 0)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.ServingEndedAsync());
        later.Release();
        await servingLater.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedWithAnEarlierConnectionWaitingForItsClient_DoesNotWaitForIt()
    {
        var broker = Broker(disconnectsAfterRetained: true);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken);
        var idle = await Mqtt5TestClient.ConnectAsync(broker, context, Connect("idle"));
        var subscriber = ConnectionSending(staysOpen: false, Connect("sub"), Subscribe(1, ("t", 0)));

        // The earlier connection may still be reaching its next read on another thread.
        await broker.ServeAsync(subscriber, context).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00, 0xE0, 0x00 },
            AfterConnackSuccess(subscriber.WrittenBytes));
        Assert.IsEmpty(await idle.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedWhileAnEarlierPublishIsHeldByADelayRule_DoesNotWaitForTheDelay()
    {
        var broker = Broker(disconnectsAfterRetained: true);
        var clock = new ManualTimeProvider();
        var idleClock = new RecordingIdleClock();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Command = "PUBLISH", DelayMilliseconds = 60000 });
        var publisherContext = Context(clock, TestContext.CancellationToken) with { Faults = plan, IdleClock = idleClock };
        var publisher = ConnectionSending(staysOpen: true, Connect("pub"), RetainedHi, DisconnectNormally);
        var publishing = broker.ServeAsync(publisher, publisherContext);
        await idleClock.FirstPause.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var subscriber = ConnectionSending(staysOpen: false, Connect("sub"), Subscribe(1, ("t", 0)));

        await broker.ServeAsync(subscriber, Context(clock, TestContext.CancellationToken)).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00, 0xE0, 0x00 },
            AfterConnackSuccess(subscriber.WrittenBytes));
        clock.Advance(TimeSpan.FromMinutes(1));
        await publishing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedOffWithAnEarlierConnectionBusy_AnswersAtOnce()
    {
        var broker = Broker();
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken);
        var publisher = new Mqtt5HeldWriteConnection(Connect("pub"), DisconnectNormally);
        var publishing = broker.ServeAsync(publisher, context);
        var subscriber = ConnectionSending(staysOpen: false, Connect("sub"), Subscribe(1, ("t", 0)), DisconnectNormally);

        await broker.ServeAsync(subscriber, context).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, AfterConnackSuccess(subscriber.WrittenBytes));
        publisher.Release();
        await publishing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedOff_KeepsTheConnectionOpenAfterTheRetainedMessage()
    {
        var state = NewState();
        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { Retain = true });
        var client = await ConnectClientAsync(Broker(state), "s");

        client.Send(Subscribe(1, ("t", 0)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await client.ReadPacketAsync());
        CollectionAssert.AreEqual(RetainedHi, await client.ReadPacketAsync());
        Assert.IsEmpty(await client.DisconnectAsync());
    }
}
