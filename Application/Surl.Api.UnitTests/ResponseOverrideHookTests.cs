using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-811: a response override's bounds - its time limit, a throw, an answer surl refuses or that cannot act at the
// point - each published as HookFailedEvent with the point served as if no override were there (ADR-0124, decision 5).
[TestClass]
public sealed class ResponseOverrideHookTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static readonly FaultPoint HttpGetA = new(FaultStage.Command)
    {
        ExchangeId = 1,
        ListenUrl = "http://127.0.0.1:49731/",
        Scheme = "http",
        Path = "/a",
        Command = "GET",
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DecideAsync_AnswerAProtocolError_IsTheRuleForThePointsScheme()
    {
        var events = new RecordingSurlEventPublisher();
        var hook = Hook((_, _) => ValueTask.FromResult<SurlFault?>(SurlFault.ProtocolError("503 Busy") with { Path = "/elsewhere", Times = 3 }), events);

        var rule = await hook.DecideAsync("f1", HttpGetA, TestContext.CancellationToken);

        Assert.IsNotNull(rule);
        Assert.AreEqual(FaultKind.Error, rule.Kind);
        Assert.AreEqual("503 Busy", rule.Reply);
        Assert.AreEqual("http", rule.Scheme);
        Assert.IsNull(rule.Path);
        Assert.IsNull(rule.Times);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task DecideAsync_AnswerNull_IsNoRule()
    {
        var events = new RecordingSurlEventPublisher();
        var hook = Hook((_, _) => ValueTask.FromResult<SurlFault?>(null), events);

        Assert.IsNull(await hook.DecideAsync("f1", HttpGetA, TestContext.CancellationToken));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task DecideAsync_DecisionThrows_PublishesThrewAndIsNoRule()
    {
        var events = new RecordingSurlEventPublisher();
        var hook = Hook((_, _) => throw new InvalidOperationException("boom"), events);

        var rule = await hook.DecideAsync("f4", HttpGetA, TestContext.CancellationToken);

        Assert.IsNull(rule);
        Assert.AreEqual(new HookFailedEvent("f4", HookFailure.Threw, "boom"), events.Events.Single());
    }

    [TestMethod]
    [DataRow("refuse-login", "A RefuseLogin fault cannot act at the Command stage. (Parameter 'answer')", DisplayName = "a kind that cannot act at the point")]
    [DataRow("error", "option --fault: key reply does not fit the http error shape", DisplayName = "a reply surl refuses")]
    public async Task DecideAsync_AnswerNoFaultRuleCanBeAtThePoint_PublishesThrewAndIsNoRule(string answer, string message)
    {
        var events = new RecordingSurlEventPublisher();
        var fault = answer == "error" ? SurlFault.ProtocolError("200 Fine") : SurlFault.RefuseLogin();
        var hook = Hook((_, _) => ValueTask.FromResult<SurlFault?>(fault), events);

        var rule = await hook.DecideAsync("f1", HttpGetA, TestContext.CancellationToken);

        Assert.IsNull(rule);
        var failed = (HookFailedEvent)events.Events.Single();
        Assert.AreEqual(HookFailure.Threw, failed.Failure);
        StringAssert.StartsWith(failed.Message, message);
    }

    [TestMethod]
    public async Task DecideAsync_DecisionPastTheLimit_PublishesTimedOutCancelsItAndIsNoRule()
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var decisionCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hook = Hook(
            async (_, token) =>
            {
                token.Register(decisionCancelled.SetResult);
                await Task.Delay(Timeout.Infinite, token);
                return null;
            },
            events,
            clock);

        var deciding = hook.DecideAsync("f2", HttpGetA, TestContext.CancellationToken).AsTask();
        await clock.WaitForTimerAsync(Limit, TestContext.CancellationToken);
        clock.Advance(Limit);
        var rule = await deciding.WaitAsync(Patience, TestContext.CancellationToken);

        Assert.IsNull(rule);
        Assert.AreEqual(
            new HookFailedEvent("f2", HookFailure.TimedOut, "The response override ran past its time limit of 00:00:00.1000000."),
            events.Events.Single());
        await decisionCancelled.Task.WaitAsync(Patience, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task DecideAsync_ExchangeCancelledWhileDeciding_ThrowsOperationCanceledAndPublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        using var exchange = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hook = Hook(
            async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return null;
            },
            events);

        var deciding = hook.DecideAsync("f1", HttpGetA, exchange.Token).AsTask();
        await started.Task.WaitAsync(Patience, TestContext.CancellationToken);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => deciding.WaitAsync(Patience, TestContext.CancellationToken));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task DecideAsync_ExchangeCancelledAndADecisionIgnoringItRunsPastTheLimit_ThrowsOperationCanceled()
    {
        var clock = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        using var exchange = new CancellationTokenSource();
        var never = new TaskCompletionSource<SurlFault?>();
        var hook = Hook((_, _) => new ValueTask<SurlFault?>(never.Task), events, clock);

        var deciding = hook.DecideAsync("f1", HttpGetA, exchange.Token).AsTask();
        await clock.WaitForTimerAsync(Limit, TestContext.CancellationToken);
        await exchange.CancelAsync();
        clock.Advance(Limit);

        await Assert.ThrowsAsync<OperationCanceledException>(() => deciding.WaitAsync(Patience, TestContext.CancellationToken));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task DecideAsync_DecisionThrowsOperationCanceledOfItsOwn_PublishesThrew()
    {
        var events = new RecordingSurlEventPublisher();
        var hook = Hook((_, _) => throw new OperationCanceledException("gave up"), events);

        Assert.IsNull(await hook.DecideAsync("f1", HttpGetA, TestContext.CancellationToken));
        Assert.AreEqual(new HookFailedEvent("f1", HookFailure.Threw, "gave up"), events.Events.Single());
    }

    private static ResponseOverrideHook Hook(
        Func<FaultPoint, CancellationToken, ValueTask<SurlFault?>> decide,
        RecordingSurlEventPublisher events,
        TimeProvider? clock = null,
        TimeSpan? limit = null) =>
        new(decide, events, clock ?? new ManualTimeProvider(), limit ?? Limit);
}
