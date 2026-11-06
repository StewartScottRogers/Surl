namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class ScriptedFaultPlanTests
{
    [TestMethod]
    public async Task FindAsync_Unscripted_FindsNone()
    {
        var plan = new ScriptedFaultPlan();

        var rule = await plan.FindAsync(new FaultPoint(FaultStage.Accept), CancellationToken.None);

        Assert.IsNull(rule);
    }

    [TestMethod]
    public async Task FindAsync_SeveralRulesMatch_ReturnsTheFirstInTheOrderGiven()
    {
        var first = new FaultRule("f1", FaultKind.Delay) { DelayMilliseconds = 500 };
        var second = new FaultRule("f2", FaultKind.Drop);
        var plan = new ScriptedFaultPlan(first, second);

        var rule = await plan.FindAsync(new FaultPoint(FaultStage.Accept), CancellationToken.None);

        Assert.AreSame(first, rule);
    }

    [TestMethod]
    public async Task FindAsync_EarlierRuleDoesNotMatch_ReturnsTheFirstThatDoes()
    {
        var other = new FaultRule("f1", FaultKind.RefuseLogin) { Account = "bob" };
        var alice = new FaultRule("f2", FaultKind.RefuseLogin) { Account = "alice" };
        var plan = new ScriptedFaultPlan(other, alice);

        var rule = await plan.FindAsync(
            new FaultPoint(FaultStage.Login) { Account = "alice" }, CancellationToken.None);

        Assert.AreSame(alice, rule);
    }

    [TestMethod]
    public async Task FindAsync_EveryScopeAttributeAndTheStageMatch_ReturnsTheRule()
    {
        var scoped = ScopedRule();
        var plan = new ScriptedFaultPlan(scoped);

        var rule = await plan.FindAsync(ScopedPoint(), CancellationToken.None);

        Assert.AreSame(scoped, rule);
    }

    [TestMethod]
    public async Task FindAsync_AnyOneScopeAttributeOrTheStageDiffers_FindsNone()
    {
        var plan = new ScriptedFaultPlan(ScopedRule());
        FaultPoint[] points =
        [
            ScopedPoint() with { ListenUrl = "smtp://127.0.0.1:2526" },
            ScopedPoint() with { Scheme = "smtps" },
            ScopedPoint() with { Account = "bob" },
            ScopedPoint() with { Path = "bob@example.com" },
            ScopedPoint() with { Command = "rcpt" },
            ScopedPoint() with { Stage = FaultStage.Login },
            new FaultPoint(FaultStage.Command),
        ];

        foreach (var point in points)
        {
            Assert.IsNull(await plan.FindAsync(point, CancellationToken.None), point.ToString());
        }
    }

    [TestMethod]
    public async Task FindAsync_RuleWithTimes_IsNeverSpentByTheScript()
    {
        var once = new FaultRule("f1", FaultKind.Drop) { Times = 1 };
        var plan = new ScriptedFaultPlan(once);

        await plan.FindAsync(new FaultPoint(FaultStage.Accept), CancellationToken.None);
        var second = await plan.FindAsync(new FaultPoint(FaultStage.Accept), CancellationToken.None);

        Assert.AreSame(once, second);
    }

    [TestMethod]
    public async Task Points_EveryPointAsked_IsRecordedInOrder()
    {
        var plan = new ScriptedFaultPlan();
        var accept = new FaultPoint(FaultStage.Accept) { ExchangeId = 1 };
        var write = new FaultPoint(FaultStage.StoreWrite) { Path = "/uploads/a.txt" };

        await plan.FindAsync(accept, CancellationToken.None);
        await plan.FindAsync(write, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { accept, write }, plan.Points.ToArray());
    }

    [TestMethod]
    public void FindAsync_NullPoint_Throws()
    {
        var plan = new ScriptedFaultPlan();

        Assert.ThrowsExactly<ArgumentNullException>(() => plan.FindAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public void Constructor_NullRules_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ScriptedFaultPlan(null!));
    }

    [TestMethod]
    public async Task EmptyFaultPlan_AnyPoint_FindsNoneSynchronously()
    {
        var finding = EmptyFaultPlan.Instance.FindAsync(ScopedPoint(), CancellationToken.None);

        Assert.IsTrue(finding.IsCompletedSuccessfully);
        Assert.IsNull(await finding);
    }

    private static FaultRule ScopedRule() => new("f1", FaultKind.Error)
    {
        Listener = "smtp://127.0.0.1:2525",
        Scheme = "smtp",
        Account = "alice",
        Path = "carol@example.com",
        Command = "RCPT",
        Stage = FaultStage.Command,
        Reply = "451 4.3.0 try later",
    };

    private static FaultPoint ScopedPoint() => new(FaultStage.Command)
    {
        ExchangeId = 3,
        ListenUrl = "smtp://127.0.0.1:2525",
        Scheme = "smtp",
        Account = "alice",
        Path = "carol@example.com",
        Command = "RCPT",
    };
}
