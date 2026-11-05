using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// <see cref="IListenerFactory.StartSharedPortListenersAsync"/>'s default: the connection
/// listener, then the datagram listener on the port it got (ADR-0082, decision 6).
/// </summary>
[TestClass]
public sealed class ListenerFactoryTests
{
    private static readonly ListenUrl Https = new("https", "127.0.0.1", 0);

    [TestMethod]
    public async Task StartSharedPortListenersAsync_ByDefault_StartsTheDatagramListenerOnTheConnectionListenersBoundPort()
    {
        IListenerFactory factory = new UnitTestListenerFactory(null);

        var listeners = await factory.StartSharedPortListenersAsync(Https, CancellationToken.None);

        Assert.AreEqual(Https.WithBoundPort(40000), listeners.ConnectionListener.ListenUrl);
        Assert.AreEqual(new ListenUrl("https", "127.0.0.1", 40000), listeners.DatagramListener.ListenUrl);
    }

    [TestMethod]
    public async Task StartSharedPortListenersAsync_AFixedPort_StartsTheDatagramListenerOnIt()
    {
        IListenerFactory factory = new UnitTestListenerFactory(null) { LeavesBoundPortUnset = true };

        var listeners = await factory.StartSharedPortListenersAsync(Https with { Port = 8443 }, CancellationToken.None);

        Assert.AreEqual(8443, listeners.DatagramListener.ListenUrl.Port);
    }

    [TestMethod]
    public async Task StartSharedPortListenersAsync_DatagramListenerThatCannotBind_DisposesTheConnectionListenerAndThrows()
    {
        var failure = new ListenerBindException(Https, null, ListenerBindFailure.AddressInUse, null);
        var factory = new UnitTestListenerFactory(failure);

        var thrown = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => ((IListenerFactory)factory).StartSharedPortListenersAsync(Https, CancellationToken.None).AsTask());

        Assert.AreSame(failure, thrown);
        Assert.IsTrue(factory.ConnectionListener!.Disposed);
    }

    [TestMethod]
    public async Task StartSharedPortListenersAsync_NullListenUrl_Throws()
    {
        IListenerFactory factory = new UnitTestListenerFactory(null);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => factory.StartSharedPortListenersAsync(null!, CancellationToken.None).AsTask());
    }

    private sealed class UnitTestListenerFactory(Exception? datagramFailure) : IListenerFactory
    {
        public UnitTestConnectionListener? ConnectionListener { get; private set; }

        public bool LeavesBoundPortUnset { get; init; }

        public ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
        {
            ConnectionListener = new UnitTestConnectionListener(LeavesBoundPortUnset ? listenUrl : listenUrl.WithBoundPort(40000));
            return ValueTask.FromResult<IConnectionListener>(ConnectionListener);
        }

        public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            datagramFailure is null
                ? ValueTask.FromResult<IDatagramListener>(new UnitTestDatagramListener(listenUrl))
                : ValueTask.FromException<IDatagramListener>(datagramFailure);
    }

    private sealed class UnitTestConnectionListener(ListenUrl listenUrl) : IConnectionListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints => [];

        public bool Disposed { get; private set; }

        public ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UnitTestDatagramListener(ListenUrl listenUrl) : IDatagramListener
    {
        public ListenUrl ListenUrl { get; } = listenUrl;

        public IReadOnlyList<EndPoint> BoundEndPoints => [];

        public ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
