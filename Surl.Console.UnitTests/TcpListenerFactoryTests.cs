using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class TcpListenerFactoryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void StartDatagramListenerAsync_Always_ThrowsNotSupportedException()
    {
        var factory = new TcpListenerFactory();

        Assert.ThrowsExactly<NotSupportedException>(
            () => factory.StartDatagramListenerAsync(new ListenUrl("tftp", "127.0.0.1", 0), TestContext.CancellationToken));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task StartConnectionListenerAsync_EphemeralPort_BindsAndAcceptsAConnection()
    {
        var factory = new TcpListenerFactory();

        await using var listener = await factory.StartConnectionListenerAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        using var client = new TcpClient();
        var accepting = listener.AcceptAsync(TestContext.CancellationToken).AsTask();
        await client.ConnectAsync("127.0.0.1", listener.ListenUrl.BoundPort!.Value, TestContext.CancellationToken);
        await using var connection = await accepting;

        Assert.IsNotNull(connection.RemoteEndPoint);
    }
}
