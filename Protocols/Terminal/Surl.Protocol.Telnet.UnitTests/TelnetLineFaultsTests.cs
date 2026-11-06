using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Telnet;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4; ADR-0138; BL-798): TELNET has no error reply, so
/// a line an <see cref="FaultKind.Error"/> rule matches is answered with the rule's text as one
/// line, the connection then closed, and one <see cref="FaultAppliedEvent"/> is published. The
/// session is replayed from pinned upstream curl 8.21.0 (<c>Fixtures/fault-line</c>), which exits 0
/// on the answer.
/// </summary>
[TestClass]
public sealed class TelnetLineFaultsTests
{
    private const string TryLater = "try later";

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule ErrorRule(string? reply = TryLater) =>
        new("f1", FaultKind.Error) { Scheme = "telnet", Reply = reply };

    private async Task<(byte[] Written, RecordingSurlEventPublisher Events, RecordingExchangeLog Log)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> request, IFaultPlan faults)
    {
        var events = new RecordingSurlEventPublisher();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(request, peerHalfClosesWhenExhausted: false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var context = new ExchangeContext(
            1,
            new ListenUrl("telnet", "127.0.0.1", 18623).WithBoundPort(18623),
            new IPEndPoint(IPAddress.Loopback, 18623),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            TimeProvider.System,
            timeout.Token)
        {
            Faults = faults,
            Events = events,
        };

        await new TelnetProtocolServer().ServeAsync(connection, context);

        return (connection.WrittenBytes, events, log);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "whole")]
    [DataRow(true, DisplayName = "one byte per read")]
    public async Task ServeAsync_LineUnderARule_IsAnsweredWithTheRulesTextAndClosed(bool oneBytePerRead)
    {
        var request = RecordedFixture.ReadRequestBytes("fault-line");
        var faults = new ScriptedFaultPlan(ErrorRule());

        var (written, events, log) = await ServeAsync(oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request), faults);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("fault-line", "replies.bin"), written);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "line 1"), events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "telnet://127.0.0.1:18623/", Scheme = "telnet" },
            faults.Points.Single());
        Assert.Contains("line 1 was answered with the chosen error of fault rule f1; the connection was closed.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RuleSpentAfterOneLine_FaultsTheSecondLineWithItsNumber()
    {
        var faults = new OnceSkippingFaultPlan(ErrorRule("échec"));

        var (written, events, _) = await ServeAsync(RecordedFixture.Whole("one\ntwo\nthree\n"u8.ToArray()), faults);

        StringAssert.EndsWith(Encoding.UTF8.GetString(written), "one\r\néchec\r\n");
        Assert.AreEqual("line 2", ((FaultAppliedEvent)events.Events.Single()).Target);
        Assert.AreEqual(2, faults.AskedCount);
    }

    [TestMethod]
    public async Task ServeAsync_NoRule_AsksAtEveryLineAndAnswersAsUsual()
    {
        var faults = new ScriptedFaultPlan();

        var (written, events, _) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("plain-session")), faults);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("plain-session", "replies.bin"), written);
        Assert.HasCount(2, faults.Points);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("try\r\nlater", DisplayName = "a line break")]
    [DataRow("try\rlater", DisplayName = "a bare CR")]
    public async Task ServeAsync_ReplyThatDoesNotFit_IsNotAppliedAndIsNoted(string? reply)
    {
        var (written, events, log) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("plain-session")), new ScriptedFaultPlan(ErrorRule(reply)));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("plain-session", "replies.bin"), written);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to line 1: its reply does not fit the telnet error shape.", log.Notes);
    }

    // Only an Error rule is answered by the server; command-stage delay and drop are BL-912's.
    [TestMethod]
    public async Task ServeAsync_CommandStageDelayRule_IsNotAppliedAndIsNoted()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 10 };

        var (written, events, log) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("plain-session")), new ScriptedFaultPlan(delay));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("plain-session", "replies.bin"), written);
        Assert.IsEmpty(events.Events);
        Assert.Contains("Fault rule f1 not applied to line 2: a Delay rule is not applied at a line.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RuleScopedToACommand_NeverMatches()
    {
        var faults = new ScriptedFaultPlan(ErrorRule() with { Command = "quit" });

        var (written, events, _) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("plain-session")), faults);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("plain-session", "replies.bin"), written);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow("telnet", "127.0.0.1", 23, null, "telnet://127.0.0.1:23/")]
    [DataRow("telnet", "127.0.0.1", 0, 41000, "telnet://127.0.0.1:41000/")]
    [DataRow("telnet", "fe80::1%12", 23, null, "telnet://[fe80::1%2512]:23/")]
    public void FormatListenUrl_GivesTheFormTheEngineReports(string scheme, string host, int port, int? boundPort, string expected)
    {
        var listenUrl = new ListenUrl(scheme, host, port);
        listenUrl = boundPort is { } bound ? listenUrl.WithBoundPort(bound) : listenUrl;

        Assert.AreEqual(expected, TelnetLineFaults.FormatListenUrl(listenUrl));
    }

    // A plan whose one rule matches every point but the first, as a rule that skips one line would.
    private sealed class OnceSkippingFaultPlan(FaultRule rule) : IFaultPlan
    {
        public int AskedCount { get; private set; }

        public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken)
        {
            AskedCount++;

            return ValueTask.FromResult(AskedCount == 1 ? null : rule);
        }
    }
}
