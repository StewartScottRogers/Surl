using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class ExchangeDataConnectionsTests
{
    private static readonly ConnectionLimits TenSecondsIdle = new(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero);
    private static readonly IPEndPoint ControlLocal = new(IPAddress.Loopback, 21);
    private static readonly IPEndPoint ControlRemote = new(IPAddress.Loopback, 50000);
    private static readonly IPEndPoint Announced = new(IPAddress.Loopback, 40000);
    private static readonly IPEndPoint CurlDataPort = new(IPAddress.Loopback, 50001);

    [TestMethod]
    public async Task StartPassiveListenerAsync_PassesTheControlEndPointsOn_AndAnnouncesTheListenersEndPoint()
    {
        var opener = new InMemoryDataConnections().ScriptPassiveListener(Announced, null);
        using var deadlines = TenSecondDeadlines(new ManualTimeProvider());
        await using var dataConnections = new ExchangeDataConnections(opener, new RecordingExchangeLog(), deadlines);

        var listener = await dataConnections.StartPassiveListenerAsync(ControlLocal, ControlRemote, CancellationToken.None);

        Assert.AreEqual(Announced, listener.LocalEndPoint);
        Assert.AreEqual(new PassiveListenerRequest(ControlLocal, ControlRemote), opener.PassiveRequests.Single());
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_OpenerThatRefuses_ThrowsItsFailure()
    {
        var opener = new InMemoryDataConnections().ScriptPassiveFailure(DataConnectionFailure.Unreachable);
        using var deadlines = TenSecondDeadlines(new ManualTimeProvider());
        await using var dataConnections = new ExchangeDataConnections(opener, new RecordingExchangeLog(), deadlines);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(
            () => dataConnections.StartPassiveListenerAsync(ControlLocal, ControlRemote, CancellationToken.None).AsTask());

        Assert.AreEqual(DataConnectionFailure.Unreachable, exception.Failure);
    }

    [TestMethod]
    public async Task AcceptAsync_NotesThePassiveConnection_AndLogsItsBytes()
    {
        var accepted = new InMemoryConnection(["STOR"u8.ToArray()], ControlLocal, CurlDataPort);
        var opener = new InMemoryDataConnections().ScriptPassiveListener(Announced, accepted);
        var log = new RecordingExchangeLog();
        using var deadlines = TenSecondDeadlines(new ManualTimeProvider());
        await using var dataConnections = new ExchangeDataConnections(opener, log, deadlines);
        var listener = await dataConnections.StartPassiveListenerAsync(ControlLocal, ControlRemote, CancellationToken.None);

        var connection = await listener.AcceptAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        await connection.ReadAsync(new byte[16], CancellationToken.None);
        await connection.WriteAsync("sent"u8.ToArray(), CancellationToken.None);
        await connection.DisposeAsync();

        Assert.AreEqual(TimeSpan.FromSeconds(30), opener.PassiveListeners.Single().AcceptTimeouts.Single());
        CollectionAssert.AreEqual(
            new[]
            {
                new ExchangeLogEntry(ExchangeLogEntryKind.Note, [], "Data connection opened: passive from 127.0.0.1:50001."),
                new ExchangeLogEntry(ExchangeLogEntryKind.BytesReceived, "STOR"u8.ToArray(), string.Empty),
                new ExchangeLogEntry(ExchangeLogEntryKind.BytesSent, "sent"u8.ToArray(), string.Empty),
                new ExchangeLogEntry(ExchangeLogEntryKind.Note, [], "Data connection closed."),
            }.Select(Describe).ToList(),
            log.Entries.Select(Describe).ToList());
        Assert.IsTrue(accepted.Disposed);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_PassesTheRequestOn_NotesTheActiveConnection_AndLogsItsBytes()
    {
        var opened = new InMemoryConnection(["RETR"u8.ToArray()], ControlLocal, CurlDataPort);
        var opener = new InMemoryDataConnections().ScriptActiveConnection(opened);
        var log = new RecordingExchangeLog();
        using var deadlines = TenSecondDeadlines(new ManualTimeProvider());
        await using var dataConnections = new ExchangeDataConnections(opener, log, deadlines);

        var connection = await dataConnections.ConnectActiveAsync(
            ControlRemote, CurlDataPort, TimeSpan.FromSeconds(30), CancellationToken.None);
        await connection.WriteAsync("hello"u8.ToArray(), CancellationToken.None);
        await connection.ReadAsync(new byte[16], CancellationToken.None);

        Assert.AreEqual(
            new ActiveConnectionRequest(ControlRemote, CurlDataPort, TimeSpan.FromSeconds(30)), opener.ActiveRequests.Single());
        CollectionAssert.AreEqual(
            new[]
            {
                new ExchangeLogEntry(ExchangeLogEntryKind.Note, [], "Data connection opened: active to 127.0.0.1:50001."),
                new ExchangeLogEntry(ExchangeLogEntryKind.BytesSent, "hello"u8.ToArray(), string.Empty),
                new ExchangeLogEntry(ExchangeLogEntryKind.BytesReceived, "RETR"u8.ToArray(), string.Empty),
            }.Select(Describe).ToList(),
            log.Entries.Select(Describe).ToList());
        CollectionAssert.AreEqual("hello"u8.ToArray(), opened.WrittenBytes);
    }

    [TestMethod]
    public async Task DataConnection_BytesReadOrWritten_RestartTheExchangesIdleClock()
    {
        var time = new ManualTimeProvider();
        var opener = new InMemoryDataConnections().ScriptActiveConnection(
            new InMemoryConnection(["RETR"u8.ToArray()], ControlLocal, CurlDataPort, peerHalfClosesWhenExhausted: false));
        using var deadlines = TenSecondDeadlines(time);
        await using var dataConnections = new ExchangeDataConnections(opener, new RecordingExchangeLog(), deadlines);
        var connection = await dataConnections.ConnectActiveAsync(
            ControlRemote, CurlDataPort, TimeSpan.FromSeconds(30), CancellationToken.None);

        time.Advance(TimeSpan.FromSeconds(9));
        await connection.ReadAsync(new byte[16], CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(9));
        await connection.WriteAsync("hello"u8.ToArray(), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(9));

        Assert.IsNull(deadlines.Reason);

        time.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(ExchangeCancellation.IdleTimeout, deadlines.Reason);
    }

    [TestMethod]
    public async Task DataConnection_PassesEveryOtherCallThrough()
    {
        var opened = new InMemoryConnection([], ControlLocal, CurlDataPort);
        var opener = new InMemoryDataConnections().ScriptActiveConnection(opened);
        using var deadlines = TenSecondDeadlines(new ManualTimeProvider());
        await using var dataConnections = new ExchangeDataConnections(opener, new RecordingExchangeLog(), deadlines);
        var connection = await dataConnections.ConnectActiveAsync(
            ControlRemote, CurlDataPort, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.AreEqual(ControlLocal, connection.LocalEndPoint);
        Assert.AreEqual(CurlDataPort, connection.RemoteEndPoint);
        Assert.IsNull(connection.TlsSession);

        var session = await connection.UpgradeToTlsAsync(CancellationToken.None);
        await connection.CompleteWritesAsync(CancellationToken.None);
        connection.Abort();

        Assert.AreSame(session, connection.TlsSession);
        Assert.IsTrue(opened.UpgradeRequested);
        Assert.IsTrue(opened.WritesCompleted);
        Assert.IsTrue(opened.Aborted);
    }

    [TestMethod]
    public async Task DataConnection_DisposedTwice_NotesItClosedOnce()
    {
        var log = new RecordingExchangeLog();
        var opener = new InMemoryDataConnections().ScriptActiveConnection(new InMemoryConnection([], ControlLocal, CurlDataPort));
        using var deadlines = TenSecondDeadlines(new ManualTimeProvider());
        var dataConnections = new ExchangeDataConnections(opener, log, deadlines);
        var connection = await dataConnections.ConnectActiveAsync(
            ControlRemote, CurlDataPort, TimeSpan.FromSeconds(30), CancellationToken.None);

        await connection.DisposeAsync();
        await connection.DisposeAsync();
        await dataConnections.DisposeAsync();

        Assert.AreEqual(1, log.Notes.Count(note => note == "Data connection closed."));
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesEveryListenerAndConnectionLeftOpen_EvenAfterOneFailsToDispose()
    {
        var failing = new DisposeFailingConnection(new InMemoryConnection([], ControlLocal, CurlDataPort));
        var leftOpen = new InMemoryConnection([], ControlLocal, CurlDataPort);
        var opener = new InMemoryDataConnections()
            .ScriptActiveConnection(failing)
            .ScriptActiveConnection(leftOpen)
            .ScriptPassiveListener(Announced, null);
        var log = new RecordingExchangeLog();
        using var deadlines = TenSecondDeadlines(new ManualTimeProvider());
        var dataConnections = new ExchangeDataConnections(opener, log, deadlines);
        await dataConnections.ConnectActiveAsync(ControlRemote, CurlDataPort, TimeSpan.FromSeconds(30), CancellationToken.None);
        await dataConnections.ConnectActiveAsync(ControlRemote, CurlDataPort, TimeSpan.FromSeconds(30), CancellationToken.None);
        await dataConnections.StartPassiveListenerAsync(ControlLocal, ControlRemote, CancellationToken.None);

        await dataConnections.DisposeAsync();

        Assert.IsTrue(failing.DisposeCalled);
        Assert.IsTrue(leftOpen.Disposed);
        Assert.IsTrue(opener.PassiveListeners.Single().Disposed);
        Assert.AreEqual(2, log.Notes.Count(note => note == "Data connection closed."));
    }

    [TestMethod]
    public async Task DisposeAsync_LeavesAListenerTheServerDisposedAlone()
    {
        var inner = new CountingPassiveListener();
        using var deadlines = TenSecondDeadlines(new ManualTimeProvider());
        var dataConnections = new ExchangeDataConnections(new OneListenerOpener(inner), new RecordingExchangeLog(), deadlines);
        var listener = await dataConnections.StartPassiveListenerAsync(ControlLocal, ControlRemote, CancellationToken.None);
        await listener.DisposeAsync();

        await dataConnections.DisposeAsync();

        Assert.AreEqual(1, inner.DisposeCount);
    }

    private static ExchangeDeadlines TenSecondDeadlines(TimeProvider time) =>
        new(TenSecondsIdle, time, CancellationToken.None);

    private static string Describe(ExchangeLogEntry entry) =>
        $"{entry.Kind}:{Convert.ToHexString(entry.Bytes)}:{entry.Text}";

    private sealed class CountingPassiveListener : IPassiveDataListener
    {
        public int DisposeCount { get; private set; }

        public IPEndPoint LocalEndPoint => Announced;

        public ValueTask<IConnection> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class OneListenerOpener(IPassiveDataListener listener) : IDataConnectionOpener
    {
        public ValueTask<IPassiveDataListener> StartPassiveListenerAsync(
            EndPoint controlLocal, EndPoint controlRemote, CancellationToken cancellationToken) =>
            ValueTask.FromResult(listener);

        public ValueTask<IConnection> ConnectActiveAsync(
            EndPoint controlRemote, IPEndPoint target, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
