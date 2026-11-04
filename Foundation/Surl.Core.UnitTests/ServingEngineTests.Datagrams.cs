using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

// Datagram flows: a listen URL whose scheme belongs to an IDatagramProtocolServer (BL-032).
public sealed partial class ServingEngineTests
{
    private static readonly ListenUrl Tftp = new("tftp", "127.0.0.1", 0);
    private static readonly byte[] ReadRequest = [0, 1, (byte)'a', 0, (byte)'o', (byte)'c', (byte)'t', (byte)'e', (byte)'t', 0];

    [TestMethod]
    public void Constructor_ServerThatTakesBothConnectionsAndFlows_Throws()
    {
        IProtocolServer[] servers = [new FakeConnectionProtocolServer("http"), new FakeTwoTransportProtocolServer("tftp", "tftps")];

        var exception = Assert.ThrowsExactly<ArgumentException>(() => new ServingEngine(
            new FakeListenerFactory(), servers, new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod));

        Assert.AreEqual("protocolServers", exception.ParamName);
        StringAssert.Contains(exception.Message, "'tftp', 'tftps'");
    }

    [TestMethod]
    public async Task ServeAsync_TftpFlow_GoesToTheDatagramServerWithAFreshContext()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var time = new ManualTimeProvider();
        var server = new FakeDatagramProtocolServer("tftp");
        var engine = CreateEngine(factory, time, logs, server);
        var local = new IPEndPoint(IPAddress.Loopback, 40000);
        var remote = new IPEndPoint(IPAddress.Loopback, 51234);
        var flow = new FakeDatagramFlow(ReadRequest, local, remote);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(flow);
        var served = await server.NextFlowAsync();
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        CollectionAssert.AreEqual(new[] { Tftp }, factory.DatagramStartRequests.ToArray());
        Assert.IsEmpty(factory.StartRequests);
        var context = served.Context;
        Assert.AreEqual(1, context.ExchangeId);
        Assert.AreEqual("tftp", context.Scheme);
        Assert.AreEqual(factory.DatagramListenerFor(Tftp).ListenUrl, context.ListenUrl);
        Assert.AreEqual(40000, context.ListenUrl.BoundPort);
        Assert.AreEqual(local, context.LocalEndPoint);
        Assert.AreEqual(remote, context.RemoteEndPoint);
        Assert.AreSame(logs.LogOf(1), context.Log);
        Assert.AreEqual(remote, logs.RemoteEndPointOf(1));
        Assert.AreSame(time, context.TimeProvider);
        CollectionAssert.AreEqual(ReadRequest, served.Flow.FirstDatagram.ToArray());
        Assert.AreEqual(local, served.Flow.LocalEndPoint);
        Assert.AreEqual(remote, served.Flow.RemoteEndPoint);
        Assert.IsTrue(factory.DatagramListenerFor(Tftp).Disposed);
        StringAssert.EndsWith(logs.LogOf(1).Notes[^1], "ended; closing the flow.");
    }

    [TestMethod]
    public async Task ServeAsync_DatagramsOfAFlow_AreLoggedForTheServer_FirstDatagramFirst()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeDatagramProtocolServer(
            async (flow, context) =>
            {
                await flow.MoveToNewLocalPortAsync(context.CancellationToken);
                await flow.SendAsync("data"u8.ToArray(), context.CancellationToken);
                await flow.ReceiveAsync(context.CancellationToken);
            },
            "tftp");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var flow = new FakeDatagramFlow("rrq"u8.ToArray());
        flow.Arrive("ack"u8.ToArray());
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(flow);
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        var datagrams = logs.LogOf(1).Entries
            .Where(entry => entry.Kind != ExchangeLogEntryKind.Note)
            .Select(entry => $"{entry.Kind}:{Encoding.ASCII.GetString(entry.Bytes)}")
            .ToArray();
        CollectionAssert.AreEqual(new[] { "BytesReceived:rrq", "BytesSent:data", "BytesReceived:ack" }, datagrams);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 40001), flow.LocalEndPoint);
        Assert.AreEqual("data", Encoding.ASCII.GetString(flow.Sent.Single()));
    }

    [TestMethod]
    public async Task ServeAsync_DatagramServerThatThrows_EndsOnlyItsOwnFlow_AndTheListenerKeepsAccepting()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var failingClient = new IPEndPoint(IPAddress.Loopback, 50666);
        var server = new FakeDatagramProtocolServer(
            (flow, _) => failingClient.Equals(flow.RemoteEndPoint)
                ? throw new InvalidOperationException("boom")
                : Task.CompletedTask,
            "tftp");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var failed = new FakeDatagramFlow(ReadRequest, remoteEndPoint: failingClient);
        var next = new FakeDatagramFlow(ReadRequest);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(failed);
        var failedFlow = await server.NextFlowAsync();
        factory.DatagramListenerFor(Tftp).Open(next);
        await server.NextFlowAsync();
        await failed.WhenDisposed.WaitAsync(Patience.Timeout);
        await next.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        var notes = logs.LogOf(failedFlow.Context.ExchangeId).Notes;
        Assert.IsTrue(notes.Any(note => note.Contains("threw InvalidOperationException: boom", StringComparison.Ordinal)));
        StringAssert.EndsWith(notes[^1], "ended; closing the flow.");
    }

    [TestMethod]
    public async Task ServeAsync_Cancelled_StopsTheDatagramListener_AndCancelsAFlowStillRunningWhenTheGracePeriodRunsOut()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeDatagramProtocolServer(
            async (flow, context) => await flow.ReceiveAsync(context.CancellationToken), "tftp");
        var engine = CreateEngine(factory, time, logs, server);
        var flow = new FakeDatagramFlow(ReadRequest);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);
        factory.DatagramListenerFor(Tftp).Open(flow);
        var served = await server.NextFlowAsync();

        await stop.CancelAsync();
        await factory.DatagramListenerFor(Tftp).WhenDisposed.WaitAsync(Patience.Timeout);
        await time.FirstTimerCreated.WaitAsync(Patience.Timeout);
        time.Advance(GracePeriod - Tick);

        Assert.IsFalse(serving.IsCompleted);
        Assert.IsFalse(served.Context.CancellationToken.IsCancellationRequested);

        time.Advance(Tick);

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        Assert.IsTrue(served.Context.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(flow.Disposed);
        CollectionAssert.Contains(logs.LogOf(1).Notes.ToList(), "Exchange 1 cancelled at shutdown.");
    }

    [TestMethod]
    public async Task ServeAsync_TcpAndDatagramListenUrlsInOneRun_StartsBothKinds_AndServesEach()
    {
        var factory = new FakeListenerFactory();
        var httpServer = new FakeConnectionProtocolServer("http");
        var tftpServer = new FakeDatagramProtocolServer("tftp");
        var engine = CreateEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), httpServer, tftpServer);
        var connection = new InMemoryConnection([]);
        var flow = new FakeDatagramFlow(ReadRequest);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http, Tftp], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        factory.DatagramListenerFor(Tftp).Open(flow);
        var exchange = await httpServer.NextExchangeAsync();
        var served = await tftpServer.NextFlowAsync();
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        CollectionAssert.AreEqual(new[] { Http }, factory.StartRequests.ToArray());
        CollectionAssert.AreEqual(new[] { Tftp }, factory.DatagramStartRequests.ToArray());
        Assert.AreEqual("http", exchange.Context.Scheme);
        Assert.AreEqual("tftp", served.Context.Scheme);
        Assert.AreNotEqual(exchange.Context.ExchangeId, served.Context.ExchangeId);
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
        Assert.IsTrue(factory.DatagramListenerFor(Tftp).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_DatagramListenerThatFailsToBind_StopsTheListenersStarted_AndReturnsBindFailed()
    {
        var factory = new FakeListenerFactory()
            .FailToStart(Tftp, new ListenerBindException(Tftp, null, ListenerBindFailure.AddressInUse, null));
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http"), new FakeDatagramProtocolServer("tftp"));

        var exitCode = await engine.ServeAsync([Http, Tftp], CancellationToken.None).WaitAsync(Patience.Timeout);

        Assert.AreEqual(SurlExitCode.BindFailed, exitCode);
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_DatagramListenerThatFailsToAccept_StopsEveryListener_AndThrows()
    {
        var factory = new FakeListenerFactory();
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http"), new FakeDatagramProtocolServer("tftp"));

        var serving = engine.ServeAsync([Http, Tftp], CancellationToken.None);
        factory.DatagramListenerFor(Tftp).FailNextAccept(new IOException("receive failed"));

        await Assert.ThrowsExactlyAsync<IOException>(() => serving.WaitAsync(Patience.Timeout));
        Assert.IsTrue(factory.ListenerFor(Http).Disposed);
        Assert.IsTrue(factory.DatagramListenerFor(Tftp).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_FlowPastTheConnectionLimit_GetsTheServersRefusal_AndIsDisposedWithoutAnExchange()
    {
        var factory = new FakeListenerFactory();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeRefusalWritingDatagramProtocolServer((_, _) => release.Task, null, "tftp");
        var logs = new FakeExchangeLogFactory();
        var engine = new ServingEngine(factory, [server], logs, new ManualTimeProvider(), GracePeriod, OneConnection);
        var admitted = new FakeDatagramFlow(ReadRequest);
        var refused = new FakeDatagramFlow(ReadRequest, remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 51235));
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(admitted);
        await server.NextFlowAsync();
        factory.DatagramListenerFor(Tftp).Open(refused);
        var refusal = await server.NextRefusalAsync();
        await refused.WhenDisposed.WaitAsync(Patience.Timeout);

        Assert.AreEqual(ConnectionRefusal.TooManyConnections, refusal);
        Assert.AreEqual("refused:TooManyConnections", Encoding.ASCII.GetString(refused.Sent.Single()));
        Assert.AreEqual($"Refused a flow from {IPAddress.Loopback}:51235: past --max-connections 1.", logs.NotesOutsideExchanges.Single());
        Assert.IsFalse(server.TryTakeFlow(out _));
        Assert.IsFalse(admitted.Disposed);
        release.TrySetResult();
        await stop.CancelAsync();
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
    }

    [TestMethod]
    public async Task ServeAsync_FlowPastTheConnectionLimit_WhoseRefusalWriterThrows_IsStillDisposed()
    {
        var factory = new FakeListenerFactory();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeRefusalWritingDatagramProtocolServer(
            (_, _) => release.Task, (_, _, _) => throw new IOException("send failed"), "tftp");
        var engine = new ServingEngine(factory, [server], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod, OneConnection);
        var refused = new FakeDatagramFlow(ReadRequest, remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 51235));
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(new FakeDatagramFlow(ReadRequest));
        await server.NextFlowAsync();
        factory.DatagramListenerFor(Tftp).Open(refused);
        await server.NextRefusalAsync();
        await refused.WhenDisposed.WaitAsync(Patience.Timeout);

        Assert.IsEmpty(refused.Sent);
        release.TrySetResult();
        await stop.CancelAsync();
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
    }

    [TestMethod]
    public async Task ServeAsync_FlowPastTheConnectionLimit_OfAServerThatWritesNoRefusal_IsDisposedWithNoReply()
    {
        var factory = new FakeListenerFactory();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeDatagramProtocolServer((_, _) => release.Task, "tftp");
        var engine = new ServingEngine(factory, [server], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod, OneConnection);
        var refused = new FakeDatagramFlow(ReadRequest, remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 51235));
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(new FakeDatagramFlow(ReadRequest));
        await server.NextFlowAsync();
        factory.DatagramListenerFor(Tftp).Open(refused);
        await refused.WhenDisposed.WaitAsync(Patience.Timeout);

        Assert.IsEmpty(refused.Sent);
        Assert.IsFalse(server.TryTakeFlow(out _));
        release.TrySetResult();
        await stop.CancelAsync();
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
    }

    [TestMethod]
    public async Task ServeAsync_FlowPastThePerAddressLimit_IsRefused_AndAFlowFromAnotherAddressIsServed()
    {
        var factory = new FakeListenerFactory();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeRefusalWritingDatagramProtocolServer((_, _) => release.Task, null, "tftp");
        var limits = new ConnectionLimits(0, 1, TimeSpan.Zero, TimeSpan.Zero);
        var engine = new ServingEngine(factory, [server], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod, limits);
        var sameAddress = IPAddress.Parse("192.0.2.1");
        var refused = new FakeDatagramFlow(ReadRequest, remoteEndPoint: new IPEndPoint(sameAddress, 50001));
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(new FakeDatagramFlow(ReadRequest, remoteEndPoint: new IPEndPoint(sameAddress, 50000)));
        await server.NextFlowAsync();
        factory.DatagramListenerFor(Tftp).Open(refused);
        var refusal = await server.NextRefusalAsync();
        await refused.WhenDisposed.WaitAsync(Patience.Timeout);
        factory.DatagramListenerFor(Tftp).Open(
            new FakeDatagramFlow(ReadRequest, remoteEndPoint: new IPEndPoint(IPAddress.Parse("192.0.2.2"), 50000)));
        var otherAddress = await server.NextFlowAsync();

        Assert.AreEqual(ConnectionRefusal.TooManyConnectionsFromAddress, refusal);
        Assert.AreEqual("refused:TooManyConnectionsFromAddress", Encoding.ASCII.GetString(refused.Sent.Single()));
        Assert.AreEqual(2, otherAddress.Context.ExchangeId);
        release.TrySetResult();
        await stop.CancelAsync();
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
    }

    [TestMethod]
    public async Task ServeAsync_FlowThatReachesTheMaximumExchangeDuration_IsCancelledAndDisposedThenAndNotBefore()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeDatagramProtocolServer(
            async (flow, context) => await flow.ReceiveAsync(context.CancellationToken), "tftp");
        var limits = new ConnectionLimits(0, 0, TimeSpan.Zero, TimeSpan.FromSeconds(60));
        var engine = new ServingEngine(factory, [server], logs, time, GracePeriod, limits);
        var flow = new FakeDatagramFlow(ReadRequest);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(flow);
        var served = await server.NextFlowAsync();
        time.Advance(TimeSpan.FromSeconds(60) - Tick);

        Assert.IsFalse(served.Context.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(flow.Disposed);

        time.Advance(Tick);
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();

        Assert.IsTrue(served.Context.CancellationToken.IsCancellationRequested);
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "Exchange 1 cancelled: it reached the maximum exchange duration of 60 s.");
    }

    [TestMethod]
    public async Task ServeAsync_FlowWhoseExchangeLogThrows_IsDisposed_AndStopsCountingAgainstTheLimit()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory { CreateFailure = new IOException("stderr is gone") };
        var server = new FakeDatagramProtocolServer("tftp");
        var engine = new ServingEngine(factory, [server], logs, new ManualTimeProvider(), GracePeriod, OneConnection);
        var first = new FakeDatagramFlow(ReadRequest);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(first);
        await first.WhenDisposed.WaitAsync(Patience.Timeout);
        logs.CreateFailure = null;
        var served = await OpenUntilServedAsync(factory.DatagramListenerFor(Tftp), server);
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        Assert.IsFalse(server.TryTakeFlow(out _));
        Assert.AreEqual(2, served.Context.ExchangeId);
    }

    [TestMethod]
    public async Task ServeAsync_FlowWithNoDatagramForTheIdleTimeout_IsCancelledAndDisposedThenAndNotBefore()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeDatagramProtocolServer(
            async (flow, context) => await flow.ReceiveAsync(context.CancellationToken), "tftp");
        var limits = new ConnectionLimits(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero);
        var engine = new ServingEngine(factory, [server], logs, time, GracePeriod, limits);
        var flow = new FakeDatagramFlow(ReadRequest);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(flow);
        var served = await server.NextFlowAsync();
        time.Advance(TimeSpan.FromSeconds(10) - Tick);

        Assert.IsFalse(served.Context.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(flow.Disposed);

        time.Advance(Tick);
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();

        Assert.IsTrue(served.Context.CancellationToken.IsCancellationRequested);
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "Exchange 1 cancelled: no byte moved for the idle timeout of 10 s.");
    }

    // The engine stops counting a flow just after disposing it, so a flow opened the moment the
    // test sees the dispose may still be refused; open fresh ones until one is served. A served
    // flow reaches the server before it is disposed, so once one is disposed the server either
    // holds it or never will.
    private static async Task<ServedFlow> OpenUntilServedAsync(FakeDatagramListener listener, FakeDatagramProtocolServer server)
    {
        var deadline = DateTime.UtcNow + Patience.Timeout;

        while (DateTime.UtcNow < deadline)
        {
            var flow = new FakeDatagramFlow(ReadRequest);
            listener.Open(flow);
            await flow.WhenDisposed.WaitAsync(Patience.Timeout);

            if (server.TryTakeFlow(out var served))
            {
                return served!;
            }
        }

        throw new AssertFailedException("No flow was served.");
    }
}
