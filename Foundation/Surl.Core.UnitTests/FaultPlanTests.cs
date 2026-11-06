using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class FaultPlanTests
{
    private static readonly FaultPoint SmtpAccept = new(FaultStage.Accept)
    {
        ExchangeId = 1,
        ListenUrl = "smtp://127.0.0.1:2525/",
        Scheme = "smtp",
    };

    [TestMethod]
    public async Task FindAsync_EmptyPlan_FindsNothing()
    {
        Assert.IsNull(await new FaultPlan().FindAsync(SmtpAccept, CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_TwoMatchingRules_FindsTheFirstAdded()
    {
        var plan = new FaultPlan();
        var first = plan.Add(new FaultRule("x", FaultKind.Drop) { Scheme = "smtp" });
        plan.Add(new FaultRule("y", FaultKind.Delay) { DelayMilliseconds = 5 });

        Assert.AreSame(first, await plan.FindAsync(SmtpAccept, CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_FirstRuleDoesNotMatch_FindsTheNextThatDoes()
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Scheme = "imap" });
        var second = plan.Add(new FaultRule("y", FaultKind.Drop));

        Assert.AreSame(second, await plan.FindAsync(SmtpAccept, CancellationToken.None));
    }

    [TestMethod]
    public void Add_GivesIdsInOrder_NeverReusedAfterRemoveOrClear()
    {
        var plan = new FaultPlan();

        var ids = new List<string> { plan.Add(new FaultRule("mine", FaultKind.Drop)).Id, plan.Add(new FaultRule("mine", FaultKind.Drop)).Id };
        plan.Remove("f2");
        ids.Add(plan.Add(new FaultRule("mine", FaultKind.Drop)).Id);
        plan.Clear();
        ids.Add(plan.Add(new FaultRule("mine", FaultKind.Drop)).Id);

        CollectionAssert.AreEqual(new[] { "f1", "f2", "f3", "f4" }, ids);
    }

    [TestMethod]
    [DataRow("smtp://127.0.0.1:2525/", true)]
    [DataRow("smtp://127.0.0.1:2525", true)]
    [DataRow("SMTP://127.0.0.1:2525/", true)]
    [DataRow("smtp://127.0.0.1:0", true)]
    [DataRow("smtp://127.0.0.1:2526/", false)]
    [DataRow("smtp://127.0.0.2:2525/", false)]
    [DataRow("smtps://127.0.0.1:2525/", false)]
    [DataRow("not a url", false)]
    public async Task FindAsync_ListenerScope_MatchesTheListenUrlAsGivenOrAsBound(string listener, bool matches)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Listener = listener });

        Assert.AreEqual(matches, await plan.FindAsync(SmtpAccept, CancellationToken.None) is not null);
    }

    [TestMethod]
    public async Task FindAsync_ListenerScope_PointWithNoListenUrl_DoesNotMatch()
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Listener = "smtp://127.0.0.1:2525/" });

        Assert.IsNull(await plan.FindAsync(SmtpAccept with { ListenUrl = null }, CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_ListenerScope_PointListenUrlNotAUrl_MatchesOnlyTheSameText()
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Listener = "smtp://127.0.0.1:2525/" });

        Assert.IsNull(await plan.FindAsync(SmtpAccept with { ListenUrl = "elsewhere" }, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("smtp", true)]
    [DataRow("SMTP", true)]
    [DataRow("imap", false)]
    public async Task FindAsync_SchemeScope_MatchesTheSchemeIgnoringCase(string scheme, bool matches)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Scheme = scheme });

        Assert.AreEqual(matches, await plan.FindAsync(SmtpAccept, CancellationToken.None) is not null);
    }

    [TestMethod]
    [DataRow("alice", true)]
    [DataRow("Alice", false)]
    [DataRow("bob", false)]
    [DataRow(null, false)]
    public async Task FindAsync_AccountScope_MatchesTheAccountOrdinally(string? pointAccount, bool matches)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.RefuseLogin) { Account = "alice" });

        var point = new FaultPoint(FaultStage.Login) { Account = pointAccount };

        Assert.AreEqual(matches, await plan.FindAsync(point, CancellationToken.None) is not null);
    }

    [TestMethod]
    [DataRow("/uploads", "/uploads", true)]
    [DataRow("/uploads", "/uploads/a.txt", true)]
    [DataRow("/uploads/", "/uploads/a.txt", true)]
    [DataRow("/uploads", "/uploads2/a.txt", false)]
    [DataRow("/uploads", "/Uploads", false)]
    [DataRow("/uploads", null, false)]
    public async Task FindAsync_PathScope_MatchesEqualOrAsASegmentPrefix(string rulePath, string? pointPath, bool matches)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.OutOfSpace) { Path = rulePath });

        var point = new FaultPoint(FaultStage.StoreWrite) { Path = pointPath };

        Assert.AreEqual(matches, await plan.FindAsync(point, CancellationToken.None) is not null);
    }

    [TestMethod]
    [DataRow("RCPT", true)]
    [DataRow("rcpt", true)]
    [DataRow("MAIL", false)]
    public async Task FindAsync_CommandScope_MatchesTheVerbIgnoringCase(string pointCommand, bool matches)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Error) { Scheme = "smtp", Command = "RCPT", Reply = "451 later" });

        var point = new FaultPoint(FaultStage.Command) { Scheme = "smtp", Command = pointCommand };

        Assert.AreEqual(matches, await plan.FindAsync(point, CancellationToken.None) is not null);
    }

    [TestMethod]
    [DataRow(FaultKind.Delay, null, FaultStage.Accept)]
    [DataRow(FaultKind.Delay, FaultStage.Command, FaultStage.Command)]
    [DataRow(FaultKind.Drop, null, FaultStage.Accept)]
    [DataRow(FaultKind.Drop, FaultStage.Command, FaultStage.Command)]
    [DataRow(FaultKind.RefuseLogin, null, FaultStage.Login)]
    [DataRow(FaultKind.Error, null, FaultStage.Command)]
    [DataRow(FaultKind.OutOfSpace, FaultStage.StoreWrite, FaultStage.StoreWrite)]
    public async Task FindAsync_Rule_MatchesOnlyAtTheStageItActsAt(FaultKind kind, FaultStage? ruleStage, FaultStage actsAt)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", kind) { Stage = ruleStage, DelayMilliseconds = kind == FaultKind.Delay ? 1 : null });

        foreach (var stage in Enum.GetValues<FaultStage>())
        {
            Assert.AreEqual(stage == actsAt, await plan.FindAsync(new FaultPoint(stage), CancellationToken.None) is not null, stage.ToString());
        }
    }

    [TestMethod]
    [DataRow(FaultKind.Delay, FaultStage.Login)]
    [DataRow(FaultKind.Drop, FaultStage.StoreWrite)]
    [DataRow(FaultKind.RefuseLogin, FaultStage.Command)]
    [DataRow(FaultKind.Error, FaultStage.Accept)]
    [DataRow(FaultKind.OutOfSpace, FaultStage.Command)]
    public void Add_StageItsKindDoesNotActAt_Throws(FaultKind kind, FaultStage stage)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new FaultPlan().Add(new FaultRule("x", kind) { Stage = stage, DelayMilliseconds = 1 }));

        Assert.AreEqual("rule", exception.ParamName);
    }

    [TestMethod]
    public void Add_DelayWithoutMilliseconds_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FaultPlan().Add(new FaultRule("x", FaultKind.Delay)));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(600_001)]
    public void Add_DelayOutOfRange_Throws(int milliseconds)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new FaultPlan().Add(new FaultRule("x", FaultKind.Delay) { DelayMilliseconds = milliseconds }));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(600_000)]
    public void Add_DelayAtTheEndsOfItsRange_IsAdded(int milliseconds)
    {
        var plan = new FaultPlan();

        plan.Add(new FaultRule("x", FaultKind.Delay) { DelayMilliseconds = milliseconds });

        Assert.AreEqual(milliseconds, plan.Rules.Single().DelayMilliseconds);
    }

    [TestMethod]
    public void Add_TimesBelowOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new FaultPlan().Add(new FaultRule("x", FaultKind.Drop) { Times = 0 }));
    }

    [TestMethod]
    public void Add_NegativeAfterBytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new FaultPlan().Add(new FaultRule("x", FaultKind.Drop) { AfterBytes = -1 }));
    }

    [TestMethod]
    public void Add_NullRule_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FaultPlan().Add(null!));
    }

    [TestMethod]
    public void Add_PastMaxRules_Throws()
    {
        var plan = new FaultPlan();
        for (var count = 0; count < FaultPlan.MaxRules; count++)
        {
            plan.Add(new FaultRule("x", FaultKind.Drop));
        }

        Assert.ThrowsExactly<InvalidOperationException>(() => plan.Add(new FaultRule("x", FaultKind.Drop)));
        Assert.AreEqual(256, plan.Rules.Count);
    }

    [TestMethod]
    public async Task FindAsync_TimesTwo_FindsTheRuleTwiceThenTheNext()
    {
        var plan = new FaultPlan();
        var limited = plan.Add(new FaultRule("x", FaultKind.Drop) { Times = 2 });
        var always = plan.Add(new FaultRule("y", FaultKind.Delay) { DelayMilliseconds = 1 });

        var found = new List<FaultRule?>();
        for (var ask = 0; ask < 4; ask++)
        {
            found.Add(await plan.FindAsync(SmtpAccept, CancellationToken.None));
        }

        CollectionAssert.AreEqual(new[] { limited, limited, always, always }, found);
        CollectionAssert.AreEqual(new int?[] { 0, null }, plan.Rules.Select(rule => rule.Times).ToArray());
    }

    [TestMethod]
    public async Task FindAsync_TimesOne_FiresExactlyOnceUnder32ConcurrentFinds()
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Times = 1 });
        using var start = new ManualResetEventSlim();

        var finds = Enumerable.Range(0, 32).Select(index => Task.Run(async () =>
        {
            start.Wait();
            return await plan.FindAsync(SmtpAccept with { ExchangeId = index }, CancellationToken.None);
        })).ToArray();
        start.Set();
        var found = await Task.WhenAll(finds).WaitAsync(Patience.Timeout);

        Assert.AreEqual(1, found.Count(rule => rule is not null));
    }

    [TestMethod]
    public async Task Remove_RuleInThePlan_StopsItMatching()
    {
        var plan = new FaultPlan();
        var rule = plan.Add(new FaultRule("x", FaultKind.Drop));

        Assert.IsTrue(plan.Remove(rule.Id));
        Assert.IsFalse(plan.Remove(rule.Id));
        Assert.IsNull(await plan.FindAsync(SmtpAccept, CancellationToken.None));
    }

    [TestMethod]
    public void Remove_NullId_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FaultPlan().Remove(null!));
    }

    [TestMethod]
    public async Task Clear_RemovesEveryRule()
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop));
        plan.Add(new FaultRule("y", FaultKind.Drop));

        plan.Clear();

        Assert.AreEqual(0, plan.Rules.Count);
        Assert.IsNull(await plan.FindAsync(SmtpAccept, CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_RulesAddedRemovedAndClearedWhileFinding_NeverThrows()
    {
        var plan = new FaultPlan();
        using var stop = new CancellationTokenSource();
        var finders = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            var found = 0;
            while (!stop.IsCancellationRequested)
            {
                found += await plan.FindAsync(SmtpAccept, CancellationToken.None) is null ? 0 : 1;
            }

            return found;
        })).ToArray();

        for (var round = 0; round < 200; round++)
        {
            var rule = plan.Add(new FaultRule("x", FaultKind.Drop));
            plan.Add(new FaultRule("y", FaultKind.Drop) { Times = 1 });
            plan.Remove(rule.Id);
            plan.Clear();
        }

        await stop.CancelAsync();
        await Task.WhenAll(finders).WaitAsync(Patience.Timeout);
        Assert.AreEqual(0, plan.Rules.Count);
    }

    [TestMethod]
    public async Task FindAsync_NullPoint_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new FaultPlan().FindAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public void FindAsync_CompletesSynchronously()
    {
        Assert.IsTrue(new FaultPlan().FindAsync(SmtpAccept, CancellationToken.None).IsCompletedSuccessfully);
    }
}
