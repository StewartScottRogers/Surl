using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// The command-stage delay and drop every server applies the same way (ADR-0124, decision 4; ADR-0141).
/// </summary>
[TestClass]
public sealed class CommandStageFaultsTests
{
    private static readonly FaultPoint RetrPoint = new(FaultStage.Command) { ExchangeId = 7, Command = "RETR" };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FindAndApplyAsync_NoRuleMatches_ReturnsNull_AndDoesNothing()
    {
        var (context, events, idleClock, log) = NewContext(new ScriptedFaultPlan(), TimeProvider.System, TestContext.CancellationToken);
        var connection = new InMemoryConnection([]);

        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, RetrPoint, "RETR");

        Assert.IsNull(rule);
        Assert.IsEmpty(events.Events);
        Assert.AreEqual(0, idleClock.PauseCount);
        Assert.IsEmpty(log.Notes);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_ErrorRule_IsHandedBackUnapplied()
    {
        var error = new FaultRule("f1", FaultKind.Error) { Reply = "550 no" };
        var (context, events, _, _) = NewContext(new ScriptedFaultPlan(error), TimeProvider.System, TestContext.CancellationToken);
        var connection = new InMemoryConnection([]);

        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, RetrPoint, "RETR");

        Assert.AreSame(error, rule);
        Assert.IsEmpty(events.Events);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_DropRule_AbortsTheConnection_NotesAndPublishesIt()
    {
        var drop = new FaultRule("f2", FaultKind.Drop) { Stage = FaultStage.Command };
        var (context, events, idleClock, log) = NewContext(new ScriptedFaultPlan(drop), TimeProvider.System, TestContext.CancellationToken);
        var connection = new InMemoryConnection([]);

        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, RetrPoint, "INBOX");

        Assert.AreSame(drop, rule);
        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual(new FaultAppliedEvent("f2", FaultKind.Drop, FaultStage.Command, 7, "INBOX"), events.Events.Single());
        CollectionAssert.AreEqual(new[] { "RETR dropped the connection, unanswered, by fault rule f2." }, log.Notes.ToArray());
        Assert.AreEqual(0, idleClock.PauseCount);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_DelayRuleWithoutMilliseconds_HoldsNotAtAll_PausingAndRestartingTheIdleClock()
    {
        var delay = new FaultRule("f3", FaultKind.Delay) { Stage = FaultStage.Command };
        var (context, events, idleClock, log) = NewContext(new ScriptedFaultPlan(delay), TimeProvider.System, TestContext.CancellationToken);
        var connection = new InMemoryConnection([]);

        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, RetrPoint, "RETR");

        Assert.AreSame(delay, rule);
        Assert.AreEqual((1, 1), (idleClock.PauseCount, idleClock.RestartCount));
        Assert.AreEqual(new FaultAppliedEvent("f3", FaultKind.Delay, FaultStage.Command, 7, "RETR"), events.Events.Single());
        CollectionAssert.AreEqual(new[] { "RETR held for 0 s by fault rule f3." }, log.Notes.ToArray());
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_DelayRule_HoldsWithTheIdleClockPaused_UntilCancelled_ThenRestartsIt()
    {
        var delay = new FaultRule("f4", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 500 };
        using var exchangeEnd = new CancellationTokenSource();
        var (context, _, idleClock, log) = NewContext(new ScriptedFaultPlan(delay), new NeverFiringTimeProvider(), exchangeEnd.Token);

        var applying = CommandStageFaults.FindAndApplyAsync(context, new InMemoryConnection([]), RetrPoint, "RETR").AsTask();
        var heldWhilePaused = !applying.IsCompleted && idleClock.IsPaused;
        await exchangeEnd.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => applying);
        Assert.IsTrue(heldWhilePaused);
        Assert.IsFalse(idleClock.IsPaused);
        CollectionAssert.AreEqual(new[] { "RETR held for 0.5 s by fault rule f4." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task FindAndApplyAsync_NoContextConnectionOrPoint_Throws()
    {
        var (context, _, _, _) = NewContext(new ScriptedFaultPlan(), TimeProvider.System, TestContext.CancellationToken);
        var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => CommandStageFaults.FindAndApplyAsync(null!, connection, RetrPoint, "RETR").AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => CommandStageFaults.FindAndApplyAsync(context, null!, RetrPoint, "RETR").AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => CommandStageFaults.FindAndApplyAsync(context, connection, null!, "RETR").AsTask());
    }

    private static (ExchangeContext Context, RecordingSurlEventPublisher Events, RecordingIdleClock IdleClock, RecordingExchangeLog Log) NewContext(
        IFaultPlan faults, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var events = new RecordingSurlEventPublisher();
        var idleClock = new RecordingIdleClock();
        var log = new RecordingExchangeLog();
        var context = new ExchangeContext(
            7,
            new ListenUrl("pop3", "127.0.0.1", 110),
            new IPEndPoint(IPAddress.Loopback, 110),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            timeProvider,
            cancellationToken)
        {
            Faults = faults,
            Events = events,
            IdleClock = idleClock,
        };

        return (context, events, idleClock, log);
    }
}
