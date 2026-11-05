using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
public sealed class SharedPortListenerPairTests
{
    private static readonly ListenUrl PortZero = new("https", "127.0.0.1", 0);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsync_BothBind_StartsTheUdpListenerOnTheTcpListenersBoundPort()
    {
        var starts = new ListenerStarts();

        await using var pair = await StartAsync(PortZero, starts);

        Assert.AreSame(starts.ConnectionListeners.Single(), pair.ConnectionListener);
        Assert.AreSame(starts.DatagramListeners.Single(), pair.DatagramListener);
        Assert.AreEqual(new ListenUrl("https", "127.0.0.1", 40000), starts.DatagramListenUrls.Single());
    }

    [TestMethod]
    public async Task StartAsync_PortZeroAndTheUdpPortTaken_ReleasesTheTcpListenerAndTriesANewPort()
    {
        var starts = new ListenerStarts { UdpPortsTaken = 1 };

        await using var pair = await StartAsync(PortZero, starts);

        Assert.AreEqual(1, starts.ConnectionListeners[0].DisposeCount);
        Assert.AreSame(starts.ConnectionListeners[1], pair.ConnectionListener);
        Assert.AreEqual(new ListenUrl("https", "127.0.0.1", 40001), pair.DatagramListener.ListenUrl);
    }

    [TestMethod]
    public async Task StartAsync_PortZeroAndTheUdpPortTakenEveryAttempt_ThrowsAfterMaxBindAttemptsWithNothingBound()
    {
        var starts = new ListenerStarts { UdpPortsTaken = int.MaxValue };

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(() => StartAsync(PortZero, starts).AsTask());

        Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
        Assert.HasCount(SharedPortListenerPair.MaxBindAttempts, starts.ConnectionListeners);
        Assert.IsTrue(starts.ConnectionListeners.All(listener => listener.DisposeCount == 1));
    }

    [TestMethod]
    public async Task StartAsync_FixedPortAndTheUdpPortTaken_ReleasesTheTcpListenerAndThrowsWithoutRetrying()
    {
        var starts = new ListenerStarts { UdpPortsTaken = 1 };

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => StartAsync(new ListenUrl("https", "127.0.0.1", 8443), starts).AsTask());

        Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
        Assert.AreEqual(1, starts.ConnectionListeners.Single().DisposeCount);
    }

    [TestMethod]
    public async Task StartAsync_PortZeroAndAnotherUdpBindFailure_ReleasesTheTcpListenerAndThrowsWithoutRetrying()
    {
        var starts = new ListenerStarts { UdpPortsTaken = 1, UdpFailure = ListenerBindFailure.PermissionDenied };

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(() => StartAsync(PortZero, starts).AsTask());

        Assert.AreEqual(ListenerBindFailure.PermissionDenied, exception.Failure);
        Assert.AreEqual(1, starts.ConnectionListeners.Single().DisposeCount);
    }

    [TestMethod]
    public async Task StartAsync_UdpStartCancelled_ReleasesTheTcpListenerAndThrows()
    {
        var starts = new ListenerStarts { CancelUdpStart = true };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => StartAsync(PortZero, starts).AsTask());

        Assert.AreEqual(1, starts.ConnectionListeners.Single().DisposeCount);
    }

    [TestMethod]
    public async Task StartAsync_TcpBindFails_ThrowsAndStartsNoUdpListener()
    {
        var starts = new ListenerStarts { TcpFailure = ListenerBindFailure.AddressInUse };

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(() => StartAsync(PortZero, starts).AsTask());

        Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
        Assert.IsEmpty(starts.DatagramListenUrls);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesBothListeners()
    {
        var starts = new ListenerStarts();
        var pair = await StartAsync(PortZero, starts);

        await pair.DisposeAsync();

        Assert.AreEqual(1, starts.ConnectionListeners.Single().DisposeCount);
        Assert.AreEqual(1, starts.DatagramListeners.Single().DisposeCount);
    }

    private ValueTask<SharedPortListenerPair> StartAsync(ListenUrl listenUrl, ListenerStarts starts) =>
        SharedPortListenerPair.StartAsync(
            listenUrl, starts.StartConnectionListenerAsync, starts.StartDatagramListenerAsync, TestContext.CancellationToken);

    /// <summary>
    /// Starts fake listeners: the TCP listener for port 0 gets the next port from 40000 up, and
    /// the first <see cref="UdpPortsTaken"/> UDP starts fail with <see cref="UdpFailure"/>.
    /// </summary>
    private sealed class ListenerStarts
    {
        private int nextPort = 40000;

        public int UdpPortsTaken { get; set; }

        public ListenerBindFailure UdpFailure { get; set; } = ListenerBindFailure.AddressInUse;

        public ListenerBindFailure? TcpFailure { get; set; }

        public bool CancelUdpStart { get; set; }

        public List<FakeConnectionListener> ConnectionListeners { get; } = [];

        public List<FakeDatagramListener> DatagramListeners { get; } = [];

        public List<ListenUrl> DatagramListenUrls { get; } = [];

        public ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
        {
            if (TcpFailure is { } failure)
            {
                throw new ListenerBindException(listenUrl, null, failure, null);
            }

            var listener = new FakeConnectionListener(listenUrl.Port == 0 ? listenUrl.WithBoundPort(nextPort++) : listenUrl);

            ConnectionListeners.Add(listener);

            return ValueTask.FromResult<IConnectionListener>(listener);
        }

        public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
        {
            DatagramListenUrls.Add(listenUrl);

            if (CancelUdpStart)
            {
                throw new OperationCanceledException();
            }

            if (UdpPortsTaken > 0)
            {
                UdpPortsTaken--;
                throw new ListenerBindException(listenUrl, null, UdpFailure, null);
            }

            var listener = new FakeDatagramListener(listenUrl);

            DatagramListeners.Add(listener);

            return ValueTask.FromResult<IDatagramListener>(listener);
        }
    }

    private sealed class FakeConnectionListener(ListenUrl listenUrl) : IConnectionListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, listenUrl.Port)];

        public int DisposeCount { get; private set; }

        public ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeDatagramListener(ListenUrl listenUrl) : IDatagramListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, listenUrl.Port)];

        public int DisposeCount { get; private set; }

        public ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }
}
