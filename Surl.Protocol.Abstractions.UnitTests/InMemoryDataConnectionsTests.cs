using System.Net;
using System.Text;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class InMemoryDataConnectionsTests
{
    private static readonly IPEndPoint ControlLocal = new(IPAddress.Loopback, 21);
    private static readonly IPEndPoint ControlRemote = new(IPAddress.Loopback, 50000);
    private static readonly IPEndPoint Announced = new(IPAddress.Loopback, 40001);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartPassiveListenerAsync_ScriptedListener_AnnouncesItsEndPointAndRecordsTheRequest()
    {
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, null);

        var listener = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);

        Assert.AreSame(Announced, listener.LocalEndPoint);
        CollectionAssert.AreEqual(new[] { new PassiveListenerRequest(ControlLocal, ControlRemote) }, connections.PassiveRequests.ToArray());
        Assert.AreSame(listener, connections.PassiveListeners.Single());
    }

    [TestMethod]
    public async Task AcceptAsync_ScriptedAccept_HandsOutTheConnectionCurlOpened()
    {
        var curl = new InMemoryConnection([Encoding.ASCII.GetBytes("hello world\n")]);
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, curl);
        var listener = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);

        var accepted = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        Assert.AreSame(curl, accepted);
        CollectionAssert.AreEqual(new[] { Timeout }, connections.PassiveListeners[0].AcceptTimeouts.ToArray());
    }

    [TestMethod]
    public async Task AcceptAsync_ScriptedAccept_CarriesTheBytesCurlSendsAndRecordsTheBytesWritten()
    {
        var curl = new InMemoryConnection([Encoding.ASCII.GetBytes("upload")]);
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, curl);
        var listener = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);
        var accepted = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);
        var buffer = new byte[16];

        var read = await accepted.ReadAsync(buffer, TestContext.CancellationToken);
        await accepted.WriteAsync(Encoding.ASCII.GetBytes("download"), TestContext.CancellationToken);

        Assert.AreEqual("upload", Encoding.ASCII.GetString(buffer, 0, read));
        Assert.AreEqual("download", Encoding.ASCII.GetString(curl.WrittenBytes));
    }

    [TestMethod]
    public async Task AcceptAsync_ScriptedEarlyClose_ReadsEndOfStreamOnceTheScriptIsExhausted()
    {
        var curl = new InMemoryConnection([Encoding.ASCII.GetBytes("par")]);
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, curl);
        var listener = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);
        var accepted = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);
        var buffer = new byte[16];

        await accepted.ReadAsync(buffer, TestContext.CancellationToken);
        var afterClose = await accepted.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual(0, afterClose);
    }

    [TestMethod]
    public async Task AcceptAsync_ScriptedUpgrade_UpgradesTheDataConnectionToTls()
    {
        var curl = new InMemoryConnection([]);
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, curl);
        var listener = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);
        var accepted = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        var session = await accepted.UpgradeToTlsAsync(TestContext.CancellationToken);

        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, session);
        Assert.IsTrue(curl.UpgradeRequested);
    }

    [TestMethod]
    public async Task AcceptAsync_CurlNeverConnects_ThrowsTimedOut()
    {
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, null);
        var listener = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(async () =>
            await listener.AcceptAsync(Timeout, TestContext.CancellationToken));

        Assert.AreEqual(DataConnectionFailure.TimedOut, exception.Failure);
    }

    [TestMethod]
    public async Task AcceptAsync_Twice_HandsOutTheConnectionOnceThenTimesOut()
    {
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, new InMemoryConnection([]));
        var listener = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);
        await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(async () =>
            await listener.AcceptAsync(TimeSpan.FromSeconds(1), TestContext.CancellationToken));

        Assert.AreEqual(DataConnectionFailure.TimedOut, exception.Failure);
        CollectionAssert.AreEqual(new[] { Timeout, TimeSpan.FromSeconds(1) }, connections.PassiveListeners[0].AcceptTimeouts.ToArray());
    }

    [TestMethod]
    public async Task AcceptAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, new InMemoryConnection([]));
        var listener = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);

        await listener.DisposeAsync();

        Assert.IsTrue(connections.PassiveListeners[0].Disposed);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await listener.AcceptAsync(Timeout, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AcceptAsync_Cancelled_ThrowsOperationCanceled()
    {
        var listener = new InMemoryPassiveDataListener(Announced, new InMemoryConnection([]));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await listener.AcceptAsync(Timeout, new CancellationToken(canceled: true)));
        Assert.IsEmpty(listener.AcceptTimeouts);
    }

    [TestMethod]
    public void InMemoryPassiveDataListener_NullEndPoint_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new InMemoryPassiveDataListener(null!, null));
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_ScriptedFailure_ThrowsItAndRecordsTheRequest()
    {
        var connections = new InMemoryDataConnections().ScriptPassiveFailure(DataConnectionFailure.Unreachable);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(async () =>
            await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken));

        Assert.AreEqual(DataConnectionFailure.Unreachable, exception.Failure);
        Assert.HasCount(1, connections.PassiveRequests);
        Assert.IsEmpty(connections.PassiveListeners);
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_ScriptExhausted_ThrowsUnavailable()
    {
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, null);
        await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(async () =>
            await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken));

        Assert.AreEqual(DataConnectionFailure.Unavailable, exception.Failure);
        Assert.HasCount(2, connections.PassiveRequests);
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_TwoScriptedListeners_HandsThemOutInOrder()
    {
        var second = new IPEndPoint(IPAddress.Loopback, 40002);
        var connections = new InMemoryDataConnections()
            .ScriptPassiveListener(Announced, null)
            .ScriptPassiveListener(second, null);

        var first = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);
        var replacement = await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);

        Assert.AreSame(Announced, first.LocalEndPoint);
        Assert.AreSame(second, replacement.LocalEndPoint);
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_Cancelled_ThrowsOperationCanceledAndRecordsNothing()
    {
        var connections = new InMemoryDataConnections().ScriptPassiveListener(Announced, null);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await connections.StartPassiveListenerAsync(ControlLocal, ControlRemote, new CancellationToken(canceled: true)));

        Assert.IsEmpty(connections.PassiveRequests);
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_NullEndPoints_Throw()
    {
        var connections = new InMemoryDataConnections();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await connections.StartPassiveListenerAsync(null!, ControlRemote, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await connections.StartPassiveListenerAsync(ControlLocal, null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public void ScriptPassiveListener_NullEndPoint_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new InMemoryDataConnections().ScriptPassiveListener(null!, null));
    }

    [TestMethod]
    public async Task ConnectActiveAsync_ScriptedConnect_HandsOutTheConnectionAndRecordsTheAddressNamed()
    {
        var curl = new InMemoryConnection([Encoding.ASCII.GetBytes("hello world\n")]);
        var target = new IPEndPoint(IPAddress.Loopback, 50001);
        var connections = new InMemoryDataConnections().ScriptActiveConnection(curl);

        var connection = await connections.ConnectActiveAsync(ControlRemote, target, Timeout, TestContext.CancellationToken);

        Assert.AreSame(curl, connection);
        CollectionAssert.AreEqual(new[] { new ActiveConnectionRequest(ControlRemote, target, Timeout) }, connections.ActiveRequests.ToArray());
    }

    [TestMethod]
    public async Task ConnectActiveAsync_ScriptedFailure_ThrowsItAndRecordsTheRequest()
    {
        var target = new IPEndPoint(IPAddress.Parse("192.0.2.1"), 50001);
        var connections = new InMemoryDataConnections().ScriptActiveFailure(DataConnectionFailure.Refused);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(async () =>
            await connections.ConnectActiveAsync(ControlRemote, target, Timeout, TestContext.CancellationToken));

        Assert.AreEqual(DataConnectionFailure.Refused, exception.Failure);
        Assert.AreSame(target, connections.ActiveRequests.Single().Target);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_Unscripted_ThrowsUnavailable()
    {
        var connections = new InMemoryDataConnections();

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(async () =>
            await connections.ConnectActiveAsync(ControlRemote, new IPEndPoint(IPAddress.Loopback, 50001), Timeout, TestContext.CancellationToken));

        Assert.AreEqual(DataConnectionFailure.Unavailable, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_Cancelled_ThrowsOperationCanceledAndRecordsNothing()
    {
        var connections = new InMemoryDataConnections().ScriptActiveConnection(new InMemoryConnection([]));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await connections.ConnectActiveAsync(ControlRemote, new IPEndPoint(IPAddress.Loopback, 50001), Timeout, new CancellationToken(canceled: true)));

        Assert.IsEmpty(connections.ActiveRequests);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_NullArguments_Throw()
    {
        var connections = new InMemoryDataConnections();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await connections.ConnectActiveAsync(null!, new IPEndPoint(IPAddress.Loopback, 50001), Timeout, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await connections.ConnectActiveAsync(ControlRemote, null!, Timeout, TestContext.CancellationToken));
    }

    [TestMethod]
    public void ScriptActiveConnection_NullConnection_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new InMemoryDataConnections().ScriptActiveConnection(null!));
    }
}
