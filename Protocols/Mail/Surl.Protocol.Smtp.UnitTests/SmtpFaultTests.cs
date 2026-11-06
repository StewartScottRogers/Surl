using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.RecordedFixtureTests;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The command-stage faults (ADR-0124, decision 4; BL-796; ADR-0141, BL-901). A delay holds a
/// command's answer with the idle clock paused and a drop aborts the connection unanswered; a command an <see cref="FaultKind.Error"/>
/// rule matches is answered with the rule's reply in place of its own, nothing is stored, and one
/// <see cref="FaultAppliedEvent"/> is published. Requests are replayed from pinned upstream curl
/// 8.21.0 (<c>Fixtures/fault-*</c>, see the README).
/// </summary>
[TestClass]
public sealed class SmtpFaultTests
{
    private const string TryLater = "451 4.3.0 try later";

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule ErrorRule(string? command = null, string? path = null, string? account = null, string? reply = TryLater) =>
        new("f1", FaultKind.Error) { Scheme = "smtp", Command = command, Path = path, Account = account, Reply = reply };

    private static MailboxStore AliceAndBobStore(TimeProvider clock) => AccountStore(clock, "alice", "bob");

    private async Task<(InMemoryConnection Connection, RecordingSurlEventPublisher Events)> ServeAsync(
        MailboxStore store, IEnumerable<ReadOnlyMemory<byte>> request, IFaultPlan faults, TimeProvider clock, SmtpProtocolServer? server = null)
    {
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(request);
        await (server ?? Server(store)).ServeAsync(connection, Context(clock, TestContext.CancellationToken) with { Faults = faults, Events = events });
        return (connection, events);
    }

