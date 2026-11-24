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
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

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

        var applying = CommandStageFaults.FindAndApplyAsync(context, new InMemoryConnection([], peerHalfClosesWhenExhausted: false), RetrPoint, "RETR").AsTask();
        var heldWhilePaused = !applying.IsCompleted && idleClock.IsPaused;
        await exchangeEnd.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => applying);
        Assert.IsTrue(heldWhilePaused);
        Assert.IsFalse(idleClock.IsPaused);
        CollectionAssert.AreEqual(new[] { "RETR held for 0.5 s by fault rule f4." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task FindAndApplyAsync_ExchangeCancelledDuringAHoldOnAnOpenConnection_ThrowsCancelled_AndRestartsTheIdleClock()
    {
        var delay = new FaultRule("f4b", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 500 };
        using var exchangeEnd = new CancellationTokenSource();
        var (context, _, idleClock, _) = NewContext(new ScriptedFaultPlan(delay), new NeverFiringTimeProvider(), exchangeEnd.Token);
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var applying = CommandStageFaults.FindAndApplyAsync(context, connection, RetrPoint, "RETR").AsTask();
        await exchangeEnd.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => applying);
        Assert.AreEqual((1, 1), (idleClock.PauseCount, idleClock.RestartCount));
        Assert.IsFalse(connection.Aborted);
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

    [TestMethod]
    public async Task FindAndApplyAsync_WithoutAConnection_DropRule_IsNotedPublishedAndHandedBack()
    {
        var drop = new FaultRule("f5", FaultKind.Drop) { Stage = FaultStage.Command };
        var (context, events, idleClock, log) = NewContext(new ScriptedFaultPlan(drop), TimeProvider.System, TestContext.CancellationToken);

        var rule = await CommandStageFaults.FindAndApplyAsync(context, RetrPoint, "/a");

        Assert.AreSame(drop, rule);
        Assert.AreEqual(new FaultAppliedEvent("f5", FaultKind.Drop, FaultStage.Command, 7, "/a"), events.Events.Single());
        CollectionAssert.AreEqual(new[] { "RETR dropped the connection, unanswered, by fault rule f5." }, log.Notes.ToArray());
        Assert.AreEqual(0, idleClock.PauseCount);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_WithoutAConnection_DelayAndNoRule_HoldAndDoNothing()
    {
        var delay = new FaultRule("f6", FaultKind.Delay) { Stage = FaultStage.Command };
        var (held, heldEvents, heldIdleClock, _) = NewContext(new ScriptedFaultPlan(delay), TimeProvider.System, TestContext.CancellationToken);
        var (unheld, unheldEvents, _, _) = NewContext(new ScriptedFaultPlan(), TimeProvider.System, TestContext.CancellationToken);

        Assert.AreSame(delay, await CommandStageFaults.FindAndApplyAsync(held, RetrPoint, "RETR"));
        Assert.IsNull(await CommandStageFaults.FindAndApplyAsync(unheld, RetrPoint, "RETR"));

        Assert.AreEqual((1, 1), (heldIdleClock.PauseCount, heldIdleClock.RestartCount));
        Assert.HasCount(1, heldEvents.Events);
        Assert.IsEmpty(unheldEvents.Events);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_WithoutAConnection_NoContextOrPoint_Throws()
    {
        var (context, _, _, _) = NewContext(new ScriptedFaultPlan(), TimeProvider.System, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => CommandStageFaults.FindAndApplyAsync(null!, RetrPoint, "RETR").AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => CommandStageFaults.FindAndApplyAsync(context, null!, "RETR").AsTask());
    }

    [TestMethod]
    public async Task FindAndApplyAsync_ClientHalfClosesDuringADelay_EndsTheHoldAtOnce_NotesIt_AndRestartsTheIdleClock()
    {
        var delay = new FaultRule("f7", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 600000 };
        var (context, events, idleClock, log) = NewContext(new ScriptedFaultPlan(delay), new NeverFiringTimeProvider(), TestContext.CancellationToken);
        var connection = new InMemoryConnection([]);

        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, RetrPoint, "RETR");

        Assert.AreSame(delay, rule);
        Assert.AreEqual((1, 1), (idleClock.PauseCount, idleClock.RestartCount));
        Assert.AreEqual(1, events.Events.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                "RETR held for 600 s by fault rule f7.",
                "Exchange 7: the client closed the connection during the hold by fault rule f7.",
            },
            log.Notes.ToArray());
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_ClientResetsDuringADelay_EndsTheHoldAtOnce_AndNotesTheReset()
    {
        var delay = new FaultRule("f8", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 600000 };
        var (context, _, idleClock, log) = NewContext(new ScriptedFaultPlan(delay), new NeverFiringTimeProvider(), TestContext.CancellationToken);
        var connection = new InMemoryConnection([], peerResetsWhenExhausted: true);

        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, RetrPoint, "RETR");

        Assert.AreSame(delay, rule);
        Assert.AreEqual(1, idleClock.RestartCount);
        Assert.AreEqual("Exchange 7: the client reset the connection during the hold by fault rule f8.", log.Notes.Last());
    }

    [TestMethod]
    public async Task FindAndApplyAsync_WatchThrowsIOException_CountsAsAReset()
    {
        var delay = new FaultRule("f9", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 600000 };
        var (context, _, _, log) = NewContext(new ScriptedFaultPlan(delay), new NeverFiringTimeProvider(), TestContext.CancellationToken);

        await CommandStageFaults.FindAndApplyAsync(
            context, new WatchDouble(() => throw new IOException("reset")), RetrPoint, "RETR");

        StringAssert.Contains(log.Notes.Last(), "the client reset the connection");
    }

    [TestMethod]
    public async Task FindAndApplyAsync_ClientStays_HoldsTheWholeDelay_AndItsBytesAreReadInOrderAfterwards()
    {
        var delay = new FaultRule("f10", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 30 };
        var (context, _, idleClock, log) = NewContext(new ScriptedFaultPlan(delay), TimeProvider.System, TestContext.CancellationToken);
        var connection = new InMemoryConnection(
            [new byte[] { 1, 2 }, new byte[] { 3 }], peerHalfClosesWhenExhausted: false);

        await CommandStageFaults.FindAndApplyAsync(context, connection, RetrPoint, "RETR");
        var first = new byte[8];
        var count = await connection.ReadAsync(first, TestContext.CancellationToken);

        Assert.AreEqual(1, log.Notes.Count);
        Assert.AreEqual((1, 1), (idleClock.PauseCount, idleClock.RestartCount));
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, first[..count]);
    }

    [TestMethod]
    public async Task FindAndApplyAsync_WatchNotWatchedOrBufferFull_HoldsTheWholeDelay()
    {
        var delay = new FaultRule("f11", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 20 };
        foreach (var outcome in new[] { PeerWatchOutcome.NotWatched, PeerWatchOutcome.BufferFull })
        {
            var (context, _, _, log) = NewContext(new ScriptedFaultPlan(delay), TimeProvider.System, TestContext.CancellationToken);

            await CommandStageFaults.FindAndApplyAsync(context, new WatchDouble(() => outcome), RetrPoint, "RETR");

            Assert.AreEqual(1, log.Notes.Count, outcome.ToString());
        }
    }

    [TestMethod]
    public async Task FindAndApplyWithClientOutcomeAsync_ClientHalfClosesDuringADelay_ReportsPeerClosed()
    {
        var delay = new FaultRule("g1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 600000 };
        var (context, _, _, _) = NewContext(new ScriptedFaultPlan(delay), new NeverFiringTimeProvider(), TestContext.CancellationToken);

        var result = await CommandStageFaults.FindAndApplyWithClientOutcomeAsync(
            context, new InMemoryConnection([]), RetrPoint, "RETR");

        Assert.AreSame(delay, result.Rule);
        Assert.AreEqual(PeerWatchOutcome.PeerClosed, result.ClientLeft);
    }

    [TestMethod]
    public async Task FindAndApplyWithClientOutcomeAsync_ClientResetsDuringADelay_ReportsPeerReset()
    {
        var delay = new FaultRule("g2", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 600000 };
        var (context, _, _, _) = NewContext(new ScriptedFaultPlan(delay), new NeverFiringTimeProvider(), TestContext.CancellationToken);

        var result = await CommandStageFaults.FindAndApplyWithClientOutcomeAsync(
            context, new InMemoryConnection([], peerResetsWhenExhausted: true), RetrPoint, "RETR");

        Assert.AreEqual(PeerWatchOutcome.PeerReset, result.ClientLeft);
    }

    [TestMethod]
    public async Task FindAndApplyWithClientOutcomeAsync_CompletedHold_DropAndNoRule_ReportNotWatched()
    {
        var delay = new FaultRule("g3", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 20 };
        var drop = new FaultRule("g4", FaultKind.Drop) { Stage = FaultStage.Command };
        var (held, _, _, _) = NewContext(new ScriptedFaultPlan(delay), TimeProvider.System, TestContext.CancellationToken);
        var (dropped, _, _, _) = NewContext(new ScriptedFaultPlan(drop), TimeProvider.System, TestContext.CancellationToken);
        var (none, _, _, _) = NewContext(new ScriptedFaultPlan(), TimeProvider.System, TestContext.CancellationToken);
        var droppedConnection = new InMemoryConnection([]);

        var heldResult = await CommandStageFaults.FindAndApplyWithClientOutcomeAsync(
            held, new InMemoryConnection([], peerHalfClosesWhenExhausted: false), RetrPoint, "RETR");
        var droppedResult = await CommandStageFaults.FindAndApplyWithClientOutcomeAsync(dropped, droppedConnection, RetrPoint, "RETR");
        var noneResult = await CommandStageFaults.FindAndApplyWithClientOutcomeAsync(none, new InMemoryConnection([]), RetrPoint, "RETR");

        Assert.AreEqual(new CommandStageFaultResult(delay, PeerWatchOutcome.NotWatched), heldResult);
        Assert.AreEqual(new CommandStageFaultResult(drop, PeerWatchOutcome.NotWatched), droppedResult);
        Assert.IsTrue(droppedConnection.Aborted);
        Assert.AreEqual(new CommandStageFaultResult(null, PeerWatchOutcome.NotWatched), noneResult);
    }

    [TestMethod]
    public async Task FindAndApplyWithClientOutcomeAsync_NoConnection_Throws()
    {
        var (context, _, _, _) = NewContext(new ScriptedFaultPlan(), TimeProvider.System, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => CommandStageFaults.FindAndApplyWithClientOutcomeAsync(context, null!, RetrPoint, "RETR").AsTask());
    }

    private sealed class WatchDouble(Func<PeerWatchOutcome> outcome) : IConnection
    {
        public EndPoint LocalEndPoint => throw new NotSupportedException();

        public EndPoint RemoteEndPoint => throw new NotSupportedException();

        public TlsSession? TlsSession => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<PeerWatchOutcome> WatchForPeerCloseAsync(int maxBufferedBytes, CancellationToken cancellationToken) =>
            ValueTask.FromResult(outcome());

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Abort()
        {
        }

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
