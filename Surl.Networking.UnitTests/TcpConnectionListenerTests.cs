using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
[TestCategory("Integration")]
public sealed class TcpConnectionListenerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsync_IPv4LoopbackPortZero_ReportsTheBoundPort()
    {
        var listenUrl = new ListenUrl("http", "127.0.0.1", 0);

        await using var listener = await TcpConnectionListener.StartAsync(listenUrl, TestContext.CancellationToken);

        var bound = (IPEndPoint)listener.BoundEndPoints.Single();
        Assert.AreEqual(IPAddress.Loopback, bound.Address);
        Assert.AreNotEqual(0, bound.Port);
        Assert.AreEqual(listenUrl.WithBoundPort(bound.Port), listener.ListenUrl);
    }

    [TestMethod]
    public async Task AcceptAsync_IPv4Loopback_ExchangesBytesBothWaysAndHalfCloses()
    {
        await using var listener = await TcpConnectionListener.StartAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);

        await ExchangeBytesBothWaysAsync(listener, IPAddress.Loopback);
    }

    [TestMethod]
    public async Task AcceptAsync_IPv6Loopback_ExchangesBytesBothWaysAndHalfCloses()
    {
        var listener = await TryStartOnIPv6LoopbackAsync();

        await using (listener)
        {
            await ExchangeBytesBothWaysAsync(listener, IPAddress.IPv6Loopback);
        }
    }

    [TestMethod]
    public async Task StartAsync_HostName_BindsEveryAddressItResolvesToOnOnePort()
    {
        var resolved = await Dns.GetHostAddressesAsync("localhost", TestContext.CancellationToken);
        TcpConnectionListener listener;
        try
        {
            listener = await TcpConnectionListener.StartAsync(new ListenUrl("http", "localhost", 0), TestContext.CancellationToken);
        }
        catch (ListenerBindException exception) when (exception.Failure == ListenerBindFailure.AddressNotAvailable)
        {
            Assert.Inconclusive($"localhost resolves to {exception.EndPoint}, which this machine cannot bind.");
            throw;
        }

        await using var started = listener;

        var bound = listener.BoundEndPoints.Cast<IPEndPoint>().ToArray();
        CollectionAssert.AreEquivalent(resolved.Distinct().ToArray(), bound.Select(endPoint => endPoint.Address).ToArray());
        Assert.IsTrue(bound.All(endPoint => endPoint.Port == listener.ListenUrl.BoundPort));
    }

    [TestMethod]
    public async Task CompleteWritesAsync_ServerHalfClosesFirst_StillReadsWhatTheClientSendsAfter()
    {
        await using var listener = await TcpConnectionListener.StartAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        var accepting = listener.AcceptAsync(TestContext.CancellationToken).AsTask();
        await client.ConnectAsync(IPAddress.Loopback, listener.ListenUrl.BoundPort!.Value, TestContext.CancellationToken);
        await using var connection = await accepting;
        var clientStream = client.GetStream();

        await connection.WriteAsync(Encoding.ASCII.GetBytes("reply"), TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        var clientRead = new byte[16];
        var clientCount = await clientStream.ReadAtLeastAsync(clientRead, clientRead.Length, throwOnEndOfStream: false, TestContext.CancellationToken);
        await clientStream.WriteAsync(Encoding.ASCII.GetBytes("after"), TestContext.CancellationToken);
        client.Client.Shutdown(SocketShutdown.Send);
        var serverRead = await ReadToEndAsync(connection);
        var readAfterEnd = await connection.ReadAsync(new byte[16], TestContext.CancellationToken);

        Assert.AreEqual("reply", Encoding.ASCII.GetString(clientRead, 0, clientCount));
        Assert.AreEqual("after", serverRead);
        Assert.AreEqual(0, readAfterEnd);
    }

    [TestMethod]
    public async Task StartAsync_PortAnotherListenerHolds_ThrowsAddressInUse()
    {
        await using var holder = await TcpConnectionListener.StartAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        var port = holder.ListenUrl.BoundPort!.Value;

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => TcpConnectionListener.StartAsync(new ListenUrl("http", "127.0.0.1", port), TestContext.CancellationToken).AsTask());

        Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, port), exception.EndPoint);
        Assert.IsInstanceOfType<SocketException>(exception.InnerException);
    }

    [TestMethod]
    public async Task StartAsync_HostThatDoesNotResolve_ThrowsHostNotFound()
    {
        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => TcpConnectionListener.StartAsync(new ListenUrl("http", "no-such-host.invalid", 0), TestContext.CancellationToken).AsTask());

        Assert.AreEqual(ListenerBindFailure.HostNotFound, exception.Failure);
        Assert.IsNull(exception.EndPoint);
    }

    [TestMethod]
    public async Task AcceptAsync_Cancelled_StopsWaitingAndTheNextAcceptStillWorks()
    {
        await using var listener = await TcpConnectionListener.StartAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        using var source = new CancellationTokenSource();

        var waiting = listener.AcceptAsync(source.Token).AsTask();
        await source.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => waiting);
        await ExchangeBytesBothWaysAsync(listener, IPAddress.Loopback);
    }

    [TestMethod]
    public async Task AcceptAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var listener = await TcpConnectionListener.StartAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);

        await listener.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => listener.AcceptAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task DisposeAsync_StopsListening()
    {
        var listener = await TcpConnectionListener.StartAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        var port = listener.ListenUrl.BoundPort!.Value;

        await listener.DisposeAsync();

        using var client = new TcpClient(AddressFamily.InterNetwork);
        await Assert.ThrowsExactlyAsync<SocketException>(
            () => client.ConnectAsync(IPAddress.Loopback, port, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task Abort_ClientSeesTheConnectionReset()
    {
        await using var listener = await TcpConnectionListener.StartAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        var accepting = listener.AcceptAsync(TestContext.CancellationToken).AsTask();
        await client.ConnectAsync(IPAddress.Loopback, listener.ListenUrl.BoundPort!.Value, TestContext.CancellationToken);
        await using var connection = await accepting;

        connection.Abort();

        await Assert.ThrowsAsync<IOException>(
            () => client.GetStream().ReadAsync(new byte[16], TestContext.CancellationToken).AsTask());
    }

    private async Task<TcpConnectionListener> TryStartOnIPv6LoopbackAsync()
    {
        if (!Socket.OSSupportsIPv6)
        {
            Assert.Inconclusive("This machine has no IPv6.");
        }

        try
        {
            return await TcpConnectionListener.StartAsync(new ListenUrl("http", "::1", 0), TestContext.CancellationToken);
        }
        catch (ListenerBindException exception) when (exception.Failure == ListenerBindFailure.AddressNotAvailable)
        {
            Assert.Inconclusive("This machine has no IPv6 loopback address.");
            throw;
        }
    }

    private async Task ExchangeBytesBothWaysAsync(TcpConnectionListener listener, IPAddress address)
    {
        using var client = new TcpClient(address.AddressFamily);
        var accepting = listener.AcceptAsync(TestContext.CancellationToken).AsTask();
        await client.ConnectAsync(address, listener.ListenUrl.BoundPort!.Value, TestContext.CancellationToken);
        await using var connection = await accepting;
        var clientStream = client.GetStream();
        var clientLocal = client.Client.LocalEndPoint;
        var clientRemote = client.Client.RemoteEndPoint;

        await clientStream.WriteAsync(Encoding.ASCII.GetBytes("ping"), TestContext.CancellationToken);
        client.Client.Shutdown(SocketShutdown.Send);
        var received = await ReadToEndAsync(connection);
        await connection.WriteAsync(Encoding.ASCII.GetBytes("pong"), TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        var answered = await ReadToEndAsync(clientStream);

        Assert.AreEqual("ping", received);
        Assert.AreEqual("pong", answered);
        Assert.AreEqual(clientLocal, connection.RemoteEndPoint);
        Assert.AreEqual(clientRemote, connection.LocalEndPoint);
    }

    private async Task<string> ReadToEndAsync(IConnection connection)
    {
        var buffer = new byte[64];
        var text = new StringBuilder();
        for (var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);
            count > 0;
            count = await connection.ReadAsync(buffer, TestContext.CancellationToken))
        {
            text.Append(Encoding.ASCII.GetString(buffer, 0, count));
        }

        return text.ToString();
    }

    private async Task<string> ReadToEndAsync(NetworkStream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII);

        return await reader.ReadToEndAsync(TestContext.CancellationToken);
    }
}