    // curl --mail-rcpt-allowfails sends DATA after bob's 451, so alice still gets the message: exit 0.
    [TestMethod]
    public async Task ServeAsync_RcptFaultForBobUnderAllowFails_Answers451ToBobAndDeliversOnlyToAlice()
    {
        var clock = new ManualTimeProvider();
        var store = AliceAndBobStore(clock);
        var faults = new ScriptedFaultPlan(ErrorRule("RCPT", "bob@x"));

        var (connection, events) = await ServeAsync(store, [ReadBytes("fault-recipient-allowfails", "request.bin")], faults, clock);

        Assert.AreEqual(RecordedReplies("fault-recipient-allowfails"), Utf8(connection.WrittenBytes));
        Assert.HasCount(1, Inbox(store, "alice"));
        Assert.IsEmpty(Inbox(store, "bob"));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "bob@x"), events.Events.Single());
        var rcptPoints = faults.Points.Where(point => point.Command == "RCPT").ToList();
        CollectionAssert.AreEqual(new[] { "alice@x", "bob@x" }, rcptPoints.Select(point => point.Path).ToList());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command)
            {
                ExchangeId = 1,
                ListenUrl = "smtp://127.0.0.1:18025/",
                Scheme = "smtp",
                Command = "RCPT",
                Path = "alice@x",
            },
            rcptPoints[0]);
    }

    // Without --mail-rcpt-allowfails curl gives up at the 451 and quits: exit 55, nothing stored.
    [TestMethod]
    public async Task ServeAsync_RcptFaultForBob_Answers451AndCurlQuitsWithNothingStored()
    {
        var clock = new ManualTimeProvider();
        var store = AliceAndBobStore(clock);

        var (connection, events) = await ServeAsync(store, [ReadBytes("fault-recipient", "request.bin")], new ScriptedFaultPlan(ErrorRule("RCPT", "bob@x")), clock);

        Assert.AreEqual(RecordedReplies("fault-recipient"), Utf8(connection.WrittenBytes));
        Assert.IsEmpty(Inbox(store, "alice"));
        Assert.IsEmpty(Inbox(store, "bob"));
        Assert.HasCount(1, events.Events);
    }

    // ADR-0124 decision 4: a DATA rule answers in place of 354, before the message; curl exits 55.
    [TestMethod]
    public async Task ServeAsync_DataFault_AnswersInPlaceOf354AndStoresNothing()
    {
        var clock = new ManualTimeProvider();
        var store = AliceAndBobStore(clock);

        var (connection, events) = await ServeAsync(store, [ReadBytes("fault-data", "request.bin")], new ScriptedFaultPlan(ErrorRule("DATA")), clock);

        Assert.AreEqual(RecordedReplies("fault-data"), Utf8(connection.WrittenBytes));
        Assert.IsEmpty(Inbox(store, "alice"));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "DATA"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NoRule_AcceptsBothRecipientsAndPublishesNoFault()
    {
        var clock = new ManualTimeProvider();

        var (connection, events) = await ServeAsync(
            AliceAndBobStore(clock), Ascii("EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<alice@x>\r\nRCPT TO:<bob@x>\r\nQUIT\r\n"), new ScriptedFaultPlan(), clock);

        StringAssert.Contains(Utf8(connection.WrittenBytes), "250 2.1.5 Recipient OK\r\n250 2.1.5 Recipient OK\r\n");
        Assert.IsEmpty(events.Events);
    }

    // A rule scoped to account alice fires only once alice has logged in with AUTH.
    [TestMethod]
    [DataRow("alice", "451 4.3.0 try later\r\n", 1, DisplayName = "alice")]
    [DataRow("bob", "250 2.1.0 Sender OK\r\n", 0, DisplayName = "bob")]
    public async Task ServeAsync_RuleScopedToAlice_FiresOnlyForAlice(string accountName, string mailReply, int faultCount)
    {
        var clock = new ManualTimeProvider();
        var store = AliceAndBobStore(clock);
        var mailPolicy = new ScriptedMailAuthenticationPolicy(["PLAIN"], null, new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, accountName, null));
        var server = new SmtpProtocolServer(new AnonymousAuthenticationPolicy(), mailPolicy, store);
        var faults = new ScriptedFaultPlan(ErrorRule(command: "MAIL", account: "alice"));

        var (connection, events) = await ServeAsync(store, Ascii("EHLO c\r\nAUTH PLAIN AHVzZXIAc2VjcmV0\r\nMAIL FROM:<a@x>\r\n"), faults, clock, server);

        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "235 2.7.0 Authentication successful\r\n" + mailReply);
        Assert.HasCount(faultCount, events.Events);
        Assert.IsNull(faults.Points[0].Account);
        Assert.AreEqual(accountName, faults.Points[^1].Account);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRuleWithNoReply_AnswersLocalErrorInProcessing()
    {
        var clock = new ManualTimeProvider();

        var (connection, _) = await ServeAsync(AliceAndBobStore(clock), Ascii("NOOP\r\n"), new ScriptedFaultPlan(ErrorRule("NOOP", reply: null)), clock);

        Assert.AreEqual(Greeting + "451 4.3.0 Local error in processing\r\n", Utf8(connection.WrittenBytes));
    }

    // ADR-0141: a command-stage delay holds RCPT's answer on the exchange's clock, with the idle
    // clock paused for the whole hold, then answers it as usual.
    [TestMethod]
    public async Task ServeAsync_RcptDelayRule_HoldsTheAnswerUntilTheClockAdvances500Ms_WithTheIdleClockPaused()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Command = "RCPT", DelayMilliseconds = 500 };
        var clock = new ManualTimeProvider();
        var idleClock = new RecordingIdleClock();
        var (serving, connection, events) = StartServing(
            Ascii("EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<alice@x>\r\nQUIT\r\n"), new ScriptedFaultPlan(delay), clock, idleClock);

        await idleClock.FirstPause.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var writtenAtTheHold = Utf8(connection.WrittenBytes);
        clock.Advance(TimeSpan.FromMilliseconds(499));
        var writtenAfter499Ms = Utf8(connection.WrittenBytes);
        var pausedAfter499Ms = idleClock.IsPaused;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        StringAssert.EndsWith(writtenAtTheHold, "250 2.1.0 Sender OK\r\n");
        Assert.AreEqual(writtenAtTheHold, writtenAfter499Ms);
        Assert.IsTrue(pausedAfter499Ms);
        Assert.IsFalse(idleClock.IsPaused);
        StringAssert.StartsWith(Utf8(connection.WrittenBytes), writtenAtTheHold + "250 2.1.5 Recipient OK\r\n");
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "alice@x"), events.Events.Single());
    }

    // ADR-0141: a command-stage drop aborts the connection in place of bob's RCPT answer; curl's
    // request is replayed from Fixtures/fault-recipient up to the RCPT it never sees answered.
    [TestMethod]
    public async Task ServeAsync_RcptDropRule_AbortsTheConnectionWithNoAnswerToRcpt()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "RCPT", Path = "bob@x" };
        var clock = new ManualTimeProvider();
        var (serving, connection, events) = StartServing(
            [ReadBytes("fault-recipient", "request.bin")], new ScriptedFaultPlan(drop), clock, new RecordingIdleClock());

        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        var recorded = RecordedReplies("fault-recipient");
        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual(recorded[..recorded.IndexOf(TryLater, StringComparison.Ordinal)], Utf8(connection.WrittenBytes));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "bob@x"), events.Events.Single());
    }

    private (Task Serving, InMemoryConnection Connection, RecordingSurlEventPublisher Events) StartServing(
        IEnumerable<ReadOnlyMemory<byte>> request, IFaultPlan faults, ManualTimeProvider clock, RecordingIdleClock idleClock)
    {
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(request);
        var context = Context(clock, TestContext.CancellationToken) with { Faults = faults, Events = events, IdleClock = idleClock };

        return (Server(AliceAndBobStore(clock)).ServeAsync(connection, context), connection, events);
    }

    [TestMethod]
    public async Task ServeAsync_UnrecognizedCommand_AsksNoFaultPoint()
    {
        var clock = new ManualTimeProvider();
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (connection, _) = await ServeAsync(AliceAndBobStore(clock), Ascii("XYZZY\r\n"), faults, clock);

        Assert.AreEqual(Greeting + SmtpReplies.NotRecognized + "\r\n", Utf8(connection.WrittenBytes));
        Assert.IsEmpty(faults.Points);
    }
}
