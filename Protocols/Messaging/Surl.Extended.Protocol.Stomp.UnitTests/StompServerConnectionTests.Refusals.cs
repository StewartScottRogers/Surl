using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>
/// The routing frames' ERROR-then-close answers, topic access, the bus's bounds, the slow
/// consumer and heart-beating (BL-532; ADR-0118 decisions 4 to 8), written by hand from the STOMP
/// 1.2 specification's "SEND", "SUBSCRIBE", "ACK", "BEGIN", "ERROR" and "Heart-beating".
/// </summary>
public sealed partial class StompServerConnectionTests
{
    // No head-timeout timer, so the only timers a heart-beat test counts are the heart-beats.
    private static readonly ExchangeLimits NoHeadTimeout = new(Timeout.InfiniteTimeSpan, 102400, 8192, 1048576, 104857600);

    [TestMethod]
    [DataRow("SEND\n\nx\0", "Missing destination header")]
    [DataRow("SEND\ndestination:/topic/a/+\n\n\0", "Unknown destination")]
    [DataRow("SEND\ndestination:/elsewhere\n\n\0", "Unknown destination")]
    [DataRow("SEND\ndestination:/queue/\n\n\0", "Unknown destination")]
    [DataRow("SUBSCRIBE\ndestination:/topic/a\n\n\0", "Missing id header")]
    [DataRow("SUBSCRIBE\nid:1\n\n\0", "Missing destination header")]
    [DataRow("SUBSCRIBE\nid:1\ndestination:/topic/a\nack:never\n\n\0", "Invalid ack mode")]
    [DataRow("SUBSCRIBE\nid:1\ndestination:/topic/a\n\n\0SUBSCRIBE\nid:1\ndestination:/topic/b\n\n\0", "Duplicate subscription id")]
    [DataRow("SUBSCRIBE\nid:1\ndestination:/topic/a\n\nbody\0", "Malformed frame")]
    [DataRow("UNSUBSCRIBE\n\n\0", "Missing id header")]
    [DataRow("ACK\n\n\0", "Missing id header")]
    [DataRow("NACK\nmessage-id:1\n\n\0", "Missing id header")]
    [DataRow("BEGIN\n\n\0", "Missing transaction header")]
    [DataRow("COMMIT\n\n\0", "Missing transaction header")]
    [DataRow("ABORT\n\n\0", "Missing transaction header")]
    [DataRow("BEGIN\ntransaction:t\n\n\0BEGIN\ntransaction:t\n\n\0", "Duplicate transaction")]
    [DataRow("COMMIT\ntransaction:t\n\n\0", "Unknown transaction")]
    [DataRow("ABORT\ntransaction:t\n\n\0", "Unknown transaction")]
    [DataRow("SEND\ndestination:/topic/a\ntransaction:t\n\n\0", "Unknown transaction")]
    [DataRow("ACK\nid:1\ntransaction:t\n\n\0", "Unknown transaction")]
    [DataRow("MESSAGE\n\n\0", "Unknown command")]
    public async Task Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed(string frames, string message)
    {
        var answers = await AnswersToAsync(NewBus(), frames);

        Assert.AreEqual($"ERROR\nmessage:{message}\n\n\0", answers);
    }

    [TestMethod]
    public async Task Frame_AtFaultCarryingAReceipt_IsAnsweredErrorWithItsReceiptId()
    {
        var answers = await AnswersToAsync(NewBus(), "SEND\nreceipt:r9\n\n\0");

        Assert.AreEqual("ERROR\nmessage:Missing destination header\nreceipt-id:r9\n\n\0", answers);
    }

    [TestMethod]
    public async Task SubscribeIn10_ClientIndividual_IsInvalidAckMode()
    {
        var session = await StompTestSession.StartAsync(NewBus(), connect: "CONNECT\nlogin:alice\npasscode:secret\n\n\0");

        session.Send("SUBSCRIBE\ndestination:/topic/a\nack:client-individual\n\n\0");
        await session.WaitForAsync("\0");
        await session.CloseAsync();

        Assert.AreEqual("ERROR\nmessage:Invalid ack mode\n\n\0", session.Answers);
    }

