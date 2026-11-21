using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1287: a test's own protocol server declares FitsErrorReply so an error fault rule scoped to its scheme is accepted
// and reaches it through ExchangeContext.Faults (ADR-0172).
public sealed partial class SurlServerTests
{
    private static bool FitsFourOrFiveHundred(string reply) => reply.StartsWith('4') || reply.StartsWith('5');

    [TestMethod]
    public async Task StartAsync_AnErrorRuleForACustomSchemeWhoseReplyFits_IsAcceptedAndListedAsGiven()
    {
        var seams = new ApiTestSeams();
        var options = FaultAwareOptions(FitsFourOrFiveHundred, Http, Echo);
        options.Faults.Add(SurlFault.ProtocolError("451 busy; later") with { Listener = Echo, Times = 1 });

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var rules = server.Faults.List();

        Assert.AreEqual("error;listener=echo://127.0.0.1:0;reply=451 busy%3B later", rules.Single().Item);
    }

    [TestMethod]
    public async Task Faults_AddAnErrorRuleForACustomScheme_ReachesTheServerWithItsReplyOncePerTimes()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(
            FaultAwareOptions(FitsFourOrFiveHundred, Http, Echo), seams.Host, TestContext.CancellationToken);

        var ruleId = server.Faults.Add(SurlFault.ProtocolError("451 busy") with { Scheme = "ECHO", Times = 1 });
        var first = await AskOneAsync(seams);
        var second = await AskOneAsync(seams);

        Assert.AreEqual("f1", ruleId);
        Assert.AreEqual("ERROR 451 busy", first);
        Assert.AreEqual("OK", second);
    }

    [TestMethod]
    public async Task StartAsync_AnErrorRuleForACustomScheme_ReachesTheServerWithItsReplyAtStart()
    {
        var seams = new ApiTestSeams();
        var options = FaultAwareOptions(FitsFourOrFiveHundred, Http, Echo);
        options.Faults.Add(SurlFault.ProtocolError("503 down") with { Scheme = "echo" });

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var answered = await AskOneAsync(seams);

        Assert.AreEqual("ERROR 503 down", answered);
    }

    [TestMethod]
    public async Task ErrorRuleForACustomScheme_WithoutFitsErrorReply_IsStillRefusedAtStartAndWhenAdded()
    {
        var seams = new ApiTestSeams();
        var refusedAtStart = FaultAwareOptions(null, Http, Echo);
        refusedAtStart.Faults.Add(SurlFault.ProtocolError("451 busy") with { Scheme = "echo" });

        var startException = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => SurlServer.StartAsync(refusedAtStart, seams.Host, TestContext.CancellationToken));
        await using var server = await SurlServer.StartAsync(
            FaultAwareOptions(null, Http, Echo), seams.Host, TestContext.CancellationToken);
        var addException = Assert.ThrowsExactly<ArgumentException>(
            () => server.Faults.Add(SurlFault.ProtocolError("451 busy") with { Listener = Echo }));

        StringAssert.Contains(startException.Message, "unless the server declares FitsErrorReply");
        StringAssert.Contains(addException.Message, "unless the server declares FitsErrorReply");
        Assert.IsEmpty(server.Faults.List());
    }

    [TestMethod]
    public async Task Faults_AddAnErrorRuleWhoseReplyDoesNotFit_ThrowsArgumentExceptionNamingTheRule()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(
            FaultAwareOptions(FitsFourOrFiveHundred, Http, Echo), seams.Host, TestContext.CancellationToken);

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => server.Faults.Add(SurlFault.ProtocolError("200 fine") with { Scheme = "echo" }));

        StringAssert.Contains(exception.Message, "reply=200 fine");
        Assert.AreEqual("fault", exception.ParamName);
        Assert.IsEmpty(server.Faults.List());
    }

    [TestMethod]
    public async Task Faults_AddAnErrorRuleWhenFitsErrorReplyThrows_ThrowsArgumentExceptionNamingTheRule()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(
            FaultAwareOptions(_ => throw new InvalidOperationException("predicate broke"), Http, Echo),
            seams.Host,
            TestContext.CancellationToken);

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => server.Faults.Add(SurlFault.ProtocolError("451 busy") with { Scheme = "echo" }));

        StringAssert.Contains(exception.Message, "predicate broke");
        StringAssert.Contains(exception.Message, "reply=451 busy");
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
    }

    [TestMethod]
    public async Task Faults_AddAnErrorRuleWithoutAReplyForACustomScheme_IsLeftToSurlWhichRefusesIt()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(
            FaultAwareOptions(FitsFourOrFiveHundred, Http, Echo), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentException>(
            () => server.Faults.Add(SurlFault.ProtocolError("451") with { Reply = null, Scheme = "echo" }));
    }

    [TestMethod]
    public async Task AddOverride_AnErrorAnswerForACustomSchemeWithAFittingPredicate_ReachesTheServer()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(
            FaultAwareOptions(FitsFourOrFiveHundred, Http, Echo), seams.Host, TestContext.CancellationToken);

        server.Faults.AddOverride(
            new SurlFaultScope { Scheme = "echo" },
            (_, _) => ValueTask.FromResult<SurlFault?>(SurlFault.ProtocolError("451 busy")));
        var answered = await AskOneAsync(seams);

        Assert.AreEqual("ERROR 451 busy", answered);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "no FitsErrorReply")]
    [DataRow(true, DisplayName = "a FitsErrorReply the reply does not fit")]
    public async Task AddOverride_AnErrorAnswerForACustomSchemeNotAccepted_ServesNormallyAndPublishesHookFailed(bool declaresPredicate)
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(
            FaultAwareOptions(declaresPredicate ? reply => reply.StartsWith('5') : null, Http, Echo),
            seams.Host,
            TestContext.CancellationToken);

        server.Faults.AddOverride(
            new SurlFaultScope { Scheme = "echo" },
            (_, _) => ValueTask.FromResult<SurlFault?>(SurlFault.ProtocolError("451 busy")));
        var answered = await AskOneAsync(seams);
        var failed = (HookFailedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.HookFailed }, ExchangeTimeLimit, TestContext.CancellationToken);

        Assert.AreEqual("OK", answered);
        Assert.AreEqual(HookFailure.Threw, failed.Failure);
    }

    private static SurlServerOptions FaultAwareOptions(Func<string, bool>? fitsErrorReply, params string[] listenUrls)
    {
        var options = ApiTestSeams.Options(listenUrls);
        options.RecordExchanges = true;
        options.ProtocolServers.Add(new SurlProtocolServer("echo", _ => new FaultAwareServer())
        {
            FitsErrorReply = fitsErrorReply,
        });
        return options;
    }

    // Brings one fake client in on the echo listener and returns what the fault-aware server wrote.
    private async Task<string> AskOneAsync(ApiTestSeams seams)
    {
        var connection = new FakeConnection([]);
        seams.ListenerFactory.Accept("echo", connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return Encoding.ASCII.GetString(connection.WrittenToClient);
    }

    // A test's own server that asks the fault plan at its command stage and answers the chosen error in its own words.
    private sealed class FaultAwareServer : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["echo"];

        public async Task ServeAsync(IConnection connection, ExchangeContext context)
        {
            var point = new FaultPoint(FaultStage.Command) { ExchangeId = context.ExchangeId, Scheme = "echo" };
            var rule = await context.Faults.FindAsync(point, context.CancellationToken);
            var answer = rule is { Kind: FaultKind.Error } ? $"ERROR {rule.Reply}" : "OK";
            await connection.WriteAsync(Encoding.ASCII.GetBytes(answer), context.CancellationToken);
        }
    }
}
