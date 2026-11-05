using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// The rules of <see cref="SocketOriginConnector"/> (ADR-0088, decision 2) over
/// <see cref="FakeOriginSockets"/>, with the clock on a <see cref="ManualTimeProvider"/>, so no
/// name is resolved and no socket opened. <see cref="SocketOriginConnectorLoopbackTests"/> repeats
/// the main cases over real loopback sockets.
/// </summary>
[TestClass]
public sealed class SocketOriginConnectorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly IPEndPoint ProxyEnd = new(IPAddress.Loopback, 3128);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_NameResolvesToSeveralAddresses_TriesThemInOrderUntilOneAccepts()
    {
        var sockets = new FakeOriginSockets
        {
            Resolve = Resolves("::1", "127.0.0.1"),
            Connect = (target, _) => target.AddressFamily == AddressFamily.InterNetworkV6
                ? throw new SocketException((int)SocketError.ConnectionRefused)
                : Task.FromResult(InMemoryDataTransport.Create(ProxyEnd, target).Transport),
        };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), sockets);

        await using var connection = await connector.ConnectAsync("localhost", 8080, Timeout, TestContext.CancellationToken);

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 8080), connection.RemoteEndPoint);
        CollectionAssert.AreEqual(
            new[] { new IPEndPoint(IPAddress.IPv6Loopback, 8080), new IPEndPoint(IPAddress.Loopback, 8080) },
            sockets.ConnectTargets.ToArray());
        Assert.IsNull(connection.TlsSession);
        connection.Abort();
    }

    [TestMethod]
    public async Task ConnectAsync_NameResolvesToAForbiddenAndAnAllowedAddress_ConnectsOnlyToTheAllowedOne()
    {
        var sockets = new FakeOriginSockets
        {
            Resolve = Resolves("192.0.2.1", "::ffff:127.0.0.1", "127.0.0.1"),
            Connect = (target, _) => Task.FromResult(InMemoryDataTransport.Create(ProxyEnd, target).Transport),
        };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), sockets);

        await using var connection = await connector.ConnectAsync("rebound.example", 80, Timeout, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Loopback, 80) }, sockets.ConnectTargets.ToArray());
        connection.Abort();
    }

    [TestMethod]
    public async Task ConnectAsync_NameResolvesOnlyToForbiddenAddresses_IsNotAllowedWithoutConnecting()
    {
        var sockets = new FakeOriginSockets { Resolve = Resolves("192.0.2.1", "2001:db8::1") };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("rebound.example", 80, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.NotAllowed, exception.Failure);
        StringAssert.Contains(exception.Message, "192.0.2.1, 2001:db8::1");
        Assert.IsEmpty(sockets.ConnectTargets);
    }

    [TestMethod]
    public async Task ConnectAsync_TheProxysOwnEndPoint_IsNotAllowed()
    {
        var sockets = new FakeOriginSockets { Resolve = Resolves("127.0.0.1") };
        var connector = new SocketOriginConnector(
            AllowedOriginRule.LoopbackOnly.WithProxyEndPoint(ProxyEnd), new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("127.0.0.1", ProxyEnd.Port, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.NotAllowed, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_ResolutionFails_IsHostNotFound()
    {
        var sockets = new FakeOriginSockets { Resolve = (_, _) => throw new SocketException((int)SocketError.HostNotFound) };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("nowhere.invalid", 80, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.HostNotFound, exception.Failure);
        StringAssert.Contains(exception.Message, "nowhere.invalid");
    }

    [TestMethod]
    public async Task ConnectAsync_ResolutionFindsNoAddress_IsHostNotFound()
    {
        var sockets = new FakeOriginSockets { Resolve = Resolves() };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("empty.example", 80, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.HostNotFound, exception.Failure);
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionRefused, OriginConnectionFailure.Refused)]
    [DataRow(SocketError.HostUnreachable, OriginConnectionFailure.Unreachable)]
    [DataRow(SocketError.AddressFamilyNotSupported, OriginConnectionFailure.Unreachable)]
    public async Task ConnectAsync_EveryAddressFails_ReportsTheLastFailure(SocketError error, OriginConnectionFailure expected)
    {
        var sockets = new FakeOriginSockets
        {
            Resolve = Resolves("::1", "127.0.0.1"),
            Connect = (target, _) => throw new SocketException(
                (int)(target.AddressFamily == AddressFamily.InterNetwork ? error : SocketError.NetworkUnreachable)),
        };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), sockets);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("localhost", 80, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(expected, exception.Failure);
        Assert.HasCount(2, sockets.ConnectTargets);
    }

    [TestMethod]
    public async Task ConnectAsync_OriginDoesNotAcceptInTime_IsTimedOut()
    {
        var time = new ManualTimeProvider();
        var sockets = new FakeOriginSockets
        {
            Resolve = Resolves("127.0.0.1"),
            Connect = async (_, cancellationToken) =>
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            },
        };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, time, sockets);

        var connect = connector.ConnectAsync("127.0.0.1", 80, Timeout, TestContext.CancellationToken).AsTask();
        await time.FirstTimerCreated;
        time.Advance(Timeout);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(() => connect);
        Assert.AreEqual(OriginConnectionFailure.TimedOut, exception.Failure);
        StringAssert.Contains(exception.Message, "127.0.0.1:80");
    }

    [TestMethod]
    public async Task ConnectAsync_CallerCancels_ThrowsOperationCanceled()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var sockets = new FakeOriginSockets
        {
            Resolve = async (_, cancellationToken) =>
            {
                await cancellation.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
                return [];
            },
        };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), sockets);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => connector.ConnectAsync("localhost", 80, Timeout, cancellation.Token).AsTask());
    }

    [TestMethod]
    public async Task ConnectAsync_TimeoutAndCallerCancelTogether_ThrowsOperationCanceled()
    {
        var time = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var sockets = new FakeOriginSockets
        {
            Resolve = async (_, cancellationToken) =>
            {
                time.Advance(Timeout);
                await cancellation.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
                return [];
            },
        };
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, time, sockets);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => connector.ConnectAsync("localhost", 80, Timeout, cancellation.Token).AsTask());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(65536)]
    public async Task ConnectAsync_PortOutOfRange_Throws(int port)
    {
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), new FakeOriginSockets());

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => connector.ConnectAsync("localhost", port, Timeout, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ConnectAsync_EmptyHost_Throws()
    {
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly, new ManualTimeProvider(), new FakeOriginSockets());

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => connector.ConnectAsync("", 80, Timeout, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public void Constructor_NullRule_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocketOriginConnector(null!));
    }

    private static Func<string, CancellationToken, Task<IPAddress[]>> Resolves(params string[] addresses) =>
        (_, _) => Task.FromResult(addresses.Select(IPAddress.Parse).ToArray());
}