    [TestMethod]
    [DataRow("SEND\ndestination:/topic/other\n\n\0")]
    [DataRow("SUBSCRIBE\nid:1\ndestination:/topic/other/#\n\n\0")]
    [DataRow("SEND\ndestination:/queue/q\n\n\0")]
    [DataRow("SUBSCRIBE\nid:1\ndestination:/queue/q\n\n\0")]
    public async Task Frame_RefusedByTopicAccess_IsAccessRefusedAndCreatesNothing(string frame)
    {
        var configuration = new MessageBusConfiguration
        {
            AccessGrants = [new TopicAccessGrant("allowed/#", "alice", TopicAccessRights.PublishAndSubscribe)],
        };
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, configuration: configuration);

        var answers = await AnswersToAsync(bus, frame);

        Assert.AreEqual("ERROR\nmessage:Access refused\n\n\0", answers);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect().InspectQueue("q").Outcome);
    }

    [TestMethod]
    public async Task Subscribe_PastTheBusSubscriptionBound_IsTooManySubscriptions()
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, maxSubscriptionsPerConnection: 1);

        var answers = await AnswersToAsync(bus, "SUBSCRIBE\nid:1\ndestination:/topic/a\n\n\0SUBSCRIBE\nid:2\ndestination:/topic/b\n\n\0");

        Assert.AreEqual("ERROR\nmessage:Too many subscriptions\n\n\0", answers);
    }

    [TestMethod]
    [DataRow("SEND\ndestination:/queue/a\n\n\0SEND\ndestination:/queue/b\n\n\0")]
    [DataRow("SEND\ndestination:/queue/a\n\n\0SUBSCRIBE\nid:1\ndestination:/queue/b\n\n\0")]
    public async Task Frame_PastTheBusQueueBound_IsResourceLimitExceeded(string frames)
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, maxQueues: 1);

        var answers = await AnswersToAsync(bus, frames);

        Assert.AreEqual("ERROR\nmessage:Resource limit exceeded\n\n\0", answers);
    }

    [TestMethod]
    public async Task Send_ToAFullQueue_IsResourceLimitExceeded()
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, maxMessagesPerQueue: 1);

        var answers = await AnswersToAsync(bus, "SEND\ndestination:/queue/a\n\n1\0SEND\ndestination:/queue/a\n\n2\0");

        Assert.AreEqual("ERROR\nmessage:Resource limit exceeded\n\n\0", answers);
    }

    [TestMethod]
    public async Task Send_OverTheBusMessageBound_IsFrameTooLarge()
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, maxMessageBytes: 2);

        var answers = await AnswersToAsync(bus, "SEND\ndestination:/topic/a\n\nabc\0");

        Assert.AreEqual("ERROR\nmessage:Frame too large\n\n\0", answers);
    }

    [TestMethod]
    public async Task Subscribe_ToAQueueAnotherConnectionHoldsExclusively_IsResourceLimitExceeded()
    {
        var bus = NewBus();
        bus.Connect().DeclareQueue("x", new BusQueueFlags(Exclusive: true));

        var answers = await AnswersToAsync(bus, "SUBSCRIBE\nid:1\ndestination:/queue/x\n\n\0");

        Assert.AreEqual("ERROR\nmessage:Resource limit exceeded\n\n\0", answers);
    }

    [TestMethod]
    public async Task Begin_PastTheOpenTransactionBound_IsResourceLimitExceeded()
    {
        var frames = string.Concat(Enumerable.Range(0, StompServerOptions.MaxOpenTransactions + 1).Select(index => $"BEGIN\ntransaction:t{index}\n\n\0"));

        var answers = await AnswersToAsync(NewBus(), frames);

        Assert.AreEqual("ERROR\nmessage:Resource limit exceeded\n\n\0", answers);
    }

    [TestMethod]
    public async Task Transaction_HoldingMoreThanTheUploadLimit_IsResourceLimitExceeded()
    {
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, 1048576, maxUploadBytes: 4);
        var session = await StompTestSession.StartAsync(NewBus(), limits: limits);

        session.Send("BEGIN\ntransaction:x\n\n\0SEND\ndestination:/topic/a\ntransaction:x\n\nabc\0SEND\ndestination:/topic/a\ntransaction:x\n\nde\0");
        await session.WaitForAsync("\0");
        await session.CloseAsync();

        Assert.AreEqual("ERROR\nmessage:Resource limit exceeded\n\n\0", session.Answers);
    }

    [TestMethod]
    public async Task Delivery_PastThePendingBound_IsResourceLimitExceededAndClosed()
    {
        var bus = NewBus();
        var options = new StompServerOptions { PendingBytesLimit = 64 };
        var session = await StompTestSession.StartAsync(bus, options: options);
        await session.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:s\ndestination:/topic/big\nreceipt:s\n\n\0", "s");

        bus.Publish(new BusMessage("big", new byte[100]));
        await session.WaitForAsync("ERROR");
        await session.CloseAsync();

        Assert.AreEqual("RECEIPT\nreceipt-id:s\n\n\0ERROR\nmessage:Resource limit exceeded\n\n\0", session.Answers);
        CollectionAssert.Contains(session.Log.Notes.ToList(), "More than 64 bytes waited unwritten; answered ERROR 'Resource limit exceeded' and closed.");
    }

    [TestMethod]
    public async Task Delivery_PastThePendingBoundWhenTheErrorCannotBeWritten_IsClosed()
    {
        var bus = NewBus();
        var options = new StompServerOptions { PendingBytesLimit = 64 };
        var session = await StompTestSession.StartAsync(bus, options: options, failWritesAfter: 2);
        await session.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:s\ndestination:/topic/big\nreceipt:s\n\n\0", "s");

        bus.Publish(new BusMessage("big", new byte[100]));
        await session.WaitUntilAsync(() => session.Log.Notes.Contains("The slow consumer's ERROR was not written (IOException)."));
        await session.CloseAsync();

        Assert.AreEqual("RECEIPT\nreceipt-id:s\n\n\0", session.Answers);
    }

    [TestMethod]
    public async Task ServeAsync_WriteFailingWhileConnected_Throws()
    {
        var session = await StompTestSession.StartAsync(NewBus(), failWritesAfter: 1);

        session.Send("SUBSCRIBE\nid:s\ndestination:/topic/a\nreceipt:s\n\n\0");

        await Assert.ThrowsExactlyAsync<IOException>(() => session.EndAsync());
    }

    [TestMethod]
    public async Task ServeAsync_CancelledWhileConnected_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var session = await StompTestSession.StartAsync(NewBus(), cancellation: cancellation.Token);
        await session.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:s\ndestination:/topic/a\nreceipt:s\n\n\0", "s");

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => session.EndAsync());
    }

    [TestMethod]
    public async Task HeartBeat_ClientWantingOne_GetsAnEolEveryIntervalInWhichNothingElseWasWritten()
    {
        var clock = new ManualTimeProvider();
        var session = await StompTestSession.StartAsync(
            NewBus(), clock, limits: NoHeadTimeout, connect: "CONNECT\naccept-version:1.2\nlogin:alice\npasscode:secret\nheart-beat:0,500\n\n\0");
        await session.WaitUntilAsync(() => clock.PendingTimerCount == 1);

        clock.Advance(TimeSpan.FromSeconds(1));
        await session.WaitForAsync("\n");
        await session.SendAndWaitForReceiptAsync("ACK\nid:1\nreceipt:k\n\n\0", "k");
        await session.WaitUntilAsync(() => clock.PendingTimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(1));
        await session.WaitUntilAsync(() => clock.PendingTimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(1));
        await session.WaitForAsync("\0\n");
        await session.CloseAsync();

        Assert.AreEqual("\nRECEIPT\nreceipt-id:k\n\n\0\n", session.Answers);
    }

    [TestMethod]
    public async Task HeartBeat_ClientSilentPastTheTolerance_IsClosedWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var options = new StompServerOptions { IdleTimeout = TimeSpan.FromSeconds(8) };
        var session = await StompTestSession.StartAsync(
            NewBus(), clock, options, NoHeadTimeout, connect: "CONNECT\naccept-version:1.2\nlogin:alice\npasscode:secret\nheart-beat:1000,0\n\n\0");
        await session.WaitUntilAsync(() => clock.PendingTimerCount == 1);

        clock.Advance(TimeSpan.FromSeconds(2));
        await session.SendAndWaitForReceiptAsync("ACK\nid:1\nreceipt:k\n\n\0", "k");
        clock.Advance(TimeSpan.FromSeconds(2));
        await session.WaitUntilAsync(() => clock.PendingTimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(2));
        await session.EndAsync();

        Assert.AreEqual("RECEIPT\nreceipt-id:k\n\n\0", session.Answers);
        CollectionAssert.Contains(session.Log.Notes.ToList(), "Nothing arrived from the client for 4000 ms; closed.");
    }

    private static async Task<string> AnswersToAsync(Surl.MessageBus.MessageBus bus, string frames)
    {
        var session = await StompTestSession.StartAsync(bus);
        session.Send(frames);
        await session.WaitForAsync("ERROR");
        await session.CloseAsync();
        return session.Answers;
    }
}
