using System.Text;

namespace Surl.MessageBus;

/// <summary>
/// Consuming a subscription queue on the <c>sub</c> right of its topic filter alone
/// (BL-1992; ADR-0146, decisions 3.2 and 3.7), deleting a queue only while it carries a given
/// description (BL-2010; decision 3.4), and asking for that right before a client's delete or purge
/// of a subscription queue (BL-2027; Amendment 2).
/// </summary>
[TestClass]
public sealed class MessageBusSubscriptionQueueTests
{
    private const string Queue = "subscription-queue";
    private const string Description = "described";
    private const string Filter = "sport/+";

    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void ConsumeSubscriptionQueue_SubOnTheFilterOnly_ReceivesWhatTheQueueKept()
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        DeclareSubscriptionQueue(connection);
        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()), "alice");
        var received = new List<string>();

        var consumed = connection.ConsumeSubscriptionQueue(
            Queue, Description, Filter, delivery => received.Add(Encoding.UTF8.GetString(delivery.Message.Payload.Span)));
        consumed.Consumer!.Start();

        Assert.AreEqual(MessageBusOutcome.Succeeded, consumed.Outcome);
        CollectionAssert.AreEqual(new[] { "kept" }, received);
        Assert.AreEqual(1, connection.InspectQueue(Queue).ConsumerCount);
    }

    [TestMethod]
    public void ConsumeAndGetFromQueue_SubOnTheFilterOnly_StayAccessDenied()
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        DeclareSubscriptionQueue(connection);

        var consumed = connection.Consume(Queue, _ => { });
        var got = connection.GetFromQueue(Queue);

        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.AccessDenied, null), consumed);
        Assert.AreEqual(new BusGetResult(MessageBusOutcome.AccessDenied, null, 0), got);
    }

    [TestMethod]
    public void ConsumeSubscriptionQueue_WithoutSubOnTheFilter_IsAccessDeniedAndAddsNoConsumer()
    {
        var bus = Bus();
        using var owner = bus.Connect("alice");
        DeclareSubscriptionQueue(owner);
        using var other = bus.Connect("mallory");

        var consumed = other.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { });

        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.AccessDenied, null), consumed);
        Assert.AreEqual(0, owner.InspectQueue(Queue).ConsumerCount);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no description")]
    [DataRow("another", DisplayName = "another description")]
    public void ConsumeSubscriptionQueue_AQueueNotCarryingTheDescription_IsRefusedAndAddsNoConsumer(string? carried)
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        connection.DeclareQueue(Queue, subscriptionDescription: carried);
        connection.BindQueue(Queue, Filter);

        var consumed = connection.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { });

        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.QueueDescriptionDiffers, null), consumed);
        Assert.AreEqual(0, connection.InspectQueue(Queue).ConsumerCount);
    }

    [TestMethod]
    [DataRow(new string[0], DisplayName = "no binding")]
    [DataRow(new[] { "sport/chess" }, DisplayName = "a narrower filter")]
    [DataRow(new[] { "news" }, DisplayName = "another filter")]
    [DataRow(new[] { "sport/+", "news" }, DisplayName = "the filter and another")]
    public void ConsumeSubscriptionQueue_AQueueNotBoundToExactlyTheFilter_IsAccessDeniedAndAddsNoConsumer(string[] bound)
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        using var binder = bus.Connect("binder");
        connection.DeclareQueue(Queue, subscriptionDescription: Description);
        foreach (var filter in bound)
        {
            Assert.AreEqual(MessageBusOutcome.Succeeded, binder.BindQueue(Queue, filter));
        }

        var consumed = connection.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { });

        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.AccessDenied, null), consumed);
        Assert.AreEqual(0, connection.InspectQueue(Queue).ConsumerCount);
    }

    [TestMethod]
    public void ConsumeSubscriptionQueue_AMissingOrAnotherConnectionsExclusiveQueue_IsRefused()
    {
        var bus = Bus();
        using var owner = bus.Connect("alice");
        using var other = bus.Connect("alice");
        owner.DeclareQueue(Queue, new BusQueueFlags(Exclusive: true), Description);

        var locked = other.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { });
        var missing = other.ConsumeSubscriptionQueue("missing", Description, Filter, _ => { });

        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.QueueLocked, null), locked);
        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.UnknownQueue, null), missing);
    }

    [TestMethod]
    public void ConsumeSubscriptionQueue_Exclusive_KeepsEveryOtherConsumerOut()
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        DeclareSubscriptionQueue(connection);

        var first = connection.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { }, exclusive: true);
        var second = connection.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { });

        Assert.AreEqual(MessageBusOutcome.Succeeded, first.Outcome);
        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.ExclusiveConsumeRefused, null), second);
    }

    [TestMethod]
    public void ConsumeSubscriptionQueue_WithAMaximumOfUnacknowledged_HoldsNoMore()
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        DeclareSubscriptionQueue(connection);
        bus.Publish(new BusMessage("sport/chess", "1"u8.ToArray()), "alice");
        bus.Publish(new BusMessage("sport/chess", "2"u8.ToArray()), "alice");

        connection.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { }, maxUnacknowledged: 1).Consumer!.Start();

        Assert.AreEqual(1, connection.InspectQueue(Queue).ReadyMessageCount);
    }

    [TestMethod]
    public void PublishToQueue_ASubscriptionQueue_IsUnknownQueueAndStoresNothing()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        DeclareSubscriptionQueue(connection);
        var message = new BusMessage("elsewhere", "sent"u8.ToArray());
        var expired = message with { ExpiresAt = clock.GetUtcNow() };

        var sent = bus.PublishToQueue(Queue, message);
        var sentExpired = bus.PublishToQueue(Queue, expired);
        var batched = connection.PublishBatch([BusBatchEntry.ToQueue(Queue, message)]);

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, sent);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, sentExpired);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, batched.Outcome);
        Assert.AreEqual(0, connection.InspectQueue(Queue).ReadyMessageCount);
    }

    [TestMethod]
    public void ConsumeSubscriptionQueue_BadArgumentsOrAClosedConnection_Throw()
    {
        var bus = Bus();
        var connection = bus.Connect("alice");

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.ConsumeSubscriptionQueue(null!, Description, Filter, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.ConsumeSubscriptionQueue(Queue, null!, Filter, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.ConsumeSubscriptionQueue(Queue, Description, null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.ConsumeSubscriptionQueue(Queue, Description, Filter, null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => connection.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { }, maxUnacknowledged: -1));
        connection.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { }));
    }

    [TestMethod]
    [DataRow(Description, DisplayName = "a description")]
    [DataRow(null, DisplayName = "none")]
    public void DeleteQueueIfDescribed_AQueueCarryingTheDescription_DeletesItWithItsMessages(string? description)
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        connection.DeclareQueue(Queue, subscriptionDescription: description);
        connection.BindQueue(Queue, Filter);
        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()), "alice");

        var deleted = connection.DeleteQueueIfDescribed(Queue, description);

        Assert.AreEqual(MessageBusOutcome.Succeeded, deleted);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.InspectQueue(Queue).Outcome);
    }

    [TestMethod]
    [DataRow("another", Description, DisplayName = "another description")]
    [DataRow(null, Description, DisplayName = "none where one is given")]
    [DataRow(Description, null, DisplayName = "one where none is given")]
    public void DeleteQueueIfDescribed_AQueueNotCarryingTheDescription_IsRefusedAndDeletesNothing(string? carried, string? given)
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        connection.DeclareQueue(Queue, subscriptionDescription: carried);
        connection.BindQueue(Queue, Filter);
        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()), "alice");

        var deleted = connection.DeleteQueueIfDescribed(Queue, given);

        Assert.AreEqual(MessageBusOutcome.QueueDescriptionDiffers, deleted);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0, carried), connection.InspectQueue(Queue));
    }

    [TestMethod]
    public void DeleteQueueIfDescribed_AQueueInUse_IsRefusedOnlyWhenAskedForAnUnusedOne()
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        DeclareSubscriptionQueue(connection);
        connection.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { });

        var refused = connection.DeleteQueueIfDescribed(Queue, Description, ifUnused: true);
        var kept = connection.InspectQueue(Queue);
        var deleted = connection.DeleteQueueIfDescribed(Queue, Description);

        Assert.AreEqual(MessageBusOutcome.QueueInUse, refused);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 0, 1, Description), kept);
        Assert.AreEqual(MessageBusOutcome.Succeeded, deleted);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.InspectQueue(Queue).Outcome);
    }

    [TestMethod]
    public void DeleteQueueIfDescribed_AMissingOrAnotherConnectionsExclusiveQueue_IsRefused()
    {
        var bus = Bus();
        using var owner = bus.Connect("alice");
        using var other = bus.Connect("alice");
        owner.DeclareQueue(Queue, new BusQueueFlags(Exclusive: true), Description);

        var locked = other.DeleteQueueIfDescribed(Queue, Description);
        var missing = other.DeleteQueueIfDescribed("missing", Description);

        Assert.AreEqual(MessageBusOutcome.QueueLocked, locked);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, missing);
        Assert.AreEqual(MessageBusOutcome.Succeeded, owner.InspectQueue(Queue).Outcome);
    }

    [TestMethod]
    public void DeleteQueueIfDescribed_ADeclaredQueue_IsAccessDeniedAndStays()
    {
        var bus = new MessageBus(clock, configuration: new MessageBusConfiguration { Queues = [new DeclaredQueue(Queue)] });
        using var connection = bus.Connect();

        var deleted = connection.DeleteQueueIfDescribed(Queue, subscriptionDescription: null);

        Assert.AreEqual(MessageBusOutcome.AccessDenied, deleted);
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.InspectQueue(Queue).Outcome);
    }

    [TestMethod]
    public void DeleteQueueIfDescribed_ANullNameOrAClosedConnection_Throws()
    {
        var bus = Bus();
        var connection = bus.Connect("alice");

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.DeleteQueueIfDescribed(null!, Description));
        connection.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.DeleteQueueIfDescribed(Queue, Description));
    }

    [TestMethod]
    [DataRow("mallory", DisplayName = "an account with no grant")]
    [DataRow(null, DisplayName = "an anonymous caller")]
    public void DeleteAndPurgeQueue_ASubscriptionQueueWithoutSubOnItsFilter_AreAccessDeniedAndTheQueueKeepsItsMessages(string? account)
    {
        var bus = Bus();
        using var owner = bus.Connect("alice");
        DeclareSubscriptionQueue(owner);
        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()), "alice");
        using var other = bus.Connect(account);

        var purged = other.PurgeQueue(Queue);
        var deleted = other.DeleteQueue(Queue);
        var deletedOnConditions = other.DeleteQueue(Queue, ifUnused: true, ifEmpty: true);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.AccessDenied, 0), purged);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, deleted);
        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.AccessDenied, 0), deletedOnConditions);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0, Description), owner.InspectQueue(Queue));
    }

    [TestMethod]
    public void DeleteAndPurgeQueue_ASubscriptionQueueWithSubOnOnlyOneOfItsFilters_AreAccessDenied()
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        using var binder = bus.Connect("binder");
        DeclareSubscriptionQueue(connection);
        binder.BindQueue(Queue, "news");

        var purged = connection.PurgeQueue(Queue);
        var deleted = connection.DeleteQueue(Queue);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.AccessDenied, 0), purged);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, deleted);
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.InspectQueue(Queue).Outcome);
    }

    [TestMethod]
    [DataRow("alice", new[] { Filter }, DisplayName = "sub on its one filter")]
    [DataRow("binder", new[] { Filter, "news" }, DisplayName = "sub on each of its filters")]
    public void DeleteAndPurgeQueue_ASubscriptionQueueWithSubOnEveryFilter_PurgeAndDeleteIt(string account, string[] bound)
    {
        var bus = Bus();
        using var binder = bus.Connect("binder");
        binder.DeclareQueue(Queue, subscriptionDescription: Description);
        foreach (var filter in bound)
        {
            binder.BindQueue(Queue, filter);
        }

        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()), "alice");
        using var connection = bus.Connect(account);

        var purged = connection.PurgeQueue(Queue);
        var deleted = connection.DeleteQueue(Queue);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.Succeeded, 1), purged);
        Assert.AreEqual(MessageBusOutcome.Succeeded, deleted);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.InspectQueue(Queue).Outcome);
    }

    [TestMethod]
    public void DeleteAndPurgeQueue_ASubscriptionQueueBoundToNothingUnderGrants_AreAccessDenied()
    {
        var bus = Bus();
        using var connection = bus.Connect("alice");
        DeclareSubscriptionQueue(connection);
        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()), "alice");
        connection.UnbindQueue(Queue, Filter);

        var purged = connection.PurgeQueue(Queue);
        var deleted = connection.DeleteQueue(Queue);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.AccessDenied, 0), purged);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, deleted);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0, Description), connection.InspectQueue(Queue));
    }

    [TestMethod]
    [DataRow(new[] { Filter }, DisplayName = "bound")]
    [DataRow(new string[0], DisplayName = "bound to nothing")]
    public void DeleteAndPurgeQueue_ASubscriptionQueueWithNoGrantConfigured_PurgeAndDeleteIt(string[] bound)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue(Queue, subscriptionDescription: Description);
        foreach (var filter in bound)
        {
            connection.BindQueue(Queue, filter);
        }

        var purged = connection.PurgeQueue(Queue);
        var deleted = connection.DeleteQueue(Queue);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.Succeeded, 0), purged);
        Assert.AreEqual(MessageBusOutcome.Succeeded, deleted);
    }

    [TestMethod]
    public void DeleteAndPurgeQueue_AQueueWithNoDescriptionWithoutSubOnItsFilter_PurgeAndDeleteIt()
    {
        var bus = Bus();
        using var owner = bus.Connect("alice");
        owner.DeclareQueue(Queue);
        owner.BindQueue(Queue, Filter);
        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()), "alice");
        using var other = bus.Connect("mallory");

        var purged = other.PurgeQueue(Queue);
        var deleted = other.DeleteQueue(Queue);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.Succeeded, 1), purged);
        Assert.AreEqual(MessageBusOutcome.Succeeded, deleted);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, owner.InspectQueue(Queue).Outcome);
    }

    [TestMethod]
    public void DeleteAndPurgeQueue_AnotherConnectionsExclusiveSubscriptionQueueWithoutSub_AreQueueLocked()
    {
        var bus = Bus();
        using var owner = bus.Connect("alice");
        owner.DeclareQueue(Queue, new BusQueueFlags(Exclusive: true), Description);
        owner.BindQueue(Queue, Filter);
        using var other = bus.Connect("mallory");

        var purged = other.PurgeQueue(Queue);
        var deleted = other.DeleteQueue(Queue);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.QueueLocked, 0), purged);
        Assert.AreEqual(MessageBusOutcome.QueueLocked, deleted);
    }

    [TestMethod]
    public void DeleteQueue_IfUnusedOnASubscriptionQueueInUseWithoutSub_IsAccessDeniedNotInUse()
    {
        var bus = Bus();
        using var owner = bus.Connect("alice");
        DeclareSubscriptionQueue(owner);
        owner.ConsumeSubscriptionQueue(Queue, Description, Filter, _ => { });
        using var other = bus.Connect("mallory");

        var deleted = other.DeleteQueue(Queue, ifUnused: true, ifEmpty: false);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.AccessDenied, 0), deleted);
    }

    [TestMethod]
    public void DeleteQueueIfDescribed_WithoutSubOnTheFilter_StillDeletes()
    {
        var bus = Bus();
        using var owner = bus.Connect("alice");
        DeclareSubscriptionQueue(owner);
        using var other = bus.Connect("mallory");

        var deleted = other.DeleteQueueIfDescribed(Queue, Description);

        Assert.AreEqual(MessageBusOutcome.Succeeded, deleted);
    }

    // alice publishes and subscribes on sport/#; the binder may bind any filter; nobody has a $queue grant.
    private MessageBus Bus() => new(
        clock,
        configuration: new MessageBusConfiguration
        {
            AccessGrants =
            [
                new TopicAccessGrant("sport/#", "alice", TopicAccessRights.Publish),
                new TopicAccessGrant("sport/#", "alice", TopicAccessRights.Subscribe),
                new TopicAccessGrant("sport/#", "binder", TopicAccessRights.Subscribe),
                new TopicAccessGrant("news", "binder", TopicAccessRights.Subscribe),
            ],
        });

    private static void DeclareSubscriptionQueue(BusConnection connection)
    {
        connection.DeclareQueue(Queue, subscriptionDescription: Description);
        connection.BindQueue(Queue, Filter);
    }
}
