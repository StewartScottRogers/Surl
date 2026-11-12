using Surl.Protocol.Abstractions;
using static Surl.Protocol.Dict.DictTestExchange;

namespace Surl.Protocol.Dict;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4; ADR-0138; BL-798): a recognized DICT command an
/// <see cref="FaultKind.Error"/> rule matches is answered with the rule's status line in place of
/// its own reply, the connection staying open, and one <see cref="FaultAppliedEvent"/> is published.
/// The <c>DEFINE * word</c> request is replayed from pinned upstream curl 8.21.0
/// (<c>Fixtures/fault-define</c>), which exits 0 on the answer.
/// </summary>
[TestClass]
public sealed class DictCommandFaultsTests
{
    private const string TryLater = "554 try later";

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule ErrorRule(string? command = null, string? path = null, string? reply = TryLater) =>
        new("f1", FaultKind.Error) { Scheme = "dict", Command = command, Path = path, Reply = reply };

    private async Task<(string Written, RecordingSurlEventPublisher Events, RecordingExchangeLog Log)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> request, IFaultPlan faults)
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(request);

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log) with { Faults = faults, Events = events });

        return (Utf8(connection.WrittenBytes), events, log);
    }

    [TestMethod]
    public async Task ServeAsync_DefineFault_AnswersTheRulesStatusLine()
    {
        var faults = new ScriptedFaultPlan(ErrorRule("DEFINE", "word"));

        var (written, events, log) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("fault-define")), faults);

        Assert.AreEqual(Utf8(RecordedFixture.ReadBytes("fault-define", "stdout.bin")), written);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "word"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command)
            {
                ExchangeId = 1,
                ListenUrl = "dict://127.0.0.1:18628/",
                Scheme = "dict",
                Path = "word",
                Command = "DEFINE",
            },
            faults.Points.Single(point => point.Command == "DEFINE"));
        Assert.Contains("DEFINE answered with the chosen error of fault rule f1.", log.Notes);
    }

    [TestMethod]
    [DataRow("DEFINE ! hello\r\n", "hello", DisplayName = "DEFINE names the word")]
    [DataRow("MATCH ! . hel\r\n", "hel", DisplayName = "MATCH names the word")]
    [DataRow("SHOW INFO surl\r\n", "surl", DisplayName = "SHOW INFO names the database")]
    [DataRow("show info surl\r\n", "surl", DisplayName = "show info in lower case names the database")]
    [DataRow("SHOW DB\r\n", null, DisplayName = "SHOW DB names nothing")]
    [DataRow("SHOW SERVER x\r\n", null, DisplayName = "SHOW with another subject names nothing")]
    [DataRow("DEFINE hello\r\n", null, DisplayName = "DEFINE missing a word names nothing")]
    [DataRow("MATCH ! hel\r\n", null, DisplayName = "MATCH missing a word names nothing")]
    [DataRow("STATUS\r\n", null, DisplayName = "STATUS names nothing")]
    public async Task ServeAsync_RecognizedCommand_AsksThePlanWithItsPath(string line, string? path)
    {
        var faults = new ScriptedFaultPlan();

        await ServeAsync(Ascii(line), faults);

        Assert.AreEqual(path, faults.Points.Single().Path);
    }

    [TestMethod]
    public async Task ServeAsync_RuleForWordA_FiresOnlyForWordA()
    {
        var faults = new ScriptedFaultPlan(ErrorRule(path: "hello"));

        var (written, events, _) = await ServeAsync(Ascii("DEFINE ! world\r\nDEFINE ! hello\r\nQUIT\r\n"), faults);

        StringAssert.StartsWith(written, Banner + "150 1 definitions retrieved\r\n");
        StringAssert.EndsWith(written, "250 ok\r\n554 try later\r\n221 bye\r\n");
        Assert.AreEqual("hello", ((FaultAppliedEvent)events.Events.Single()).Target);
    }

    [TestMethod]
    public async Task ServeAsync_FaultOnACommandWithNoPath_NamesTheCommandAsTheTarget()
    {
        var (written, events, _) = await ServeAsync(Ascii("status\r\n"), new ScriptedFaultPlan(ErrorRule("STATUS")));

        Assert.AreEqual(Banner + "554 try later\r\n", written);
        Assert.AreEqual("STATUS", ((FaultAppliedEvent)events.Events.Single()).Target);
    }

    [TestMethod]
    public async Task ServeAsync_UnrecognizedOrMalformedCommand_AsksNoFaultPoint()
    {
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (written, events, _) = await ServeAsync(Ascii("XYZZY\r\nDEFINE \"open\r\n"), faults);

        Assert.AreEqual(Banner + "500 unknown command\r\n501 syntax error, illegal parameters\r\n", written);
        Assert.IsEmpty(faults.Points);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("554", DisplayName = "no text")]
    [DataRow("554 ", DisplayName = "empty text")]
    [DataRow("450 try later", DisplayName = "status below 500")]
    [DataRow("600 try later", DisplayName = "status above 599")]
    [DataRow("5x4 try later", DisplayName = "status not digits")]
    [DataRow("5540try later", DisplayName = "no space after the status")]
    [DataRow("554 try\r\n250 ok", DisplayName = "a line break")]
    [DataRow("554 try\nlater", DisplayName = "a bare LF")]
    public async Task ServeAsync_ReplyThatDoesNotFit_IsNotAppliedAndIsNoted(string? reply)
    {
        var (written, events, log) = await ServeAsync(Ascii("STATUS\r\n"), new ScriptedFaultPlan(ErrorRule(reply: reply)));

        Assert.AreEqual(Banner + "210 status ok\r\n", written);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to DICT STATUS: its reply does not fit the dict error shape.", log.Notes);
    }

    // ADR-0138 decision 5 as amended (BL-914): a command-stage delay holds the command's answer on
    // the exchange's clock, with the idle clock paused, then answers it as usual.
    [TestMethod]
    public async Task ServeAsync_CommandStageDelayRule_HoldsTheAnswerFor500MsThenAnswersAsUsual()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Command = "STATUS", DelayMilliseconds = 500 };
        var clock = new ManualTimeProvider();
        var idleClock = new RecordingIdleClock();
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(Ascii("STATUS\r\nQUIT\r\n"));
        var serving = Server().ServeAsync(
            connection,
            Context(clock, TestContext.CancellationToken) with { Faults = new ScriptedFaultPlan(delay), Events = events, IdleClock = idleClock });

        await idleClock.FirstPause.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var writtenAtTheHold = Utf8(connection.WrittenBytes);
        clock.Advance(TimeSpan.FromMilliseconds(499));
        var writtenAfter499Ms = Utf8(connection.WrittenBytes);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(Banner, writtenAtTheHold);
        Assert.AreEqual(Banner, writtenAfter499Ms);
        Assert.IsFalse(idleClock.IsPaused);
        Assert.AreEqual(Banner + "210 status ok\r\n221 bye\r\n", Utf8(connection.WrittenBytes));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "STATUS"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_CommandStageDropRule_AbortsTheConnectionWithNothingWrittenForTheCommand()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "DEFINE", Path = "hello" };
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(Ascii("STATUS\r\nDEFINE ! hello\r\nSTATUS\r\n"));

        await Server().ServeAsync(
            connection,
            Context(new ManualTimeProvider(), TestContext.CancellationToken) with { Faults = new ScriptedFaultPlan(drop), Events = events });

        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual(Banner + "210 status ok\r\n", Utf8(connection.WrittenBytes));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "hello"), events.Events.Single());
    }

    // The kinds a command does not apply are noted and the command is answered as without them.
    [TestMethod]
    public async Task ServeAsync_CommandStageRuleOfAnotherKind_IsNotAppliedAndIsNoted()
    {
        var other = new FaultRule("f1", FaultKind.OutOfSpace) { Stage = FaultStage.Command };

        var (written, events, log) = await ServeAsync(Ascii("STATUS\r\n"), new ScriptedFaultPlan(other));

        Assert.AreEqual(Banner + "210 status ok\r\n", written);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to DICT STATUS: a OutOfSpace rule is not applied at a command.", log.Notes);
    }
}
