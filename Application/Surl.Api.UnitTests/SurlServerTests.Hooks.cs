using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-811: hooks on a running server - event callbacks and bounded response overrides (ADR-0124, decision 5).
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task OnEvent_ACallback_RunsForEachMatchingEventAndStopsOnceItsRegistrationIsDisposed()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(HookOptions(), seams.Host, TestContext.CancellationToken);
        var started = new List<long>();
        var firstSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = server.OnEvent(new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted }, (surlEvent, _) =>
        {
            started.Add(((ExchangeStartedEvent)surlEvent).ExchangeId);
            firstSeen.TrySetResult();
            return ValueTask.CompletedTask;
        });
        var secondEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await ServeAsync(seams, "http", HttpGet("/hello.txt", null));
        await firstSeen.Task.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        registration.Dispose();
        using var watcher = server.OnEvent(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }, (_, _) =>
        {
            secondEnded.TrySetResult();
            return ValueTask.CompletedTask;
        });
        await ServeAsync(seams, "http", HttpGet("/hello.txt", null));
        await secondEnded.Task.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);

        Assert.HasCount(1, started);
    }

    [TestMethod]
    public async Task OnEvent_ACallbackThatThrows_PublishesHookFailedAndServingGoesOn()
    {
        var seams = new ApiTestSeams();
        var options = HookOptions();
        options.RecordExchanges = true;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        using var registration = server.OnEvent(
            new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted }, (_, _) => throw new InvalidOperationException("callback broke"));

        var first = await ServeAsync(seams, "http", HttpGet("/hello.txt", null));
        var failed = (HookFailedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.HookFailed }, ExchangeTimeLimit, TestContext.CancellationToken);
        var second = await ServeAsync(seams, "http", HttpGet("/hello.txt", null));

        Assert.AreEqual(new HookFailedEvent("h1", HookFailure.Threw, "callback broke"), failed with { Sequence = 0, At = default });
        StringAssert.StartsWith(first, "HTTP/1.1 200 OK\r\n");
        StringAssert.StartsWith(second, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task AddOverride_AProtocolErrorForPathA_MakesGetAFailAndGetBSucceed()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(HookOptions(), seams.Host, TestContext.CancellationToken);
        var overrideId = server.Faults.AddOverride(
            new SurlFaultScope { Stage = FaultStage.Command },
            (point, _) => ValueTask.FromResult(point.Path == "/a" ? SurlFault.ProtocolError("503 Busy") : null));

        var getA = await ServeAsync(seams, "http", HttpGet("/a", null));
        var getB = await ServeAsync(seams, "http", HttpGet("/b", null));

        Assert.AreEqual("f1", overrideId);
        StringAssert.StartsWith(getA, "HTTP/1.1 503 Busy\r\n");
        StringAssert.StartsWith(getB, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(getB, "file b");
    }

    [TestMethod]
    public async Task AddOverride_PastItsTimeLimit_ServesTheRequestNormallyAndPublishesTimedOut()
    {
        var seams = new ApiTestSeams();
        var clock = new ManualTimeProvider();
        var options = HookOptions();
        options.RecordExchanges = true;
        options.TimeProvider = clock;
        options.HookTimeLimit = TimeSpan.FromMilliseconds(250);
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        server.Faults.AddOverride(new SurlFaultScope { Stage = FaultStage.Command }, async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return SurlFault.ProtocolError("503 Busy");
        });

        var connection = new FakeConnection(Encoding.ASCII.GetBytes(HttpGet("/a", null)));
        seams.ListenerFactory.Accept("http", connection);
        await clock.WaitForTimerAsync(options.HookTimeLimit, TestContext.CancellationToken);
        clock.Advance(options.HookTimeLimit);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        var failed = (HookFailedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.HookFailed }, Timeout.InfiniteTimeSpan, TestContext.CancellationToken)
            .WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);

        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenToClient), "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("f1", failed.HookId);
        Assert.AreEqual(HookFailure.TimedOut, failed.Failure);
    }

    [TestMethod]
    public async Task AddOverride_ThatThrows_ServesTheRequestNormallyPublishesThrewAndStaysAdded()
    {
        var seams = new ApiTestSeams();
        var options = HookOptions();
        options.RecordExchanges = true;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var asked = 0;
        server.Faults.AddOverride(new SurlFaultScope { Stage = FaultStage.Command }, (_, _) =>
        {
            Interlocked.Increment(ref asked);
            throw new InvalidOperationException("override broke");
        });

        var first = await ServeAsync(seams, "http", HttpGet("/a", null));
        var second = await ServeAsync(seams, "http", HttpGet("/a", null));
        var failures = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.HookFailed });

        StringAssert.StartsWith(first, "HTTP/1.1 200 OK\r\n");
        StringAssert.StartsWith(second, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(2, asked);
        Assert.HasCount(2, failures);
        Assert.AreEqual(new HookFailedEvent("f1", HookFailure.Threw, "override broke"), failures[0] with { Sequence = 0, At = default });
    }

    [TestMethod]
    public async Task AddOverride_AnsweringEveryFaultAtEveryPoint_NeverLogsInAWrongPasswordNorServesADotSurlPath()
    {
        var seams = new ApiTestSeams();
        var options = HookOptions();
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.AllowPlaintextAuthentication = true;
        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var wrongPasswordWithoutOverride = await ServeAsync(seams, "http", HttpGet("/hello.txt", "alice:wrong"));
        var dotSurlWithoutOverride = await ServeAsync(seams, "http", HttpGet("/.surl/accounts", "alice:secret"));

        // Every answer a fault rule can give, chosen per point: none of them grants anything.
        server.Faults.AddOverride(new SurlFaultScope(), (point, _) => ValueTask.FromResult<SurlFault?>(point.Stage switch
        {
            FaultStage.Accept => SurlFault.Delay(TimeSpan.Zero),
            FaultStage.Login => null,
            FaultStage.Command => SurlFault.Delay(TimeSpan.Zero) with { Stage = FaultStage.Command },
            _ => SurlFault.OutOfSpace(),
        }));
        server.Faults.AddOverride(new SurlFaultScope { Stage = FaultStage.Login }, (_, _) => ValueTask.FromResult<SurlFault?>(SurlFault.RefuseLogin()));
        var wrongPassword = await ServeAsync(seams, "http", HttpGet("/hello.txt", "alice:wrong"));
        var dotSurl = await ServeAsync(seams, "http", HttpGet("/.surl/accounts", "alice:wrong"));

        StringAssert.StartsWith(wrongPasswordWithoutOverride, "HTTP/1.1 401 ");
        StringAssert.StartsWith(wrongPassword, "HTTP/1.1 401 ");
        Assert.IsFalse(dotSurlWithoutOverride.StartsWith("HTTP/1.1 2", StringComparison.Ordinal));
        Assert.IsFalse(dotSurl.StartsWith("HTTP/1.1 2", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AddOverride_Misused_Throws()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(HookOptions(), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => server.Faults.AddOverride(null!, (_, _) => ValueTask.FromResult<SurlFault?>(null)));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.Faults.AddOverride(new SurlFaultScope(), null!));
        var both = Assert.ThrowsExactly<ArgumentException>(() => server.Faults.AddOverride(
            new SurlFaultScope { Listener = Http, Scheme = "http" }, (_, _) => ValueTask.FromResult<SurlFault?>(null)));
        Assert.AreEqual("scope", both.ParamName);
    }

    [TestMethod]
    public async Task OnEvent_NullArguments_Throw()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(HookOptions(), seams.Host, TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => server.OnEvent(null!, (_, _) => ValueTask.CompletedTask));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.OnEvent(new SurlEventQuery(), null!));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "zero")]
    [DataRow(30_001, DisplayName = "past 30 seconds")]
    public async Task StartAsync_HookTimeLimitOutOfRange_ThrowsArgumentException(int milliseconds)
    {
        var options = HookOptions();
        options.HookTimeLimit = TimeSpan.FromMilliseconds(milliseconds);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken));

        StringAssert.StartsWith(exception.Message, "HookTimeLimit must be from 1 millisecond to 30 seconds.");
    }

    // hello.txt, a and b, on one HTTP listener.
    private static SurlServerOptions HookOptions()
    {
        var options = ApiTestSeams.Options(Http);
        options.Files["hello.txt"] = "hello"u8.ToArray();
        options.Files["a"] = "file a"u8.ToArray();
        options.Files["b"] = "file b"u8.ToArray();
        return options;
    }
}
