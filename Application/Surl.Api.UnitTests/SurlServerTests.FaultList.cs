using System.Text;
using Surl.Cli;
using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-963: a running server's fault plan read back through SurlServer.Faults.List - each rule's ID, its --fault item
// without times, and what is left of its count (ADR-0124, decision 8's list_faults).
public sealed partial class SurlServerTests
{
    private static readonly SurlFault[] ListedStartRules =
    [
        SurlFault.DropConnection() with { Listener = Http, AfterBytes = 12 },
        SurlFault.ProtocolError("503 Busy; try later") with { Scheme = "http", Path = "/a b/c%d", Times = 3 },
    ];

    private static readonly SurlFault[] ListedAddedRules =
    [
        SurlFault.Delay(TimeSpan.FromMilliseconds(250)) with { Stage = FaultStage.Command, Command = "GET" },
        SurlFault.RefuseLogin() with { Account = "al=ice", Times = 1 },
        SurlFault.OutOfSpace(),
    ];

    [TestMethod]
    public async Task List_StartRulesAndAddedRules_AreListedInPlanOrderWithTheirIdsItemsAndCounts()
    {
        await using var server = await StartWithListedRulesAsync();

        var listed = server.Faults.List();

        CollectionAssert.AreEqual(
            new[]
            {
                new SurlFaultRule("f1", "drop;listener=http%3A%2F%2F127.0.0.1%3A0;after-bytes=12", null),
                new SurlFaultRule("f2", "error;scheme=http;path=%2Fa%20b%2Fc%25d;reply=503%20Busy%3B%20try%20later", 3),
                new SurlFaultRule("f3", "delay;command=GET;stage=command;ms=250", null),
                new SurlFaultRule("f4", "refuse-login;account=al%3Dice", 1),
                new SurlFaultRule("f5", "out-of-space", null),
            },
            listed.ToArray());
    }

    [TestMethod]
    public async Task List_EachItemReadBackByCommandLineParser_GivesTheSameRuleWithoutItsCount()
    {
        await using var server = await StartWithListedRulesAsync();

        var listed = server.Faults.List();
        var given = ListedStartRules.Concat(ListedAddedRules).ToArray();

        Assert.HasCount(given.Length, listed);
        for (var index = 0; index < given.Length; index++)
        {
            var parsed = CommandLineParser.Parse([Http, "--fault", listed[index].Item]);
            Assert.AreEqual(CommandLineOutcome.Serve, parsed.Outcome, listed[index].Item);
            Assert.AreEqual(
                SurlServerFaults.Read(given[index] with { Times = null }, "fault"),
                FaultPlanComposition.ToRule(parsed.CommandLine!.Faults[0]),
                listed[index].Item);
        }
    }

    [TestMethod]
    public async Task List_ATimesRuleApplied_RemainingDropsByOneEachTimeAndARuleWithoutTimesStaysNull()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(FaultOptions(), seams.Host, TestContext.CancellationToken);
        server.Faults.Add(SurlFault.DropConnection() with { Times = 2 });
        server.Faults.Add(SurlFault.OutOfSpace());

        var before = server.Faults.List();
        await ServeOneAsync(seams);
        var afterOne = server.Faults.List();
        await ServeOneAsync(seams);
        var afterTwo = server.Faults.List();
        var served = await ServeOneAsync(seams);
        var afterSpent = server.Faults.List();

        CollectionAssert.AreEqual(new int?[] { 2, null }, before.Select(rule => rule.Remaining).ToArray());
        CollectionAssert.AreEqual(new int?[] { 1, null }, afterOne.Select(rule => rule.Remaining).ToArray());
        CollectionAssert.AreEqual(new int?[] { 0, null }, afterTwo.Select(rule => rule.Remaining).ToArray());
        CollectionAssert.AreEqual(new int?[] { 0, null }, afterSpent.Select(rule => rule.Remaining).ToArray());
        Assert.AreEqual("drop", afterSpent[0].Item);
        StringAssert.StartsWith(Encoding.ASCII.GetString(served.WrittenToClient), "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task List_ARemovedRuleAndAResponseOverride_AreNotListed()
    {
        var seams = new ApiTestSeams();
        var options = FaultOptions();
        options.Faults.Add(SurlFault.RefuseLogin());
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        server.Faults.Add(SurlFault.OutOfSpace());
        server.Faults.AddOverride(new SurlFaultScope(), (_, _) => ValueTask.FromResult<SurlFault?>(null));

        var removed = server.Faults.Remove("f1");
        var listed = server.Faults.List();
        server.Faults.Clear();

        Assert.IsTrue(removed);
        CollectionAssert.AreEqual(new[] { new SurlFaultRule("f2", "out-of-space", null) }, listed.ToArray());
        Assert.IsEmpty(server.Faults.List());
    }

    // Starts a server with ListedStartRules, then adds ListedAddedRules while it serves.
    private async Task<SurlServer> StartWithListedRulesAsync()
    {
        var options = FaultOptions();
        foreach (var fault in ListedStartRules)
        {
            options.Faults.Add(fault);
        }

        var server = await SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken);
        foreach (var fault in ListedAddedRules)
        {
            server.Faults.Add(fault);
        }

        return server;
    }
}
