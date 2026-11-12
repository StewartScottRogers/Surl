using Surl.Protocol.Abstractions;
using static Surl.Protocol.Gopher.GopherTestExchange;

namespace Surl.Protocol.Gopher;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4; ADR-0138; BL-798): a selector an
/// <see cref="FaultKind.Error"/> rule matches is answered with an error item (type <c>3</c>)
/// carrying the rule's text, before the content store is asked, and one
/// <see cref="FaultAppliedEvent"/> is published. The selector <c>/a</c> is replayed from pinned
/// upstream curl 8.21.0 (<c>Fixtures/fault-selector</c>), which exits 0 on the answer.
/// </summary>
[TestClass]
public sealed class GopherSelectorFaultsTests
{
    private const string TryLater = "try later";

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule ErrorRule(string? path = "/a", string? reply = TryLater) =>
        new("f1", FaultKind.Error) { Scheme = "gopher", Path = path, Reply = reply };

    private async Task<(string Written, RecordingSurlEventPublisher Events, RecordingExchangeLog Log)> ServeAsync(byte[] request, IFaultPlan faults)
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(RecordedFixture.Whole(request));

        await Server().ServeAsync(connection, Context(TimeProvider.System, log, TestContext.CancellationToken) with { Faults = faults, Events = events });

        return (Utf8(connection.WrittenBytes), events, log);
    }

    [TestMethod]
    public async Task ServeAsync_SelectorUnderARule_GetsTheErrorItem()
    {
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (written, events, log) = await ServeAsync(RecordedFixture.ReadBytes("fault-selector", "request.bin"), faults);

        Assert.AreEqual(Utf8(RecordedFixture.ReadBytes("fault-selector", "stdout.bin")), written);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/a"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "gopher://127.0.0.1:18634/", Scheme = "gopher", Path = "/a" },
            faults.Points.Single());
        Assert.Contains("The selector was answered with the chosen error of fault rule f1.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_SelectorTheRuleDoesNotName_IsServed()
    {
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (written, events, _) = await ServeAsync(Ascii("/file.txt\r\n"), faults);

        Assert.AreEqual(FileBody, written);
        Assert.IsEmpty(events.Events);
        Assert.AreEqual("/file.txt", faults.Points.Single().Path);
    }

    [TestMethod]
    public async Task ServeAsync_NoSelectorRead_AsksNoFaultPoint()
    {
        var faults = new ScriptedFaultPlan(ErrorRule(path: null));

        var (written, _, _) = await ServeAsync([], faults);

        Assert.AreEqual(string.Empty, written);
        Assert.IsEmpty(faults.Points);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("try\tlater", DisplayName = "a TAB")]
    [DataRow("try\r\nlater", DisplayName = "a line break")]
    [DataRow("try\nlater", DisplayName = "a bare LF")]
    public async Task ServeAsync_ReplyThatDoesNotFit_IsNotAppliedAndIsNoted(string? reply)
    {
        var (written, events, log) = await ServeAsync(Ascii("/file.txt\r\n"), new ScriptedFaultPlan(ErrorRule(path: null, reply: reply)));

        Assert.AreEqual(FileBody, written);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to the selector: its reply does not fit the gopher error shape.", log.Notes);
    }

    // ADR-0138 decision 5 as amended (BL-914): a command-stage delay holds the selector's answer on
    // the exchange's clock, with the idle clock paused, then answers it as usual.
    [TestMethod]
    public async Task ServeAsync_CommandStageDelayRule_HoldsTheAnswerFor500MsThenAnswersAsUsual()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Path = "/file.txt", DelayMilliseconds = 500 };
        var clock = new ManualTimeProvider();
        var idleClock = new RecordingIdleClock();
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(RecordedFixture.Whole("/file.txt\r\n"u8.ToArray()));
        var serving = Server().ServeAsync(
            connection,
            Context(clock, new RecordingExchangeLog(), TestContext.CancellationToken) with { Faults = new ScriptedFaultPlan(delay), Events = events, IdleClock = idleClock });

        await idleClock.FirstPause.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var writtenAtTheHold = Utf8(connection.WrittenBytes);
        clock.Advance(TimeSpan.FromMilliseconds(499));
        var writtenAfter499Ms = Utf8(connection.WrittenBytes);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(string.Empty, writtenAtTheHold);
        Assert.AreEqual(string.Empty, writtenAfter499Ms);
        Assert.IsFalse(idleClock.IsPaused);
        Assert.AreEqual(FileBody, Utf8(connection.WrittenBytes));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "/file.txt"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_CommandStageDropRule_AbortsTheConnectionWithNothingWritten()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command };
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(RecordedFixture.Whole("/file.txt\r\n"u8.ToArray()));

        await Server().ServeAsync(
            connection,
            Context(TimeProvider.System, new RecordingExchangeLog(), TestContext.CancellationToken) with { Faults = new ScriptedFaultPlan(drop), Events = events });

        Assert.IsTrue(connection.Aborted);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "/file.txt"), events.Events.Single());
    }

    // The kinds a selector does not apply are noted and it is answered as without them.
    [TestMethod]
    public async Task ServeAsync_CommandStageRuleOfAnotherKind_IsNotAppliedAndIsNoted()
    {
        var other = new FaultRule("f1", FaultKind.OutOfSpace) { Stage = FaultStage.Command };

        var (written, events, log) = await ServeAsync(Ascii("/file.txt\r\n"), new ScriptedFaultPlan(other));

        Assert.AreEqual(FileBody, written);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to the selector: a OutOfSpace rule is not applied at a selector.", log.Notes);
    }

    [TestMethod]
    public void ErrorMenu_EncodesTheTextAsUtf8()
    {
        Assert.AreEqual("3échec\t\terror.host\t1\r\n.\r\n", Utf8(GopherSelectorFaults.ErrorMenu("échec")));
    }
}
