using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9): a matching
/// <see cref="FaultKind.Error"/> rule answers a <c>PUB</c>, <c>HPUB</c> or <c>SUB</c> with
/// <c>-ERR '&lt;text&gt;'</c>, publishes or subscribes nothing and keeps the connection, publishing one
/// <see cref="FaultAppliedEvent"/>. Every line is written by hand from the NATS client protocol page.
/// </summary>
[TestClass]
public sealed class NatsFaultTests
{
    private const string AliceConnect = "CONNECT {\"user\":\"alice\",\"pass\":\"secret\"}\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_PubToARulesSubject_AnswersTheChosenErrAndAnotherSubjectIsServed()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(Rule("PUB", "foo"));

        var (answers, log) = await ServeAsync(plan, events, AliceConnect, "SUB > 1\r\nPUB foo 2\r\nhi\r\nPUB bar 2\r\nok\r\nPING\r\n");

        Assert.AreEqual("-ERR 'busy now'\r\nMSG bar 1 2\r\nok\r\nPONG\r\n", answers);
        AssertOneFaultApplied(events, "PUB foo");
        CollectionAssert.Contains(log.Notes.ToArray(), "PUB foo answered by fault rule f1 with -ERR 'busy now'.");
        var point = plan.Points.First(point => point.Command == "PUB");
        Assert.AreEqual(FaultStage.Command, point.Stage);
        Assert.AreEqual(1L, point.ExchangeId);
        Assert.AreEqual("nats://127.0.0.1:4222/", point.ListenUrl);
        Assert.AreEqual("nats", point.Scheme);
        Assert.AreEqual("alice", point.Account);
    }

    [TestMethod]
    public async Task ServeAsync_HpubUnderARule_AnswersTheChosenErr()
    {
        var events = new RecordingSurlEventPublisher();

        var (answers, _) = await ServeAsync(
            new ScriptedFaultPlan(Rule("HPUB", "foo")),
            events,
            "CONNECT {\"headers\":true,\"user\":\"alice\",\"pass\":\"secret\"}\r\n",
            "SUB foo 1\r\nHPUB foo 12 14\r\nNATS/1.0\r\n\r\nhi\r\nPING\r\n");

        Assert.AreEqual("-ERR 'busy now'\r\nPONG\r\n", answers);
        AssertOneFaultApplied(events, "HPUB foo");
    }

    [TestMethod]
    public async Task ServeAsync_SubUnderARule_AnswersTheChosenErrAndSubscribesNothing()
    {
        var events = new RecordingSurlEventPublisher();

        var (answers, _) = await ServeAsync(new ScriptedFaultPlan(Rule("SUB", "foo")), events, AliceConnect, "SUB foo 1\r\nPUB foo 1\r\na\r\nPING\r\n");

        Assert.AreEqual("-ERR 'busy now'\r\nPONG\r\n", answers);
        AssertOneFaultApplied(events, "SUB foo");
    }

    [TestMethod]
    public async Task ServeAsync_ATokenLoginAcceptedUnchecked_CarriesNoAccountToThePlan()
    {
        var plan = new ScriptedFaultPlan(Rule("SUB", "foo") with { Account = "alice" });
        var policy = new UnitTestNatsAuthenticationPolicy { Verdict = PasswordLoginVerdict.AcceptedUnchecked };

        var (answers, _) = await ServeAsync(plan, new RecordingSurlEventPublisher(), AliceConnect, "SUB foo 1\r\nPING\r\n", policy);

        Assert.AreEqual("PONG\r\n", answers);
        Assert.IsTrue(plan.Points.All(point => point.Account is null));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("it's")]
    [DataRow("a\nb")]
    public async Task ServeAsync_ARuleWhoseReplyIsNotErrText_IsNotedAndNotApplied(string? reply)
    {
        var events = new RecordingSurlEventPublisher();

        var (answers, log) = await ServeAsync(new ScriptedFaultPlan(Rule("SUB", "foo") with { Reply = reply }), events, AliceConnect, "SUB foo 1\r\nPUB foo 1\r\na\r\nPING\r\n");

        Assert.AreEqual("MSG foo 1 1\r\na\r\nPONG\r\n", answers);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(log.Notes.ToArray(), "SUB foo: fault rule f1 is not an error with -ERR text; not applied.");
    }

    [TestMethod]
    public async Task ServeAsync_ADelayRuleAtTheCommandStage_IsNotedAndNotApplied()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Command = "PUB", DelayMilliseconds = 10 });

        var (answers, log) = await ServeAsync(plan, events, AliceConnect, "SUB foo 1\r\nPUB foo 1\r\na\r\nPING\r\n");

        Assert.AreEqual("MSG foo 1 1\r\na\r\nPONG\r\n", answers);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(log.Notes.ToArray(), "PUB foo: fault rule f1 is not an error with -ERR text; not applied.");
    }

    [TestMethod]
    public async Task ServeAsync_NoRule_AsksThePlanAtEachPubAndSub()
    {
        var plan = new ScriptedFaultPlan();

        var (answers, _) = await ServeAsync(plan, new RecordingSurlEventPublisher(), AliceConnect, "SUB foo 1\r\nPUB foo 1\r\na\r\nPING\r\n");

        Assert.AreEqual("MSG foo 1 1\r\na\r\nPONG\r\n", answers);
        CollectionAssert.AreEqual(new[] { "SUB foo", "PUB foo" }, plan.Points.Select(point => $"{point.Command} {point.Path}").ToArray());
    }

    private static FaultRule Rule(string command, string path) =>
        new("f1", FaultKind.Error) { Scheme = "nats", Command = command, Path = path, Reply = "busy now" };

    private static void AssertOneFaultApplied(RecordingSurlEventPublisher events, string target) =>
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, target), events.Events.OfType<FaultAppliedEvent>().Single());

    private async Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(
        IFaultPlan plan, ISurlEventPublisher events, string connect, string operations, IAuthenticationPolicy? policy = null)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Encoding.UTF8.GetBytes(connect + operations)], new IPEndPoint(IPAddress.Loopback, 4222));
        var context = new ExchangeContext(
            1,
            new ListenUrl("nats", "127.0.0.1", 4222).WithBoundPort(4222),
            new IPEndPoint(IPAddress.Loopback, 4222),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            new ManualTimeProvider(),
            TestContext.CancellationToken)
        { Faults = plan, Events = events };

        await new NatsServer(policy ?? new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServeAsync(connection, context);

        var written = Encoding.UTF8.GetString(connection.WrittenBytes);
        return (written[(written.IndexOf("\r\n", StringComparison.Ordinal) + 2)..], log);
    }
}
