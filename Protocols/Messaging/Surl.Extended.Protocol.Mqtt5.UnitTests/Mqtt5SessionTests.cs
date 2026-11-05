using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The bounds on what waits for one session (ADR-0093 decision 3).
/// </summary>
[TestClass]
public sealed class Mqtt5SessionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Offer_WithNoConnection_QueuesQualityOfService1ButNot0()
    {
        using var session = NewSession(maxMessages: 10, maxPayloadBytes: 100);
        session.IsConnected = false;

        session.Offer(Message("zero", BusQualityOfService.AtLeastOnce), 0, retain: false, []);
        session.Offer(Message("one", BusQualityOfService.AtLeastOnce), 1, retain: false, []);

        Assert.AreEqual("one", (await TakeAsync(session)).Message.Topic);
    }

    [TestMethod]
    public async Task Offer_PastTheMessageBound_DropsTheNewMessage()
    {
        using var session = NewSession(maxMessages: 1, maxPayloadBytes: 100);

        session.Offer(Message("kept", BusQualityOfService.AtMostOnce), 0, retain: false, []);
        session.Offer(Message("dropped", BusQualityOfService.AtMostOnce), 0, retain: false, []);
        Assert.AreEqual("kept", (await TakeAsync(session)).Message.Topic);
        session.Offer(Message("next", BusQualityOfService.AtMostOnce), 0, retain: false, []);

        Assert.AreEqual("next", (await TakeAsync(session)).Message.Topic);
    }

    [TestMethod]
    public async Task Offer_PastThePayloadBound_DropsTheNewMessageUntilBytesAreTaken()
    {
        using var session = NewSession(maxMessages: 10, maxPayloadBytes: 4);

        session.Offer(Message("kept", BusQualityOfService.AtMostOnce, 3), 0, retain: false, []);
        session.Offer(Message("dropped", BusQualityOfService.AtMostOnce, 2), 0, retain: false, []);
        Assert.AreEqual("kept", (await TakeAsync(session)).Message.Topic);
        session.Offer(Message("next", BusQualityOfService.AtMostOnce, 4), 0, retain: false, []);

        Assert.AreEqual("next", (await TakeAsync(session)).Message.Topic);
    }

    [TestMethod]
    public void Dispose_WithAnExpiryTimer_DisposesIt()
    {
        var clock = new ManualTimeProvider();
        var session = NewSession(maxMessages: 1, maxPayloadBytes: 1);
        session.ExpiryTimer = clock.CreateTimer(_ => Assert.Fail("The timer fired."), null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);

        session.Dispose();
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.AreEqual(0, clock.PendingTimerCount);
    }

    private static Mqtt5Session NewSession(int maxMessages, long maxPayloadBytes)
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System);
        return new Mqtt5Session("c", bus.Connect(), bus.Names, "mqtt5", maxMessages, maxPayloadBytes);
    }

    private static BusMessage Message(string topic, BusQualityOfService qualityOfService, int payloadBytes = 0) =>
        new(topic, new byte[payloadBytes]) { QualityOfService = qualityOfService };

    private async Task<Mqtt5Delivery> TakeAsync(Mqtt5Session session)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return await session.TakeAsync(timeout.Token);
    }
}
