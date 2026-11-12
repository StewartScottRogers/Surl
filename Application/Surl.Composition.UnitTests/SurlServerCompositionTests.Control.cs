using System.Text;
using Surl.Cli;
using Surl.Content;
using Surl.Control;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The loopback control endpoint's token and its serving beside the protocol listeners (ADR-0124 decision 7), over
/// <see cref="FakeListenerFactory"/> and <see cref="StartTestSeams"/>, with no network: the control listener and the
/// HTTP listener are both <c>http</c>, so each is handed its own connections by its listen URL.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    private const string GoodToken = "0123456789abcdef0123456789abcdef";
    private const string ControlUrl = "http://127.0.0.1:0";
    private const string ServedHttpUrl = "http://127.0.0.1:8080";
    private static readonly string BoundControlUrl = $"http://127.0.0.1:{FakeListenerFactory.BoundPort}/";

    private static readonly string ControlWarning =
        $"surl: warning: --control: a test control endpoint on {BoundControlUrl} can change this server's files, mail, messages, accounts and faults";

    private static SurlCommandLine ControlCommandLine(params string[] extra) =>
        StartTestSeams.Parse([.. extra, "--control", ControlUrl, "--control-token-file", "token.txt", "--allow-anonymous", ServedHttpUrl]);

    [TestMethod]
    public async Task ServeAsync_Control_ListsTheHttpExchangeServedOnTheSameInstanceAndNotItsOwn()
    {
        var factory = new FakeListenerFactory();
        await using var serving = await ComposeWithControlAsync(factory);
        await SeedTextAsync(serving, "/seeded.txt", "served over http");
        await using var running = await ServeControlledAsync(serving, TextWriter.Null);

        var served = await SendAsync(factory, ServedHttpUrl, HttpGet("/seeded.txt"));
        var waited = await SendAsync(factory, ControlUrl, ControlRequest("GET", "/v1/events/wait?kind=exchange_ended"));
        var listed = await SendAsync(factory, ControlUrl, ControlRequest("GET", "/v1/exchanges"));

        StringAssert.Contains(served, "served over http");
        StringAssert.StartsWith(waited, "HTTP/1.1 200");
        StringAssert.StartsWith(listed, "HTTP/1.1 200");
        StringAssert.Contains(listed, "\"listen_url\":\"http://127.0.0.1:" + FakeListenerFactory.BoundPort + "/\"");
        StringAssert.Contains(listed, "GET /seeded.txt");
        Assert.DoesNotContain("/v1/", listed);
        Assert.DoesNotContain(GoodToken, listed);
    }

    [TestMethod]
    public async Task ServeAsync_ControlPostFaultsDrop_DropsTheNextHttpConnection()
    {
        var factory = new FakeListenerFactory();
        await using var serving = await ComposeWithControlAsync(factory);
        await using var running = await ServeControlledAsync(serving, TextWriter.Null);

        var added = await SendAsync(factory, ControlUrl, ControlRequest("POST", "/v1/faults", "drop;scheme=http;times=1"));
        var dropped = new FakeConnection(Encoding.ASCII.GetBytes(HttpGet("/")));
        await factory.ConnectionsFor(ServedHttpUrl).WriteAsync(dropped, TestContext.CancellationToken);
        await dropped.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        var served = await SendAsync(factory, ServedHttpUrl, HttpGet("/"));

        StringAssert.StartsWith(added, "HTTP/1.1 201");
        StringAssert.Contains(added, "{\"fault_id\":\"f1\"}");
        Assert.IsTrue(dropped.Aborted);
        Assert.IsEmpty(dropped.WrittenBytes);
        StringAssert.StartsWith(served, "HTTP/1.1 ");
    }

    [TestMethod]
    public async Task ServeAsync_ControlPostReset_EmptiesASeededFile()
    {
        var factory = new FakeListenerFactory();
        await using var serving = await ComposeWithControlAsync(factory);
        await SeedTextAsync(serving, "/seeded.txt", "gone after the reset");
        await using var running = await ServeControlledAsync(serving, TextWriter.Null);

        var reset = await SendAsync(factory, ControlUrl, ControlRequest("POST", "/v1/reset"));
        var served = await SendAsync(factory, ServedHttpUrl, HttpGet("/seeded.txt"));

        StringAssert.StartsWith(reset, "HTTP/1.1 204");
        StringAssert.StartsWith(served, "HTTP/1.1 404");
        Assert.AreEqual(ContentEntryKind.None, serving.Stores.Content.GetEntryKind(serving.Stores.Content.MapRequestPath("/seeded.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_AControlRequestWithoutTheToken_IsRefused401()
    {
        var factory = new FakeListenerFactory();
        await using var serving = await ComposeWithControlAsync(factory);
        await using var running = await ServeControlledAsync(serving, TextWriter.Null);

        var refused = await SendAsync(factory, ControlUrl, ControlRequest("POST", "/v1/reset", token: null));

        StringAssert.StartsWith(refused, "HTTP/1.1 401");
        StringAssert.Contains(refused, "WWW-Authenticate: Bearer");
    }

    [TestMethod]
    public async Task ServeAsync_Control_WritesTheWarningAfterTheFaultWarningAndTheStatusLineAfterTheListeningLines()
    {
        using var log = new StringWriter();
        await using var serving = await ComposeWithControlAsync(new FakeListenerFactory(), "--fault", "refuse-login");

        await ServeUntilBoundAsync(serving, log);

        var listeningLine = $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/";
        Assert.AreEqual(
            "surl: warning: --allow-anonymous: every request and login is accepted without checking credentials" + Environment.NewLine
            + "surl: warning: --fault: 1 fault rule(s) make this server misbehave on purpose" + Environment.NewLine
            + ControlWarning + Environment.NewLine
            + listeningLine + Environment.NewLine
            + "Control endpoint listening on " + BoundControlUrl + Environment.NewLine,
            log.ToString());
        Assert.AreEqual(BoundControlUrl, $"http://{serving.ControlListenUrl!.Host}:{serving.ControlListenUrl.BoundPort}/");
    }

    [TestMethod]
    public async Task ServeAsync_ControlWithSilent_WritesNeitherTheWarningNorTheStatusLine()
    {
        using var log = new StringWriter();
        await using var serving = await ComposeWithControlAsync(new FakeListenerFactory(), "--silent");

        await ServeUntilBoundAsync(serving, log);

        Assert.AreEqual(string.Empty, log.ToString());
        Assert.IsNotNull(serving.ControlListenUrl);
    }

    [TestMethod]
    public async Task ServeAsync_WithoutControl_HasNoControlListener()
    {
        using var log = new StringWriter();
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--allow-anonymous", ServedHttpUrl), new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        await ServeUntilBoundAsync(serving, log);

        Assert.IsNull(serving.ControlListenUrl);
        Assert.DoesNotContain("ontrol", log.ToString());
    }

    [TestMethod]
    public async Task ServeAsync_AControlListenerThatCannotBind_EndsWith45BeforeAnyOtherLine()
    {
        using var log = new StringWriter();
        var factory = new FakeListenerFactory
        {
            BindFailure = new ListenerBindException(new ListenUrl("http", "127.0.0.1", 0), null, ListenerBindFailure.AddressInUse, null),
        };
        await using var serving = await ComposeWithControlAsync(factory);

        var outcome = await serving.ServeAsync(StartTestSeams.Outputs(log), TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.BindFailed, outcome.ExitCode);
        CollectionAssert.AreEqual(new[] { "(45) Could not bind http://127.0.0.1:0/: Address already in use" }, outcome.Failure!.Lines.ToArray());
        Assert.AreEqual(string.Empty, log.ToString());
        Assert.IsNull(serving.ControlListenUrl);
    }

    [TestMethod]
    public async Task ServeAsync_ControlStoppedBeforeItBinds_EndsOkWithNoLine()
    {
        using var log = new StringWriter();
        await using var serving = await ComposeWithControlAsync(new FakeListenerFactory());

        var outcome = await serving.ServeAsync(StartTestSeams.Outputs(log), new CancellationToken(canceled: true));

        Assert.AreEqual(new SurlServingOutcome(SurlExitCode.Ok, null), outcome);
        Assert.AreEqual(string.Empty, log.ToString());
    }

    private Task<ComposedServing> ComposeWithControlAsync(FakeListenerFactory factory, params string[] options) =>
        ComposeWithControlTokenFileAsync(factory, GoodToken + "\n", options);

    private async Task<ComposedServing> ComposeWithControlTokenFileAsync(FakeListenerFactory factory, string tokenFileText, params string[] options)
    {
        var start = await SurlServerComposition.ComposeAsync(
            ControlCommandLine(options),
            new StartTestSeams(factory).Seams with { ReadStartFile = _ => Encoding.ASCII.GetBytes(tokenFileText) },
            TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
    }

    private async Task SeedTextAsync(ComposedServing serving, string path, string text)
    {
        using var contents = new MemoryStream(Encoding.ASCII.GetBytes(text));
        await serving.Stores.SeedFileAsync(path, contents, TestContext.CancellationToken);
    }

    // Serves until every listener has bound; disposing the result stops serving and checks it ended Ok.
    private async Task<RunningServing> ServeControlledAsync(ComposedServing serving, TextWriter log)
    {
        var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        return new RunningServing(stop, serve);
    }

    private async Task<string> SendAsync(FakeListenerFactory factory, string listenUrl, string request)
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(request));
        await factory.ConnectionsFor(listenUrl).WriteAsync(connection, TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        return Encoding.ASCII.GetString(connection.WrittenBytes);
    }

    private static string HttpGet(string path) => $"GET {path} HTTP/1.1\r\nHost: x\r\nConnection: close\r\n\r\n";

    private static string ControlRequest(string method, string target, string? body = null, string? token = GoodToken)
    {
        var request = new StringBuilder($"{method} {target} HTTP/1.1\r\nHost: x\r\nConnection: close\r\n");
        if (token is not null)
        {
            request.Append($"Authorization: Bearer {token}\r\n");
        }

        if (body is not null)
        {
            request.Append($"Content-Type: text/plain\r\nContent-Length: {body.Length}\r\n");
        }

        return request.Append("\r\n").Append(body).ToString();
    }

    private sealed class RunningServing(CancellationTokenSource stop, Task<SurlServingOutcome> serve) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            Assert.AreEqual(new SurlServingOutcome(SurlExitCode.Ok, null), await serve);
            stop.Dispose();
        }
    }

    private static IReadOnlyList<KeyValuePair<string, string>> Bearer(string token) =>
        [new KeyValuePair<string, string>("Authorization", "Bearer " + token)];

    [TestMethod]
    public async Task ComposeAsync_AnUnreadableControlTokenFile_IsRefused37BeforeTheLockIsTaken()
    {
        var startSeams = new StartTestSeams();
        var seams = startSeams.Seams with { ReadStartFile = path => throw new FileNotFoundException(path) };

        var start = await SurlServerComposition.ComposeAsync(
            ControlCommandLine("--directory", "served"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(37) Could not read control token file token.txt" }, start.Failure.Lines.ToArray());
        Assert.IsFalse(startSeams.IsHeld("served"));
    }

    [TestMethod]
    [DataRow("short")]
    [DataRow("tooLong")]
    [DataRow("nonAscii")]
    public async Task ComposeAsync_AControlTokenOutsideItsBounds_IsRefused2WithoutTheToken(string kind)
    {
        var token = kind switch
        {
            "short" => new string('a', ControlToken.MinLength - 1),
            "tooLong" => new string('a', ControlToken.MaxLength + 1),
            _ => new string('a', ControlToken.MinLength - 1) + "é",
        };
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => System.Text.Encoding.Latin1.GetBytes(token + "\n") };

        var start = await SurlServerComposition.ComposeAsync(ControlCommandLine(), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(2) Control token file token.txt must hold 32 to 1024 visible ASCII bytes on its first line" },
            start.Failure.Lines.ToArray());
    }

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    [DataRow("")]
    [DataRow("\nsecond line")]
    public async Task ComposeAsync_AControlTokenFile_AcceptsItsFirstLineWithoutTheLineEndAsTheToken(string lineEnd)
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => System.Text.Encoding.ASCII.GetBytes(GoodToken + lineEnd) };

        var start = await SurlServerComposition.ComposeAsync(ControlCommandLine(), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        var (token, _, _) = ControlComposition.ReadToken(ControlCommandLine(), _ => System.Text.Encoding.ASCII.GetBytes(GoodToken + lineEnd));
        Assert.IsTrue(token!.IsPresentedIn(Bearer(GoodToken)));
        Assert.IsFalse(token.IsPresentedIn(Bearer(GoodToken + "\r")));
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutControl_ReadsNoControlTokenFile()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = path => throw new AssertFailedException(path) };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("http://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
    }
}
