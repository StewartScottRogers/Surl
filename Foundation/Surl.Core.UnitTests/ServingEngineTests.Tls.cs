using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

public sealed partial class ServingEngineTests
{
    private static readonly ListenUrl HttpsListenUrl = new("https", "127.0.0.1", 8443);

    [TestMethod]
    public async Task ServeAsync_ImplicitTlsScheme_HandsTheServerTheSecuredConnection_AndNotesTheHandshake()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("https");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var withoutAlpn = new InMemoryConnection([]);
        var alpnSession = new TlsSession(SslProtocols.Tls12, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, "http/1.1", null, null);
        var withAlpn = new InMemoryConnection([], upgradeTlsSession: alpnSession);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([HttpsListenUrl], stop.Token);

        factory.ListenerFor(HttpsListenUrl).Connect(withoutAlpn);
        var first = await server.NextExchangeAsync();
        factory.ListenerFor(HttpsListenUrl).Connect(withAlpn);
        var second = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(withoutAlpn.UpgradeRequested);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, first.Connection.TlsSession);
        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "TLS handshake completed: Tls13, TLS_AES_128_GCM_SHA256, ALPN none");
        Assert.IsTrue(withAlpn.UpgradeRequested);
        Assert.AreSame(alpnSession, second.Connection.TlsSession);
        CollectionAssert.Contains(
            logs.LogOf(2).Notes.ToList(), "TLS handshake completed: Tls12, TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, ALPN http/1.1");
    }

    [TestMethod]
    public async Task ServeAsync_PlaintextScheme_NeverStartsAHandshake()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var connection = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(connection.UpgradeRequested);
        Assert.IsNull(exchange.Connection.TlsSession);
        Assert.IsFalse(logs.LogOf(1).Notes.Any(note => note.StartsWith("TLS handshake", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ImplicitHandshakeFails_ClosesTheConnectionUnserved_AndKeepsAccepting()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("https");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var failing = new InMemoryConnection([], upgradeFails: true);
        var next = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([HttpsListenUrl], stop.Token);

        factory.ListenerFor(HttpsListenUrl).Connect(failing);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(failing);
        factory.ListenerFor(HttpsListenUrl).Connect(next);
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.AreEqual(2, exchange.Context.ExchangeId);
        Assert.IsTrue(failing.UpgradeRequested);
        Assert.IsEmpty(failing.WrittenBytes);
        Assert.IsFalse(failing.Aborted);
        var notes = logs.LogOf(1).Notes.ToList();
        CollectionAssert.Contains(notes, "TLS handshake failed: The TLS handshake failed.");
        Assert.IsFalse(notes.Any(note => note.Contains("protocol server threw", StringComparison.Ordinal)));
        Assert.AreEqual("Exchange 1 ended; closing the connection.", notes[^1]);
        Assert.IsEmpty(logs.LogOf(1).Entries.Where(entry => entry.Kind != ExchangeLogEntryKind.Note));
    }

    [TestMethod]
    public async Task ServeAsync_NoHandshakeWithinTheHeadTimeout_ClosesTheConnectionUnserved()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var time = new ManualTimeProvider();
        var server = new FakeConnectionProtocolServer("https");
        var engine = CreateEngine(factory, time, logs, server);
        var connection = new PendingHandshakeConnection();
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([HttpsListenUrl], stop.Token);

        factory.ListenerFor(HttpsListenUrl).Connect(connection);
        await connection.HandshakeStarted.WaitAsync(Patience.Timeout);
        await time.FirstTimerCreated.WaitAsync(Patience.Timeout);
        time.Advance(ExchangeLimits.Default.HeadTimeout - TimeSpan.FromTicks(1));

        Assert.IsFalse(connection.HandshakeCancelled.IsCompleted);

        time.Advance(TimeSpan.FromTicks(1));
        await connection.Disposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(connection.HandshakeCancelled.IsCompleted);
        Assert.IsFalse(server.TryTakeExchange(out _));
        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "TLS handshake failed: no handshake within the head timeout of 30 s");
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownDuringTheImplicitHandshake_NotesTheShutdownCancellation()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var time = new ManualTimeProvider();
        var server = new FakeConnectionProtocolServer("https");
        var engine = new ServingEngine(factory, [server], logs, time, TimeSpan.Zero, ConnectionLimits.None);
        var connection = new PendingHandshakeConnection();
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([HttpsListenUrl], stop.Token);

        factory.ListenerFor(HttpsListenUrl).Connect(connection);
        await connection.HandshakeStarted.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(server.TryTakeExchange(out _));
        var notes = logs.LogOf(1).Notes.ToList();
        CollectionAssert.Contains(notes, "Exchange 1 cancelled at shutdown.");
        Assert.IsFalse(notes.Any(note => note.StartsWith("TLS handshake", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ServerUpgradeFails_IsNotedAsAFailedHandshakeNotAServerFault()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer(
            async (connection, context) => await connection.UpgradeToTlsAsync(context.CancellationToken),
            "http");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var connection = new InMemoryConnection([], upgradeFails: true);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        var notes = logs.LogOf(1).Notes.ToList();
        CollectionAssert.Contains(notes, "TLS handshake failed: The TLS handshake failed.");
        Assert.IsFalse(notes.Any(note => note.Contains("protocol server threw", StringComparison.Ordinal)));
        Assert.IsFalse(connection.Aborted);
    }

    /// <summary>
    /// A plaintext connection whose TLS handshake never completes: it waits until its token is
    /// cancelled, as a client that connects and sends no ClientHello does.
    /// </summary>
    private sealed class PendingHandshakeConnection : IConnection
    {
        private readonly TaskCompletionSource handshakeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource handshakeCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandshakeStarted => handshakeStarted.Task;

        public Task HandshakeCancelled => handshakeCancelled.Task;

        public Task Disposed => disposed.Task;

        public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 8443);

        public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

        public TlsSession? TlsSession => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            throw new AssertFailedException("A connection with no completed handshake was read.");

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
            throw new AssertFailedException("A connection with no completed handshake was written to.");

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public void Abort()
        {
        }

        public async ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken)
        {
            handshakeStarted.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                handshakeCancelled.TrySetResult();
            }

            throw new UnreachableException();
        }

        public ValueTask DisposeAsync()
        {
            disposed.TrySetResult();

            return ValueTask.CompletedTask;
        }
    }
}
