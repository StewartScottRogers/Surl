using Surl.Protocol.Abstractions;

namespace Surl.Core;

// Each connection gets an ITlsLoginJudge that asks the fault plan and publishes LoginJudgedEvent
// (ADR-0165, decisions 2 to 5; BL-1224).
public sealed partial class ServingEngineTests
{
    private static SurlEventSubscription SubscribeToLogins(SurlEventStream stream) =>
        stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is LoginJudgedEvent or FaultAppliedEvent });

    private static async Task<(ITlsLoginJudge Judge, long ExchangeId)> JudgeOfAnExchangeAsync(FaultPlan plan, SurlEventStream stream)
    {
        var factory = new FakeListenerFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateFaultingEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), stream, plan, server);
        var connection = new JudgeCapturingConnection();
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        return (connection.Judge!, exchange.Context.ExchangeId);
    }

    [TestMethod]
    public async Task ServeAsync_Connection_GetsAJudgeWhoseAcceptedLoginIsPublished()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = SubscribeToLogins(stream);
        var (judge, exchangeId) = await JudgeOfAnExchangeAsync(new FaultPlan(), stream);

        Assert.IsFalse(await judge.ShouldRefuseAsync("alice", CancellationToken.None));
        judge.Judged("alice", true);

        var judged = (LoginJudgedEvent)Drain(subscription).Single();
        Assert.AreEqual(exchangeId, judged.ExchangeId);
        Assert.AreEqual("http", judged.Scheme);
        Assert.AreEqual("alice", judged.Account);
        Assert.AreEqual("tls-srp", judged.Mechanism);
        Assert.IsTrue(judged.IsAccepted);
        Assert.IsFalse(judged.IsRefusedByFault);
    }

    [TestMethod]
    public async Task ShouldRefuseAsync_RefuseLoginRule_PublishesFaultAppliedThenARefusedJudgement()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = SubscribeToLogins(stream);
        var plan = new FaultPlan();
        var rule = plan.Add(new FaultRule("x", FaultKind.RefuseLogin) { Account = "alice", Times = 1 });
        var (judge, exchangeId) = await JudgeOfAnExchangeAsync(plan, stream);

        Assert.IsTrue(await judge.ShouldRefuseAsync("alice", CancellationToken.None));
        judge.Judged("alice", false);
        Assert.IsFalse(await judge.ShouldRefuseAsync("alice", CancellationToken.None));
        judge.Judged("alice", false);

        var events = Drain(subscription);
        Assert.HasCount(3, events);
        var applied = (FaultAppliedEvent)events[0];
        Assert.AreEqual(rule.Id, applied.RuleId);
        Assert.AreEqual(FaultStage.Login, applied.Stage);
        Assert.AreEqual(exchangeId, applied.ExchangeId);
        Assert.IsTrue(((LoginJudgedEvent)events[1]).IsRefusedByFault);
        Assert.IsFalse(((LoginJudgedEvent)events[2]).IsRefusedByFault);
    }

    [TestMethod]
    public async Task ShouldRefuseAsync_RuleOfAnotherKind_IsNotApplied()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = SubscribeToLogins(stream);
        var plan = new FaultPlan();
        plan.AddOverride(new FaultOverrideScope { Stage = FaultStage.Login }, (_, _, _) => ValueTask.FromResult<FaultRule?>(new FaultRule("x", FaultKind.Delay)));
        var (judge, _) = await JudgeOfAnExchangeAsync(plan, stream);

        Assert.IsFalse(await judge.ShouldRefuseAsync("alice", CancellationToken.None));
        Assert.IsEmpty(Drain(subscription));
    }

    [TestMethod]
    public async Task Judge_NullUserName_Throws()
    {
        var (judge, _) = await JudgeOfAnExchangeAsync(new FaultPlan(), new SurlEventStream(new ManualTimeProvider()));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await judge.ShouldRefuseAsync(null!, CancellationToken.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => judge.Judged(null!, true));
    }
}
