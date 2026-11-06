using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;
using static Surl.Protocol.Pop3.RecordedFixtureTests;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4; BL-796): a command an <see cref="FaultKind.Error"/>
/// rule matches is answered with <c>-ERR</c> and the rule's text in place of its own, the maildrop
/// untouched, and one <see cref="FaultAppliedEvent"/> is published. The <c>RETR</c> request is
/// replayed from pinned upstream curl 8.21.0 (<c>Fixtures/fault-retr</c>).
/// </summary>
[TestClass]
public sealed class Pop3FaultTests
{
    private const string TryLater = "try later";

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule ErrorRule(string? command = null, string? account = null, string? reply = TryLater) =>
        new("f1", FaultKind.Error) { Scheme = "pop3", Command = command, Account = account, Reply = reply };

    private async Task<(string Written, RecordingSurlEventPublisher Events)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> request, IFaultPlan faults, PasswordLoginVerdict anonymousVerdict = PasswordLoginVerdict.RefusedAnonymous)
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore(["alice", "bob"], allowAnonymous: false, clock);
        Seed(store, "alice@x", [Message]);
        Seed(store, "bob@x", [Message]);
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(request);
        var policy = new Pop3TestPolicy { Verdict = PasswordLoginVerdict.Accepted, AnonymousVerdict = anonymousVerdict };

        await Server(store, policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken) with { Faults = faults, Events = events });

        return (Utf8(connection.WrittenBytes), events);
    }

    // curl pop3://.../1 gives up at the -ERR: exit 8, "Weird server reply".
    [TestMethod]
    public async Task ServeAsync_RetrFault_AnswersErrWithTheRulesText()
    {
        var faults = new ScriptedFaultPlan(ErrorRule("RETR"));

        var (written, events) = await ServeAsync([ReadBytes("fault-retr", "request.bin")], faults);

        Assert.AreEqual(RecordedReplies("fault-retr"), written);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "RETR"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command)
            {
                ExchangeId = 1,
                ListenUrl = "pop3://127.0.0.1:18110/",
                Scheme = "pop3",
                Account = "alice",
                Command = "RETR",
            },
            faults.Points.Single(point => point.Command == "RETR"));
    }

    [TestMethod]
    [DataRow("alice", "-ERR try later\r\n", 1, DisplayName = "alice")]
    [DataRow("bob", "+OK 1 133\r\n", 0, DisplayName = "bob")]
    public async Task ServeAsync_RuleScopedToAlice_FiresOnlyForAlice(string accountName, string statReply, int faultCount)
    {
        var faults = new ScriptedFaultPlan(ErrorRule(account: "alice"));

        var (written, events) = await ServeAsync(Ascii($"USER {accountName}\r\nPASS p\r\nSTAT\r\n"), faults);

        Assert.AreEqual(Greeting + "+OK User accepted\r\n+OK Logged in\r\n" + statReply, written);
        Assert.HasCount(faultCount, events.Events);
        Assert.IsNull(faults.Points[0].Account);
    }

    // A fault never skips the login check: a maildrop command before a login is refused first.
    [TestMethod]
    public async Task ServeAsync_RetrBeforeLogin_IsRefusedBeforeAnyFault()
    {
        var faults = new ScriptedFaultPlan(ErrorRule("RETR"));

        var (written, events) = await ServeAsync(Ascii("RETR 1\r\n"), faults);

        Assert.AreEqual(Greeting + "-ERR [AUTH] Authentication required\r\n", written);
        Assert.IsEmpty(faults.Points);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRuleWithNoReply_AnswersLocalErrorInProcessing()
    {
        var (written, _) = await ServeAsync(Ascii("CAPA\r\n"), new ScriptedFaultPlan(ErrorRule("CAPA", reply: null)));

        Assert.AreEqual(Greeting + "-ERR Local error in processing\r\n", written);
    }

    // Only an Error rule is answered by the server; a command-stage delay a plan returns is not one.
    [TestMethod]
    public async Task ServeAsync_CommandStageDelayRule_IsNotAnsweredAsAnError()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 10 };

        var (written, events) = await ServeAsync(Ascii("USER alice\r\nPASS p\r\nNOOP\r\n"), new ScriptedFaultPlan(delay));

        Assert.AreEqual(Greeting + "+OK User accepted\r\n+OK Logged in\r\n+OK\r\n", written);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_UnrecognizedCommand_AsksNoFaultPoint()
    {
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (written, _) = await ServeAsync(Ascii("XYZZY\r\n"), faults);

        Assert.AreEqual(Greeting + "-ERR Command not recognized\r\n", written);
        Assert.IsEmpty(faults.Points);
    }
}
