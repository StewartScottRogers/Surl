using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// A <see cref="SocketListenerFactory"/> hands its <see cref="TimeProvider"/> to every connection its
/// TCP and Unix socket listeners accept (ADR-0124, decision 6; BL-848): a connection's peek wait ends
/// when the fake clock is advanced, never by wall time. Fast tests on loopback and a socket file.
/// </summary>
[TestClass]
public sealed class SocketListenerFactoryTimeProviderTests
{
    // Far longer than any test runs, so only the manual clock can end a peek.
    private static readonly TimeSpan Wait = TimeSpan.FromHours(1);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartConnectionListenerAsync_TcpListenUrl_AcceptedConnectionsPeekOnTheFactorysClock()
    {
        var time = new ManualTimeProvider();
        var factory = new SocketListenerFactory(null, time);
        await using var listener = await factory.StartConnectionListenerAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(listener.BoundEndPoints[0], TestContext.CancellationToken);

        await AssertPeekEndsWhenTheClockAdvancesAsync(listener, time);
    }

    [TestMethod]
    public async Task StartConnectionListenerAsync_UnixListenUrl_AcceptedConnectionsPeekOnTheFactorysClock()
    {
        var path = Path.Combine(Path.GetTempPath(), "surl-" + Guid.NewGuid().ToString("N")[..8] + ".sock");
        var time = new ManualTimeProvider();
        var factory = new SocketListenerFactory(null, time);
        var listenUrl = new ListenUrl("http", "localhost", 80) { UnixSocket = new UnixSocketAddress(path, false) };
        await using var listener = await factory.StartConnectionListenerAsync(listenUrl, TestContext.CancellationToken);
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(new UnixDomainSocketEndPoint(path), TestContext.CancellationToken);

        await AssertPeekEndsWhenTheClockAdvancesAsync(listener, time);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_UsesTheSystemClock()
    {
        Assert.IsInstanceOfType<IListenerFactory>(new SocketListenerFactory(null, null));
    }

    private async Task AssertPeekEndsWhenTheClockAdvancesAsync(IConnectionListener listener, ManualTimeProvider time)
    {
        var connection = (StreamConnection)await listener.AcceptAsync(TestContext.CancellationToken);

        var peek = connection.PeekByteAsync(Wait, TestContext.CancellationToken).AsTask();
        await time.FirstTimerCreated;
        Assert.IsFalse(peek.IsCompleted);
        time.Advance(Wait);

        Assert.IsNull(await peek);

        // The manual clock never ends a lingering close, so close with a reset instead.
        connection.Abort();
        await connection.DisposeAsync();
    }
}
