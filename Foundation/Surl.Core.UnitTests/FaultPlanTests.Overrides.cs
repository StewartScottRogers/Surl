using Surl.Protocol.Abstractions;

namespace Surl.Core;

// BL-811: overrides, rules whose answer a Surl.Api hook computes, in the plan's order (ADR-0124, decision 5).
public sealed partial class FaultPlanTests
{
    private static readonly FaultPoint HttpGetA = new(FaultStage.Command)
    {
        ExchangeId = 7,
        ListenUrl = "http://127.0.0.1:8080/",
        Scheme = "http",
        Account = "alice",
        Path = "/a",
        Command = "GET",
    };

    [TestMethod]
    public async Task FindAsync_OverrideAnswersARule_ReturnsItUnderTheOverridesId()
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Scheme = "imap" });
        var asked = new List<(string Id, FaultPoint Point)>();
        var id = plan.AddOverride(new FaultOverrideScope(), (overrideId, point, _) =>
        {
            asked.Add((overrideId, point));
            return ValueTask.FromResult<FaultRule?>(new FaultRule("answer", FaultKind.Error) { Reply = "503 Busy" });
        });

        var found = await plan.FindAsync(HttpGetA, CancellationToken.None);

        Assert.AreEqual("f2", id);
        Assert.AreEqual(new FaultRule("f2", FaultKind.Error) { Reply = "503 Busy" }, found);
        Assert.AreEqual(("f2", HttpGetA), asked.Single());
        Assert.HasCount(1, plan.Rules);
    }

    [TestMethod]
    public async Task FindAsync_OverrideAnswersNull_GoesOnToTheNextRuleAndSpendsIt()
    {
        var plan = new FaultPlan();
        plan.AddOverride(new FaultOverrideScope(), (_, _, _) => ValueTask.FromResult<FaultRule?>(null));
        var next = plan.Add(new FaultRule("x", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 5, Times = 1 });

        Assert.AreSame(next, await plan.FindAsync(HttpGetA, CancellationToken.None));
        Assert.IsNull(await plan.FindAsync(HttpGetA, CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_RuleBeforeAnOverride_WinsWithoutAskingIt()
    {
        var plan = new FaultPlan();
        var first = plan.Add(new FaultRule("x", FaultKind.Error) { Reply = "500 x" });
        var askedCount = 0;
        plan.AddOverride(new FaultOverrideScope(), (_, _, _) =>
        {
            askedCount++;
            return ValueTask.FromResult<FaultRule?>(null);
        });

        Assert.AreSame(first, await plan.FindAsync(HttpGetA, CancellationToken.None));
        Assert.AreEqual(0, askedCount);
    }

    [TestMethod]
    [DataRow(FaultStage.Command, null, null, null, null, null, true, DisplayName = "the point's stage")]
    [DataRow(FaultStage.Login, null, null, null, null, null, false, DisplayName = "another stage")]
    [DataRow(null, "http://127.0.0.1:0", "HTTP", "alice", "/a", "get", true, DisplayName = "every attribute matching")]
    [DataRow(null, "ftp://127.0.0.1:0", null, null, null, null, false, DisplayName = "another listener")]
    [DataRow(null, null, "ftp", null, null, null, false, DisplayName = "another scheme")]
    [DataRow(null, null, null, "bob", null, null, false, DisplayName = "another account")]
    [DataRow(null, null, null, null, "/b", null, false, DisplayName = "another path")]
    [DataRow(null, null, null, null, null, "PUT", false, DisplayName = "another command")]
    public async Task FindAsync_OverrideScope_AsksOnlyAtPointsItMatches(
        FaultStage? stage, string? listener, string? scheme, string? account, string? path, string? command, bool asks)
    {
        var plan = new FaultPlan();
        var scope = new FaultOverrideScope
        {
            Stage = stage,
            Listener = listener,
            Scheme = scheme,
            Account = account,
            Path = path,
            Command = command,
        };
        var askedCount = 0;
        plan.AddOverride(scope, (_, _, _) =>
        {
            askedCount++;
            return ValueTask.FromResult<FaultRule?>(null);
        });

        Assert.IsNull(await plan.FindAsync(HttpGetA, CancellationToken.None));
        Assert.AreEqual(asks ? 1 : 0, askedCount);
    }

    [TestMethod]
    public async Task Remove_AnOverride_StopsItBeingAskedAndFindCompletesSynchronouslyAgain()
    {
        var plan = new FaultPlan();
        var id = plan.AddOverride(new FaultOverrideScope(), (_, _, _) =>
            ValueTask.FromResult<FaultRule?>(new FaultRule("y", FaultKind.Drop)));

        Assert.IsTrue(plan.Remove(id));
        var finding = plan.FindAsync(HttpGetA, CancellationToken.None);

        Assert.IsTrue(finding.IsCompletedSuccessfully);
        Assert.IsNull(await finding);
    }

    [TestMethod]
    public async Task Clear_RemovesOverrides()
    {
        var plan = new FaultPlan();
        plan.AddOverride(new FaultOverrideScope(), (_, _, _) => ValueTask.FromResult<FaultRule?>(new FaultRule("y", FaultKind.Drop)));

        plan.Clear();

        Assert.IsTrue(plan.FindAsync(HttpGetA, CancellationToken.None).IsCompletedSuccessfully);
        Assert.AreEqual("f2", plan.Add(new FaultRule("z", FaultKind.Drop)).Id);
    }

    [TestMethod]
    public async Task FindAsync_EntriesRemovedWhileAnOverrideDecides_AreSkipped()
    {
        var plan = new FaultPlan();
        string? laterOverrideId = null;
        string? laterRuleId = null;
        plan.AddOverride(new FaultOverrideScope(), (_, _, _) =>
        {
            plan.Remove(laterOverrideId!);
            plan.Remove(laterRuleId!);
            return ValueTask.FromResult<FaultRule?>(null);
        });
        laterOverrideId = plan.AddOverride(new FaultOverrideScope(), (_, _, _) =>
            ValueTask.FromResult<FaultRule?>(new FaultRule("y", FaultKind.Drop)));
        laterRuleId = plan.Add(new FaultRule("x", FaultKind.Error) { Reply = "500 x" }).Id;

        Assert.IsNull(await plan.FindAsync(HttpGetA, CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_WithAnOverride_ASpentRuleIsNotFound()
    {
        var plan = new FaultPlan();
        plan.AddOverride(new FaultOverrideScope { Scheme = "ftp" }, (_, _, _) => ValueTask.FromResult<FaultRule?>(null));
        plan.Add(new FaultRule("x", FaultKind.Error) { Reply = "500 x", Times = 1 });

        Assert.IsNotNull(await plan.FindAsync(HttpGetA, CancellationToken.None));
        Assert.IsNull(await plan.FindAsync(HttpGetA, CancellationToken.None));
    }

    [TestMethod]
    public void AddOverride_PastMaxRules_Throws()
    {
        var plan = new FaultPlan();
        for (var count = 0; count < FaultPlan.MaxRules; count++)
        {
            plan.AddOverride(new FaultOverrideScope(), (_, _, _) => ValueTask.FromResult<FaultRule?>(null));
        }

        Assert.ThrowsExactly<InvalidOperationException>(
            () => plan.AddOverride(new FaultOverrideScope(), (_, _, _) => ValueTask.FromResult<FaultRule?>(null)));
        Assert.ThrowsExactly<InvalidOperationException>(() => plan.Add(new FaultRule("x", FaultKind.Drop)));
    }

    [TestMethod]
    public void AddOverride_NullArguments_Throw()
    {
        var plan = new FaultPlan();

        Assert.ThrowsExactly<ArgumentNullException>(
            () => plan.AddOverride(null!, (_, _, _) => ValueTask.FromResult<FaultRule?>(null)));
        Assert.ThrowsExactly<ArgumentNullException>(() => plan.AddOverride(new FaultOverrideScope(), null!));
    }

    [TestMethod]
    [DataRow(FaultKind.Drop, null, FaultStage.Accept)]
    [DataRow(FaultKind.Drop, FaultStage.Command, FaultStage.Command)]
    [DataRow(FaultKind.RefuseLogin, null, FaultStage.Login)]
    [DataRow(FaultKind.Error, null, FaultStage.Command)]
    [DataRow(FaultKind.OutOfSpace, null, FaultStage.StoreWrite)]
    public void StageOf_ARule_IsTheStageItNamesOrItsKindsDefault(FaultKind kind, FaultStage? stage, FaultStage expected)
    {
        Assert.AreEqual(expected, FaultPlan.StageOf(new FaultRule("x", kind) { Stage = stage }));
    }

    [TestMethod]
    public void StageOf_NullRule_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => FaultPlan.StageOf(null!));
    }
}
