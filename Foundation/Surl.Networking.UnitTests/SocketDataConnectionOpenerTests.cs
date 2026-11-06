using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// The rules of <see cref="SocketDataConnectionOpener"/> and <see cref="SocketPassiveDataListener"/>
/// (ADR-0052, decisions 5, 6 and 9) over <see cref="FakeDataConnectionSockets"/>, with the clock on
/// a <see cref="ManualTimeProvider"/>, so no socket is opened.
/// </summary>
[TestClass]
public sealed class SocketDataConnectionOpenerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly IPEndPoint ControlLocal = new(IPAddress.Parse("192.0.2.1"), 21);
    private static readonly IPEndPoint ControlRemote = new(IPAddress.Parse("192.0.2.7"), 50000);
    private static readonly IPEndPoint PeerDataEnd = new(IPAddress.Parse("192.0.2.7"), 50001);
    private static readonly IPEndPoint StrangerDataEnd = new(IPAddress.Parse("198.51.100.9"), 50001);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartPassiveListenerAsync_BindsTheControlConnectionsLocalAddress()
    {
        var sockets = new FakeDataConnectionSockets();
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);

        await using var listener = await opener.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { ControlLocal.Address }, sockets.ListenedAddresses.ToArray());
        Assert.AreEqual(sockets.LastSocket!.LocalEndPoint, listener.LocalEndPoint);
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_Ipv4MappedControlAddress_BindsIpv4()
    {
        var sockets = new FakeDataConnectionSockets();
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);

        await using var listener = await opener.StartPassiveListenerAsync(
            new IPEndPoint(IPAddress.Parse("::ffff:192.0.2.1"), 21), ControlRemote, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.1") }, sockets.ListenedAddresses.ToArray());
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_BindFails_IsUnreachable()
    {
        var sockets = new FakeDataConnectionSockets { ListenFailure = new SocketException((int)SocketError.AddressNotAvailable) };
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(
            () => opener.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(DataConnectionFailure.Unreachable, exception.Failure);
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_NonIpControlEndPoint_IsUnreachableWithoutBinding()
    {
        var sockets = new FakeDataConnectionSockets();
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(
            () => opener.StartPassiveListenerAsync(new DnsEndPoint("localhost", 21), ControlRemote, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(DataConnectionFailure.Unreachable, exception.Failure);
        Assert.IsEmpty(sockets.ListenedAddresses);
    }

    [TestMethod]
    public async Task StartPassiveListenerAsync_Cancelled_Throws()
    {
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), new FakeDataConnectionSockets());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => opener.StartPassiveListenerAsync(ControlLocal, ControlRemote, new CancellationToken(canceled: true)).AsTask());
    }

    [TestMethod]
    public void StartPassiveListenerAsync_NullEndPoints_Throw()
    {
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), new FakeDataConnectionSockets());

        Assert.ThrowsExactly<ArgumentNullException>(() => opener.StartPassiveListenerAsync(null!, ControlRemote, TestContext.CancellationToken));
        Assert.ThrowsExactly<ArgumentNullException>(() => opener.StartPassiveListenerAsync(ControlLocal, null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AcceptAsync_ConnectionFromThePeer_IsHandedOutAndCarriesBytesBothWays()
    {
        var (sockets, listener) = await StartPassiveAsync();
        var peer = InMemoryDataTransport.Create(sockets.LastSocket!.LocalEndPoint, PeerDataEnd);
        sockets.LastSocket.Arrive(peer.Transport);

        var connection = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        Assert.AreEqual(PeerDataEnd, connection.RemoteEndPoint);
        Assert.AreEqual(sockets.LastSocket.LocalEndPoint, connection.LocalEndPoint);
        await AssertCarriesBytesBothWaysAsync(connection, peer.Client);
        Assert.AreEqual(1, sockets.LastSocket.CloseCount, "The listener stops listening once its connection is taken.");
        await AbortAsync(connection);
        await listener.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptAsync_MappedPeerAddress_IsThePeer()
    {
        var sockets = new FakeDataConnectionSockets();
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);
        await using var listener = await opener.StartPassiveListenerAsync(
            ControlLocal, new IPEndPoint(IPAddress.Parse("::ffff:192.0.2.7"), 50000), TestContext.CancellationToken);
        sockets.LastSocket!.Arrive(InMemoryDataTransport.Create(ControlLocal, PeerDataEnd).Transport);

        var connection = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        Assert.AreEqual(PeerDataEnd, connection.RemoteEndPoint);
        await AbortAsync(connection);
    }

    [TestMethod]
    public async Task AcceptAsync_ConnectionFromAnotherAddress_IsResetAndTheListenerWaitsUntilTheTimeout()
    {
        var time = new ManualTimeProvider();
        var (sockets, listener) = await StartPassiveAsync(time);
        var stranger = InMemoryDataTransport.Create(ControlLocal, StrangerDataEnd);
        sockets.LastSocket!.Arrive(stranger.Transport);

        var accept = listener.AcceptAsync(Timeout, TestContext.CancellationToken).AsTask();
        await sockets.LastSocket.AcceptsStarted(2);

        Assert.AreEqual(1, stranger.Control.ResetAndCloseCount);
        Assert.IsFalse(accept.IsCompleted, "The listener goes on waiting after a stranger's connection.");
        time.Advance(Timeout);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(() => accept);
        Assert.AreEqual(DataConnectionFailure.TimedOut, exception.Failure);
        await listener.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptAsync_StrangerThenPeer_HandsOutThePeer()
    {
        var (sockets, listener) = await StartPassiveAsync();
        sockets.LastSocket!.Arrive(InMemoryDataTransport.Create(ControlLocal, StrangerDataEnd).Transport);
        sockets.LastSocket.Arrive(InMemoryDataTransport.Create(ControlLocal, PeerDataEnd).Transport);

        var connection = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        Assert.AreEqual(PeerDataEnd, connection.RemoteEndPoint);
        await AbortAsync(connection);
        await listener.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptAsync_NothingArrives_TimesOutAtTheTimeout()
    {
        var time = new ManualTimeProvider();
        var (_, listener) = await StartPassiveAsync(time);

        var accept = listener.AcceptAsync(Timeout, TestContext.CancellationToken).AsTask();
        await time.FirstTimerCreated;
        time.Advance(Timeout - TimeSpan.FromSeconds(1));
        Assert.IsFalse(accept.IsCompleted);
        time.Advance(TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(() => accept);
        Assert.AreEqual(DataConnectionFailure.TimedOut, exception.Failure);
        await listener.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptAsync_CallerCancels_ThrowsOperationCanceled()
    {
        var (_, listener) = await StartPassiveAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var accept = listener.AcceptAsync(Timeout, cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => accept);
        await listener.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptAsync_OneClientsAcceptFailure_IsAbsorbed()
    {
        var (sockets, listener) = await StartPassiveAsync();
        sockets.LastSocket!.Fail(new SocketException((int)SocketError.ConnectionReset));
        sockets.LastSocket.Arrive(InMemoryDataTransport.Create(ControlLocal, PeerDataEnd).Transport);

        var connection = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        Assert.AreEqual(PeerDataEnd, connection.RemoteEndPoint);
        await AbortAsync(connection);
        await listener.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptAsync_ListeningSocketFails_IsUnreachable()
    {
        var (sockets, listener) = await StartPassiveAsync();
        sockets.LastSocket!.Fail(new SocketException((int)SocketError.InvalidArgument));

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(
            () => listener.AcceptAsync(Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(DataConnectionFailure.Unreachable, exception.Failure);
        await listener.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptAsync_AfterTheConnectionWasHandedOut_Throws()
    {
        var (sockets, listener) = await StartPassiveAsync();
        sockets.LastSocket!.Arrive(InMemoryDataTransport.Create(ControlLocal, PeerDataEnd).Transport);
        var connection = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => listener.AcceptAsync(Timeout, TestContext.CancellationToken).AsTask());

        await AbortAsync(connection);
        await listener.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptAsync_AfterDispose_Throws()
    {
        var (sockets, listener) = await StartPassiveAsync();

        await listener.DisposeAsync();
        await listener.DisposeAsync();

        Assert.AreEqual(2, sockets.LastSocket!.CloseCount);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => listener.AcceptAsync(Timeout, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task DisposeAsync_ConnectionAcceptedAsTheListenerStops_IsReset()
    {
        var sockets = new FakeDataConnectionSockets { AcceptsIgnoreCancellation = true };
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);
        var listener = await opener.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);
        using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken))
        {
            var accept = listener.AcceptAsync(Timeout, cancellation.Token).AsTask();
            await cancellation.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => accept);
        }

        var late = InMemoryDataTransport.Create(ControlLocal, PeerDataEnd);
        var disposing = listener.DisposeAsync().AsTask();
        sockets.LastSocket!.Arrive(late.Transport);
        await disposing;

        Assert.AreEqual(1, late.Control.ResetAndCloseCount);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_ThePeersAddress_ConnectsAndCarriesBytesBothWays()
    {
        var target = new IPEndPoint(ControlRemote.Address, 50002);
        var transport = InMemoryDataTransport.Create(new IPEndPoint(ControlLocal.Address, 60000), target);
        var sockets = new FakeDataConnectionSockets { Connect = (_, _) => Task.FromResult(transport.Transport) };
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);

        var connection = await opener.ConnectActiveAsync(ControlRemote, target, Timeout, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { target }, sockets.ConnectTargets.ToArray());
        Assert.AreEqual(target, connection.RemoteEndPoint);
        await AssertCarriesBytesBothWaysAsync(connection, transport.Client);
        await AbortAsync(connection);
    }

    [TestMethod]
    [DataRow("198.51.100.9", 50002)]
    [DataRow("192.0.2.7", 1023)]
    public async Task ConnectActiveAsync_AnotherAddressOrALowPort_IsRefusedWithoutConnecting(string address, int port)
    {
        var sockets = new FakeDataConnectionSockets();
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(
            () => opener.ConnectActiveAsync(ControlRemote, new IPEndPoint(IPAddress.Parse(address), port), Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(DataConnectionFailure.Refused, exception.Failure);
        Assert.IsEmpty(sockets.ConnectTargets);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_NotOpenWithinTheTimeout_TimesOut()
    {
        var time = new ManualTimeProvider();
        var sockets = new FakeDataConnectionSockets { Connect = WaitForCancellationAsync };
        var opener = new SocketDataConnectionOpener(null, time, sockets);

        var connect = opener.ConnectActiveAsync(ControlRemote, PeerDataEnd, Timeout, TestContext.CancellationToken).AsTask();
        await time.FirstTimerCreated;
        time.Advance(Timeout);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(() => connect);
        Assert.AreEqual(DataConnectionFailure.TimedOut, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_CallerCancels_ThrowsOperationCanceled()
    {
        var sockets = new FakeDataConnectionSockets { Connect = WaitForCancellationAsync };
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var connect = opener.ConnectActiveAsync(ControlRemote, PeerDataEnd, Timeout, cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => connect);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_ConnectFails_IsUnreachable()
    {
        var sockets = new FakeDataConnectionSockets
        {
            Connect = (_, _) => Task.FromException<DataTransport>(new SocketException((int)SocketError.ConnectionRefused)),
        };
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(
            () => opener.ConnectActiveAsync(ControlRemote, PeerDataEnd, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(DataConnectionFailure.Unreachable, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectActiveAsync_NullArguments_Throw()
    {
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), new FakeDataConnectionSockets());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => opener.ConnectActiveAsync(null!, PeerDataEnd, Timeout, TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => opener.ConnectActiveAsync(ControlRemote, null!, Timeout, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PublicConstructor_OpensNoSocketUntilAsked(bool withTimeProvider)
    {
        var opener = new SocketDataConnectionOpener(null, withTimeProvider ? new ManualTimeProvider() : null);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(
            () => opener.ConnectActiveAsync(ControlRemote, StrangerDataEnd, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(DataConnectionFailure.Refused, exception.Failure);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_PassiveDataConnection_UsesTheServersTlsSettingsWithNoAlpn()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));
        var sockets = new FakeDataConnectionSockets();
        var opener = new SocketDataConnectionOpener(settings, new ManualTimeProvider(), sockets);
        await using var listener = await opener.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken);
        var peer = InMemoryDataTransport.Create(ControlLocal, PeerDataEnd);
        sockets.LastSocket!.Arrive(peer.Transport);
        var connection = await listener.AcceptAsync(Timeout, TestContext.CancellationToken);

        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        await using var client = new SslStream(peer.Client, leaveInnerStreamOpen: true);
        await client.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions
            {
                TargetHost = "localhost",
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                ApplicationProtocols = [new SslApplicationProtocol("ftp")],
            },
            TestContext.CancellationToken);
        var session = await upgrade;

        Assert.AreSame(session, connection.TlsSession);
        Assert.IsNull(session.ApplicationProtocol);
        Assert.AreEqual(certificate.Thumbprint, client.RemoteCertificate!.GetCertHashString());
        await AssertCarriesBytesBothWaysAsync(connection, client);
        await AbortAsync(connection);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_NoTlsSettings_Throws()
    {
        var target = new IPEndPoint(ControlRemote.Address, 50002);
        var transport = InMemoryDataTransport.Create(ControlLocal, target);
        var sockets = new FakeDataConnectionSockets { Connect = (_, _) => Task.FromResult(transport.Transport) };
        var opener = new SocketDataConnectionOpener(null, new ManualTimeProvider(), sockets);
        var connection = await opener.ConnectActiveAsync(ControlRemote, target, Timeout, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        await AbortAsync(connection);
    }

    private async Task<(FakeDataConnectionSockets Sockets, IPassiveDataListener Listener)> StartPassiveAsync(
        ManualTimeProvider? time = null)
    {
        var sockets = new FakeDataConnectionSockets();
        var opener = new SocketDataConnectionOpener(null, time ?? new ManualTimeProvider(), sockets);

        return (sockets, await opener.StartPassiveListenerAsync(ControlLocal, ControlRemote, TestContext.CancellationToken));
    }

    private async Task AssertCarriesBytesBothWaysAsync(IConnection connection, Stream client)
    {
        await connection.WriteAsync(Encoding.ASCII.GetBytes("listing\r\n"), TestContext.CancellationToken);
        var toClient = new byte[64];
        var toClientCount = await client.ReadAsync(toClient, TestContext.CancellationToken);
        Assert.AreEqual("listing\r\n", Encoding.ASCII.GetString(toClient, 0, toClientCount));

        await client.WriteAsync(Encoding.ASCII.GetBytes("upload"), TestContext.CancellationToken);
        await client.FlushAsync(TestContext.CancellationToken);
        var toServer = new byte[64];
        var toServerCount = await connection.ReadAsync(toServer, TestContext.CancellationToken);
        Assert.AreEqual("upload", Encoding.ASCII.GetString(toServer, 0, toServerCount));
    }

    private static async Task AbortAsync(IConnection connection)
    {
        connection.Abort();
        await connection.DisposeAsync();
    }

    private static async Task<DataTransport> WaitForCancellationAsync(IPEndPoint target, CancellationToken cancellationToken)
    {
        await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);

        throw new InvalidOperationException($"The connect to {target} was never cancelled.");
    }
}
