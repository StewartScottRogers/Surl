using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// The two reads that leave the bus as it was (BL-1195, ADR-0164 decisions 1 to 3).
/// </summary>
[TestClass]
public sealed class MessageBusBrowseTests
{
    private readonly SettableTimeProvider clock = new();
    private readonly RecordingSurlEventPublisher events = new();

    [TestMethod]
    public void BrowseQueue_ListsReadyMessagesInDeliveryOrderAndChangesNothing()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2") with { HighPriority = true });
        bus.PublishToQueue("q", Message("3"));
        connection.GetFromQueue("q");
        var before = connection.InspectQueue("q");

        var browsed = bus.BrowseQueue("q");
        var after = connection.InspectQueue("q");

        Assert.AreEqual(MessageBusOutcome.Succeeded, browsed.Outcome);
        CollectionAssert.AreEqual(new[] { 0, 1 }, browsed.Messages.Select(message => message.Position).ToArray());
        CollectionAssert.AreEqual(new[] { "1", "3" }, browsed.Messages.Select(message => Text(message.Message)).ToArray());
        Assert.AreEqual(2, browsed.ReadyCount);
        Assert.AreEqual(1, browsed.InFlightCount);
        Assert.IsFalse(browsed.HasMore);
        Assert.AreEqual(before, after);
        Assert.AreEqual("1", Text(connection.GetFromQueue("q").Delivery!.Message));
    }

    [TestMethod]
    public void BrowseQueue_HighPriorityComesFirst()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2") with { HighPriority = true });

        var browsed = bus.BrowseQueue("q");

        CollectionAssert.AreEqual(new[] { "2", "1" }, browsed.Messages.Select(message => Text(message.Message)).ToArray());
    }

    [TestMethod]
    public void BrowseQueue_LimitAndAfterPosition_PageThroughTheMessages()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        foreach (var payload in new[] { "a", "b", "c" })
        {
            bus.PublishToQueue("q", Message(payload));
        }

        var first = bus.BrowseQueue("q", limit: 2);
        var second = bus.BrowseQueue("q", afterPosition: 1, limit: 2);

        CollectionAssert.AreEqual(new[] { "a", "b" }, first.Messages.Select(message => Text(message.Message)).ToArray());
        Assert.IsTrue(first.HasMore);
        CollectionAssert.AreEqual(new[] { "c" }, second.Messages.Select(message => Text(message.Message)).ToArray());
        Assert.AreEqual(2, second.Messages[0].Position);
        Assert.IsFalse(second.HasMore);
    }

    [TestMethod]
    public void BrowseQueue_SkipsExpiredMessagesWithoutDroppingThem()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("old") with { ExpiresAt = clock.UtcNow.AddMinutes(1) });
        bus.PublishToQueue("q", Message("new"));
        clock.UtcNow = clock.UtcNow.AddMinutes(2);

        var browsed = bus.BrowseQueue("q");

        CollectionAssert.AreEqual(new[] { "new" }, browsed.Messages.Select(message => Text(message.Message)).ToArray());
        Assert.AreEqual(0, browsed.Messages[0].Position);
        Assert.AreEqual(2, browsed.ReadyCount);
    }

    [TestMethod]
    public void BrowseQueue_StopsBeforeTheByteBoundButAlwaysListsTheFirstMessage()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", new BusMessage("q", new byte[(int)MessageBus.MaxBrowseBytes + 1]));
        bus.PublishToQueue("q", Message("small"));

        var browsed = bus.BrowseQueue("q");

        Assert.HasCount(1, browsed.Messages);
        Assert.IsTrue(browsed.HasMore);
    }

    [TestMethod]
    public void BrowseQueue_AnUnknownQueue_IsUnknownQueue()
    {
        var bus = new MessageBus(clock);

        var browsed = bus.BrowseQueue("missing");

        Assert.AreEqual(new BusQueueBrowseResult(MessageBusOutcome.UnknownQueue, [], 0, 0, false).Outcome, browsed.Outcome);
        Assert.IsEmpty(browsed.Messages);
    }

    [TestMethod]
    public void BrowseQueue_RefusesArgumentsOutOfRange()
    {
        var bus = new MessageBus(clock);

        Assert.Throws<ArgumentNullException>(() => bus.BrowseQueue(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => bus.BrowseQueue("q", afterPosition: -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => bus.BrowseQueue("q", limit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => bus.BrowseQueue("q", limit: MessageBus.MaxBrowseLimit + 1));
    }

    [TestMethod]
    public void GetRetainedMessage_ReturnsItWithoutDroppingItAndNullWhenThereIsNone()
    {
        var bus = new MessageBus(clock);
        bus.Publish(new BusMessage("a/b", "x"u8.ToArray()) { Retain = true, QualityOfService = BusQualityOfService.AtLeastOnce });

        var retained = bus.GetRetainedMessage("a/b");

        Assert.IsNotNull(retained);
        Assert.AreEqual("x", Text(retained));
        Assert.AreEqual(BusQualityOfService.AtLeastOnce, retained.QualityOfService);
        Assert.IsNotNull(bus.GetRetainedMessage("a/b"));
        Assert.IsNull(bus.GetRetainedMessage("a/none"));
        Assert.IsNull(bus.GetRetainedMessage("#/bad"));
        Assert.Throws<ArgumentNullException>(() => bus.GetRetainedMessage(null!));
    }

    [TestMethod]
    public void GetRetainedMessage_AnExpiredOne_IsNone()
    {
        var bus = new MessageBus(clock);
        bus.Publish(new BusMessage("t", "x"u8.ToArray()) { Retain = true, ExpiresAt = clock.UtcNow.AddMinutes(1) });
        clock.UtcNow = clock.UtcNow.AddMinutes(2);

        Assert.IsNull(bus.GetRetainedMessage("t"));
    }

    [TestMethod]
    public void NeitherRead_PublishesAnEvent()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));
        bus.Publish(new BusMessage("t", "x"u8.ToArray()) { Retain = true });
        var mark = events.Events.Count;

        bus.BrowseQueue("q");
        bus.BrowseQueue("missing");
        bus.GetRetainedMessage("t");

        Assert.AreEqual(mark, events.Events.Count);
    }

    private static BusMessage Message(string payload) => new("q", Encoding.UTF8.GetBytes(payload));

    private static string Text(BusMessage message) => Encoding.UTF8.GetString(message.Payload.Span);
}
