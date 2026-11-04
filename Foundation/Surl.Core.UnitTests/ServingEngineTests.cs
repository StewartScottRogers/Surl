using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed partial class ServingEngineTests
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(5);
    private static readonly ListenUrl Http = new("http", "127.0.0.1", 8080);
    private static readonly ListenUrl Gopher = new("gopher", "127.0.0.1", 0);

    [TestMethod]
    public void Constructor_TwoServersListingOneScheme_Throws()
    {
        IProtocolServer[] servers = [new FakeConnectionProtocolServer("http", "https"), new FakeConnectionProtocolServer("https")];

        var exception = Assert.ThrowsExactly<ArgumentException>(() => new ServingEngine(
            new FakeListenerFactory(), servers, new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod));

        Assert.AreEqual("protocolServers", exception.ParamName);
        StringAssert.Contains(exception.Message, "'https'");
    }

    [TestMethod]
    public void Constructor_NegativeGracePeriod_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ServingEngine(
            new FakeListenerFactory(), [], new FakeExchangeLogFactory(), new ManualTimeProvider(), TimeSpan.FromTicks(-1)));
    }

    [TestMethod]
    public void DefaultShutdownGracePeriod_IsFiveSeconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(5), ServingEngine.DefaultShutdownGracePeriod);
    }

    [TestMethod]
    public async Task ServeAsync_NoListenUrls_Throws()
    {
        var engine = CreateEngine(new FakeListenerFactory(), new ManualTimeProvider(), new FakeExchangeLogFactory());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.ServeAsync([], CancellationToken.None));

        Assert.AreEqual("listenUrls", exception.ParamName);
    }

    [TestMethod]
    public async Task ServeAsync_SchemeWithNoServer_ReturnsUnsupportedProtocolBeforeAnyListenerStarts()
    {
        var factory = new FakeListenerFactory();
        var engine = CreateEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), new FakeConnectionProtocolServer("http"));

        var exitCode = await engine.ServeAsync([Http, new ListenUrl("dict", "127.0.0.1", 2628)], CancellationToken.None);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.IsEmpty(factory.StartRequests);
    }

    [TestMethod]
    public async Task ServeAsync_SchemeOfAServerThatTakesNeitherConnectionsNorFlows_ReturnsUnsupportedProtocolBeforeAnyListenerStarts()
    {
        var factory = new FakeListenerFactory();
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeDatagramProtocolServer("tftp"), new FakeTransportlessProtocolServer("ldap"));

        var exitCode = await engine.ServeAsync(
            [new ListenUrl("tftp", "127.0.0.1", 69), new ListenUrl("ldap", "127.0.0.1", 389)], CancellationToken.None);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.IsEmpty(factory.StartRequests);
        Assert.IsEmpty(factory.DatagramStartRequests);
    }

    [TestMethod]
    public async Task ServeAsync_StartsOneListenerPerListenUrl_AndStopsThemWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http"), new FakeConnectionProtocolServer("gopher"));
        using var stop = new CancellationTokenSource();

        var serving = engine.ServeAsync([Http, Gopher], stop.Token);

        CollectionAssert.AreEqual(new[] { Http, Gopher }, factory.StartRequests.ToArray());
        Assert.IsFalse(serving.IsCompleted);

        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
        Assert.IsTrue(factory.ListenerFor(Gopher).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_Connection_GoesToTheServerForItsSchemeWithAFreshContext()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var time = new ManualTimeProvider();
        var httpServer = new FakeConnectionProtocolServer("http");
        var gopherServer = new FakeConnectionProtocolServer("gopher");
        var engine = CreateEngine(factory, time, logs, httpServer, gopherServer);
        var local = new IPEndPoint(IPAddress.Loopback, 40000);
        var remote = new IPEndPoint(IPAddress.Loopback, 51234);
        var connection = new InMemoryConnection([], local, remote);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http, Gopher], stop.Token);

        factory.ListenerFor(Gopher).Connect(connection);
        var exchange = await gopherServer.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        var context = exchange.Context;
        Assert.AreEqual(1, context.ExchangeId);
        Assert.AreEqual("gopher", context.Scheme);
        Assert.AreEqual(factory.ListenerFor(Gopher).ListenUrl, context.ListenUrl);
        Assert.AreEqual(40000, context.ListenUrl.BoundPort);
        Assert.AreEqual(local, context.LocalEndPoint);
        Assert.AreEqual(remote, context.RemoteEndPoint);
        Assert.AreSame(logs.LogOf(1), context.Log);
        Assert.AreEqual(remote, logs.RemoteEndPointOf(1));
        Assert.AreSame(time, context.TimeProvider);
        Assert.IsFalse(context.CancellationToken.IsCancellationRequested);
        Assert.AreEqual(local, exchange.Connection.LocalEndPoint);
        Assert.AreEqual(remote, exchange.Connection.RemoteEndPoint);
        Assert.IsFalse(httpServer.TryTakeExchange(out _));
        Assert.IsTrue(connection.Disposed);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task ServeAsync_EachExchange_GetsItsOwnIdAndLog()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var first = await server.NextExchangeAsync();
        var second = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        CollectionAssert.AreEquivalent(new long[] { 1, 2 }, new[] { first.Context.ExchangeId, second.Context.ExchangeId });
        Assert.AreNotSame(first.Context.Log, second.Context.Log);
        Assert.AreSame(logs.LogOf(first.Context.ExchangeId), first.Context.Log);
    }

    [TestMethod]
    public async Task ServeAsync_TwoConnectionsOnOneListener_AreServedConcurrently()
    {
        var factory = new FakeListenerFactory();
        var bothInFlight = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = 0;
        var server = new FakeConnectionProtocolServer(
            async (_, _) =>
            {
                if (Interlocked.Increment(ref inFlight) == 2)
                {
                    bothInFlight.TrySetResult();
                }

                await bothInFlight.Task.WaitAsync(Patience.Timeout);
            },
            "http");
        var engine = CreateEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), server);
        var first = new InMemoryConnection([]);
        var second = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(first);
        factory.ListenerFor(Http).Connect(second);
        await bothInFlight.Task.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(first.Disposed && !first.Aborted);
        Assert.IsTrue(second.Disposed && !second.Aborted);
    }

    [TestMethod]
    public async Task ServeAsync_ServerThatThrows_EndsOnlyItsOwnExchange_AndTheListenerKeepsAccepting()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var failingClient = new IPEndPoint(IPAddress.Loopback, 50666);
        var server = new FakeConnectionProtocolServer(
            (connection, _) => failingClient.Equals(connection.RemoteEndPoint)
                ? throw new InvalidOperationException("boom")
                : Task.CompletedTask,
            "http");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var failed = new InMemoryConnection([], remoteEndPoint: failingClient);
        var next = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(failed);
        var failedExchange = await server.NextExchangeAsync();
        factory.ListenerFor(Http).Connect(next);
        await server.NextExchangeAsync();
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        Assert.IsTrue(failed.Aborted);
        Assert.IsTrue(failed.Disposed);
        Assert.IsFalse(next.Aborted);
        Assert.IsTrue(next.Disposed);
        var notes = logs.LogOf(failedExchange.Context.ExchangeId).Notes;
        Assert.IsTrue(notes.Any(note => note.Contains("threw InvalidOperationException: boom", StringComparison.Ordinal)));
        StringAssert.EndsWith(notes[^1], "ended; closing the connection.");
    }

    [TestMethod]
    public async Task ServeAsync_ServerThatThrowsCancellationBeforeShutdown_HasItsExchangeAborted()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer((_, _) => throw new OperationCanceledException(), "http");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var connection = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(connection.Aborted);
        Assert.IsTrue(logs.LogOf(1).Notes.Any(note => note.Contains("threw OperationCanceledException", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeLogThatThrows_StillHasItsConnectionAbortedAndDisposed()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory { CreateFailure = new IOException("stderr is gone") };
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, new FakeConnectionProtocolServer("http"));
        var connection = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        Assert.IsTrue(connection.Aborted);
    }

    [TestMethod]
    public async Task ServeAsync_ListenerThatFailsToStop_StillHasTheOthersStopped_AndThrows()
    {
        var factory = new FakeListenerFactory();
        factory.ListenerFor(Http).DisposeFailure = new IOException("close failed");
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http", "gopher"));
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http, Gopher], stop.Token);

        await stop.CancelAsync();

        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => serving.WaitAsync(Patience.Timeout));
        Assert.AreEqual("close failed", exception.Message);
        Assert.IsTrue(factory.ListenerFor(Gopher).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_ListenerThatFailsToStopAfterABindFailure_DoesNotHideTheBindExitCode()
    {
        var https = new ListenUrl("https", "127.0.0.1", 8443);
        var factory = new FakeListenerFactory()
            .FailToStart(https, new ListenerBindException(https, null, ListenerBindFailure.AddressInUse, null));
        factory.ListenerFor(Http).DisposeFailure = new IOException("close failed");
        factory.ListenerFor(Gopher).DisposeFailure = new IOException("close failed too");
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http", "https", "gopher"));

        var exitCode = await engine.ServeAsync([Http, Gopher, https], CancellationToken.None).WaitAsync(Patience.Timeout);

        Assert.AreEqual(SurlExitCode.BindFailed, exitCode);
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
        Assert.IsTrue(factory.ListenerFor(Gopher).Disposed);
    }

    [TestMethod]
    [DataRow(ListenerBindFailure.AddressInUse, SurlExitCode.BindFailed)]
    [DataRow(ListenerBindFailure.AddressNotAvailable, SurlExitCode.BindFailed)]
    [DataRow(ListenerBindFailure.PermissionDenied, SurlExitCode.BindFailed)]
    [DataRow(ListenerBindFailure.Other, SurlExitCode.BindFailed)]
    [DataRow(ListenerBindFailure.HostNotFound, SurlExitCode.CouldNotResolveHost)]
    public async Task ServeAsync_ListenerThatFailsToBind_StopsTheListenersStarted_AndReturnsTheBindExitCode(
        ListenerBindFailure failure, SurlExitCode expected)
    {
        var factory = new FakeListenerFactory()
            .FailToStart(Gopher, new ListenerBindException(Gopher, null, failure, null));
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http", "gopher"));

        var exitCode = await engine.ServeAsync([Http, Gopher], CancellationToken.None).WaitAsync(Patience.Timeout);

        Assert.AreEqual(expected, exitCode);
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
        Assert.AreEqual(0, factory.ListenerFor(Http).AcceptCalls);
    }

    [TestMethod]
    public async Task ServeAsync_CancelledWhileStarting_StopsTheListenersStarted_AndReturnsOk()
    {
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        var factory = new FakeListenerFactory().FailToStart(Gopher, new OperationCanceledException(stop.Token));
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http", "gopher"));

        var exitCode = await engine.ServeAsync([Http, Gopher], stop.Token).WaitAsync(Patience.Timeout);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
        Assert.AreEqual(0, factory.ListenerFor(Http).AcceptCalls);
    }

    [TestMethod]
    public async Task ServeAsync_UnexpectedFailureWhileStarting_StopsTheListenersStarted_AndThrows()
    {
        var factory = new FakeListenerFactory().FailToStart(Gopher, new InvalidOperationException("no"));
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http", "gopher"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => engine.ServeAsync([Http, Gopher], CancellationToken.None).WaitAsync(Patience.Timeout));

        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_CancellationThatIsNotTheCallersWhileStarting_StopsTheListenersStarted_AndThrows()
    {
        var factory = new FakeListenerFactory().FailToStart(Gopher, new OperationCanceledException());
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http", "gopher"));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => engine.ServeAsync([Http, Gopher], CancellationToken.None).WaitAsync(Patience.Timeout));

        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_ListenerThatFailsToAccept_StopsEveryListener_AndThrows()
    {
        var factory = new FakeListenerFactory();
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http", "gopher"));

        var serving = engine.ServeAsync([Http, Gopher], CancellationToken.None);
        factory.ListenerFor(Http).FailNextAccept(new IOException("accept failed"));

        await Assert.ThrowsExactlyAsync<IOException>(() => serving.WaitAsync(Patience.Timeout));
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
        Assert.IsTrue(factory.ListenerFor(Gopher).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_ListenerThatThrowsCancellationNobodyAskedFor_StopsEveryListener_AndThrows()
    {
        var factory = new FakeListenerFactory();
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http", "gopher"));

        var serving = engine.ServeAsync([Http, Gopher], CancellationToken.None);
        factory.ListenerFor(Gopher).FailNextAccept(new OperationCanceledException());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => serving.WaitAsync(Patience.Timeout));
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
        Assert.IsTrue(factory.ListenerFor(Gopher).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_Cancelled_StopsAccepting_AndWaitsForAnExchangeThatEndsWithinTheGracePeriod()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeConnectionProtocolServer((_, _) => release.Task, "http");
        var engine = CreateEngine(factory, time, new FakeExchangeLogFactory(), server);
        var connection = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);
        factory.ListenerFor(Http).Connect(connection);
        var exchange = await server.NextExchangeAsync();

        await stop.CancelAsync();
        await factory.ListenerFor(Http).WhenDisposed.WaitAsync(Patience.Timeout);
        await time.FirstTimerCreated.WaitAsync(Patience.Timeout);
        time.Advance(GracePeriod - TimeSpan.FromTicks(1));
        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));

        Assert.IsFalse(serving.IsCompleted);
        release.TrySetResult();
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        Assert.IsFalse(exchange.Context.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(connection.Disposed && !connection.Aborted);
        Assert.IsFalse(server.TryTakeExchange(out _));
    }

    [TestMethod]
    public async Task ServeAsync_Cancelled_CancelsAnExchangeStillRunningWhenTheGracePeriodRunsOut()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer(
            (_, context) => Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken), "http");
        var engine = CreateEngine(factory, time, logs, server);
        var connection = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);
        factory.ListenerFor(Http).Connect(connection);
        var exchange = await server.NextExchangeAsync();

        await stop.CancelAsync();
        await time.FirstTimerCreated.WaitAsync(Patience.Timeout);
        time.Advance(GracePeriod - TimeSpan.FromTicks(1));

        Assert.IsFalse(serving.IsCompleted);
        Assert.IsFalse(exchange.Context.CancellationToken.IsCancellationRequested);

        time.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        Assert.IsTrue(exchange.Context.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(connection.Disposed && !connection.Aborted);
        CollectionAssert.Contains(logs.LogOf(1).Notes.ToList(), "Exchange 1 cancelled at shutdown.");
        Assert.IsTrue(exchange.Context.ShutdownToken.IsCancellationRequested);
        Assert.IsFalse(exchange.Context.IsCancelledForALimit);
    }

    [TestMethod]
    public async Task ServeAsync_BytesTheServerReadsAndWrites_AreLoggedForIt()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer(
            async (connection, context) =>
            {
                var buffer = new byte[16];
                var count = await connection.ReadAsync(buffer, context.CancellationToken);
                await connection.ReadAsync(buffer.AsMemory(count), context.CancellationToken);
                await connection.WriteAsync("pong"u8.ToArray(), context.CancellationToken);
            },
            "http");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection(["ping"u8.ToArray()]));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        var bytes = logs.LogOf(1).Entries
            .Where(entry => entry.Kind != ExchangeLogEntryKind.Note)
            .Select(entry => $"{entry.Kind}:{Encoding.ASCII.GetString(entry.Bytes)}")
            .ToArray();
        CollectionAssert.AreEqual(new[] { "BytesReceived:ping", "BytesSent:pong" }, bytes);
    }

    // No connection limits, so the shutdown grace period's timer is the only one created;
    // ServingEngineTests.Limits.cs covers the limits.
    private static ServingEngine CreateEngine(
        FakeListenerFactory factory, TimeProvider time, FakeExchangeLogFactory logs, params IProtocolServer[] servers) =>
        new(factory, servers, logs, time, GracePeriod, ConnectionLimits.None);
}
