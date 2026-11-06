using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-808: fault rules given at start in SurlServerOptions.Faults, and added, removed and cleared on a running server
// through SurlServer.Faults, each read by surl's own --fault reader (ADR-0124, decision 4).
public sealed partial class SurlServerTests
{
    private static readonly TimeSpan ExchangeTimeLimit = TimeSpan.FromSeconds(30);

    [TestMethod]
    public async Task StartAsync_ADropRuleForTheListenUrl_DropsTheFirstConnectionUnservedAndAQueryFindsIt()
    {
        var connection = new FakeConnection(CurlGetHello);
        var seams = new ApiTestSeams(new FakeListenerFactory { ConnectionToAccept = connection });
        var options = FaultOptions();
        options.Faults.Add(SurlFault.DropConnection() with { Listener = Http });

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        var applied = (FaultAppliedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.FaultApplied }, ExchangeTimeLimit, TestContext.CancellationToken);

        Assert.IsEmpty(connection.WrittenToClient);
        Assert.AreEqual("f1", applied.RuleId);
        Assert.AreEqual(FaultKind.Drop, applied.Kind);
        Assert.HasCount(1, server.FindExchanges(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }));
    }

    [TestMethod]
    public async Task StartAsync_AFaultRule_WritesSurlsFaultWarningToTheLog()
    {
        var seams = new ApiTestSeams();
        var options = FaultOptions();
        var log = new StringWriter();
        options.Log = log;
        options.Faults.Add(SurlFault.RefuseLogin());
        options.Faults.Add(SurlFault.OutOfSpace());

        await using (await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken))
        {
        }

        StringAssert.Contains(
            log.ToString(), "surl: warning: --fault: 2 fault rule(s) make this server misbehave on purpose" + Environment.NewLine);
    }

    [TestMethod]
    public async Task Faults_AddWhileServing_DropsTheNextExchangeAndRemoveRestoresServing()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = FaultOptions();
        options.Log = log;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var logBeforeAdd = log.ToString();

        var ruleId = server.Faults.Add(SurlFault.DropConnection());
        var dropped = await ServeOneAsync(seams);
        var applied = (FaultAppliedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.FaultApplied }, ExchangeTimeLimit, TestContext.CancellationToken);
        var removed = server.Faults.Remove(ruleId);
        var served = await ServeOneAsync(seams);

        Assert.AreEqual("f1", ruleId);
        Assert.IsEmpty(dropped.WrittenToClient);
        Assert.AreEqual(ruleId, applied.RuleId);
        Assert.IsTrue(removed);
        StringAssert.StartsWith(Encoding.ASCII.GetString(served.WrittenToClient), "HTTP/1.1 200 OK\r\n");
        Assert.IsFalse(server.Faults.Remove(ruleId));
        Assert.DoesNotContain("--fault", log.ToString());
        StringAssert.StartsWith(log.ToString(), logBeforeAdd);
    }

    [TestMethod]
    public async Task Faults_Clear_RemovesTheStartRulesAndTheAddedRules()
    {
        var seams = new ApiTestSeams();
        var options = FaultOptions();
        options.Faults.Add(SurlFault.DropConnection());
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var addedId = server.Faults.Add(SurlFault.DropConnection() with { Scheme = "http" });

        server.Faults.Clear();
        var served = await ServeOneAsync(seams);

        Assert.AreEqual("f2", addedId);
        StringAssert.StartsWith(Encoding.ASCII.GetString(served.WrittenToClient), "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("f3", server.Faults.Add(SurlFault.RefuseLogin()));
    }

    [TestMethod]
    [DataRow("error", "option --fault: kind error needs key scheme or key listener", DisplayName = "error without a scheme")]
    [DataRow("delay", "option --fault: key ms must be a whole number from 0 to 600000", DisplayName = "a fraction of a millisecond")]
    [DataRow("drop", "option --fault: key account cannot be given at stage accept", DisplayName = "an account at accept")]
    public async Task Faults_AddARuleSurlRefuses_ThrowsArgumentExceptionWithSurlsTextAndAddsNothing(string kind, string refusal)
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(FaultOptions(), seams.Host, TestContext.CancellationToken);
        var fault = kind switch
        {
            "error" => SurlFault.ProtocolError("503 Busy"),
            "delay" => SurlFault.Delay(TimeSpan.FromTicks(15_000)),
            _ => SurlFault.DropConnection() with { Account = "alice" },
        };

        var exception = Assert.ThrowsExactly<ArgumentException>(() => server.Faults.Add(fault));
        var served = await ServeOneAsync(seams);

        StringAssert.StartsWith(exception.Message, refusal);
        Assert.AreEqual("fault", exception.ParamName);
        StringAssert.StartsWith(Encoding.ASCII.GetString(served.WrittenToClient), "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("f1", server.Faults.Add(SurlFault.RefuseLogin()));
    }

    [TestMethod]
    public async Task Faults_NullArguments_ThrowArgumentNullException()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(FaultOptions(), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => server.Faults.Add(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.Faults.Remove(null!));
    }

    private static SurlServerOptions FaultOptions()
    {
        var options = ApiTestSeams.Options(Http);
        options.Files["hello.txt"] = "hello"u8.ToArray();
        options.RecordExchanges = true;
        return options;
    }

    // Brings one fake client in with upstream curl's GET and waits until its exchange has ended.
    private async Task<FakeConnection> ServeOneAsync(ApiTestSeams seams)
    {
        var connection = new FakeConnection(CurlGetHello);
        seams.ListenerFactory.Accept(connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return connection;
    }
}
