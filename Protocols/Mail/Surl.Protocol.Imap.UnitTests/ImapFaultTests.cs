using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;
using static Surl.Protocol.Imap.RecordedFixtureTests;

namespace Surl.Protocol.Imap;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4; BL-796): a command an <see cref="FaultKind.Error"/>
/// rule matches is answered with a tagged <c>NO</c> or <c>BAD</c> - the rule's reply - in place of
/// its own, the store untouched, and one <see cref="FaultAppliedEvent"/> is published. The
/// <c>SELECT</c> request is replayed from pinned upstream curl 8.21.0 (<c>Fixtures/fault-select</c>).
/// </summary>
[TestClass]
public sealed class ImapFaultTests
{
    private const string TryLater = "NO try later";

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule ErrorRule(string? command = null, string? path = null, string? account = null, string? reply = TryLater) =>
        new("f1", FaultKind.Error) { Scheme = "imap", Command = command, Path = path, Account = account, Reply = reply };

    private async Task<(string Responses, RecordingSurlEventPublisher Events)> ServeAsync(IEnumerable<ReadOnlyMemory<byte>> request, IFaultPlan faults)
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, "alice", "bob");
        Deliver(store, "alice", 1);
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(request);

        await Server(store, new ScriptedLoginPolicy()).ServeAsync(connection, Context(clock, TestContext.CancellationToken) with { Faults = faults, Events = events });

        return (Utf8(connection.WrittenBytes), events);
    }

    // curl 'imap://.../INBOX;UID=1' gives up at the tagged NO: exit 67, "Select failed".
    [TestMethod]
    public async Task ServeAsync_SelectInboxFault_AnswersTheTaggedNoWithTheRulesText()
    {
        var faults = new ScriptedFaultPlan(ErrorRule("SELECT", "INBOX"));

        var (responses, events) = await ServeAsync([ReadBytes("fault-select", "request.bin")], faults);

        Assert.AreEqual(RecordedResponses("fault-select"), responses);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "INBOX"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command)
            {
                ExchangeId = 1,
                ListenUrl = "imap://127.0.0.1:18143/",
                Scheme = "imap",
                Account = "alice",
                Path = "INBOX",
                Command = "SELECT",
            },
            faults.Points.Single(point => point.Command == "SELECT"));
    }

    [TestMethod]
    [DataRow("alice", "A2 NO try later\r\n", 1, DisplayName = "alice")]
    [DataRow("bob", "A2 OK [READ-WRITE] SELECT completed\r\n", 0, DisplayName = "bob")]
    public async Task ServeAsync_RuleScopedToAlice_FiresOnlyForAlice(string accountName, string selectCompletion, int faultCount)
    {
        var faults = new ScriptedFaultPlan(ErrorRule("SELECT", account: "alice"));

        var (responses, events) = await ServeAsync(Bytes($"A1 LOGIN {accountName} p\r\nA2 SELECT INBOX\r\n"), faults);

        StringAssert.EndsWith(responses, selectCompletion);
        Assert.HasCount(faultCount, events.Events);
        Assert.IsNull(faults.Points[0].Account);
    }

    // A UID command is asked about by its own verb, and a selected-state command names the selected mailbox.
    [TestMethod]
    public async Task ServeAsync_UidFetchFault_IsAskedAsFetchOnTheSelectedMailbox()
    {
        var faults = new ScriptedFaultPlan(ErrorRule("FETCH", "INBOX"));

        var (responses, events) = await ServeAsync(Bytes("A1 LOGIN alice p\r\nA2 SELECT INBOX\r\nA3 UID FETCH 1 BODY[]\r\n"), faults);

        StringAssert.EndsWith(responses, "A2 OK [READ-WRITE] SELECT completed\r\nA3 NO try later\r\n");
        Assert.AreEqual("INBOX", events.Events.OfType<FaultAppliedEvent>().Single().Target);
    }

    [TestMethod]
    public async Task ServeAsync_UidWithNoCommand_IsAskedAsUid()
    {
        var faults = new ScriptedFaultPlan();

        await ServeAsync(Bytes("A1 LOGIN alice p\r\nA2 SELECT INBOX\r\nA3 UID\r\n"), faults);

        Assert.AreEqual("UID", faults.Points[^1].Command);
    }

    [TestMethod]
    public async Task ServeAsync_SelectWithNoMailbox_IsAskedWithNoPath()
    {
        var faults = new ScriptedFaultPlan();

        await ServeAsync(Bytes("A1 LOGIN alice p\r\nA2 SELECT\r\n"), faults);

        Assert.AreEqual("SELECT", faults.Points[^1].Command);
        Assert.IsNull(faults.Points[^1].Path);
    }

    // ADR-0124 decision 4: the fault is answered in place of "+", before the message.
    [TestMethod]
    public async Task ServeAsync_AppendFault_AnswersBeforeTheContinuationAndStoresNothing()
    {
        var faults = new ScriptedFaultPlan(ErrorRule("APPEND", "INBOX"));

        var (responses, events) = await ServeAsync(Bytes("A1 LOGIN alice p\r\nA2 APPEND INBOX {5}\r\n"), faults);

        StringAssert.EndsWith(responses, "A1 OK LOGIN completed\r\nA2 NO try later\r\n");
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "INBOX"), events.Events.Single());
    }

    // A fault never skips a login check: the state's refusal is answered first and no point is asked.
    [TestMethod]
    public async Task ServeAsync_SelectBeforeLogin_IsRefusedForTheStateBeforeAnyFault()
    {
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (responses, events) = await ServeAsync(Bytes("A1 SELECT INBOX\r\n"), faults);

        StringAssert.EndsWith(responses, "A1 " + ImapResponses.LoginRequired + "\r\n");
        Assert.IsEmpty(faults.Points);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRuleWithNoReply_AnswersNoUnavailable()
    {
        var (responses, _) = await ServeAsync(Bytes("A1 NOOP\r\n"), new ScriptedFaultPlan(ErrorRule("NOOP", reply: null)));

        StringAssert.EndsWith(responses, "A1 NO [UNAVAILABLE] Local error in processing\r\n");
    }

    // Only an Error rule is answered by the server; a command-stage delay a plan returns is not one.
    [TestMethod]
    public async Task ServeAsync_CommandStageDelayRule_IsNotAnsweredAsAnError()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 10 };

        var (responses, events) = await ServeAsync(Bytes("A1 NOOP\r\n"), new ScriptedFaultPlan(delay));

        StringAssert.EndsWith(responses, "A1 OK NOOP completed\r\n");
        Assert.IsEmpty(events.Events);
    }
}
