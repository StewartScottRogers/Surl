using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class DroppingAfterBytesConnectionTests
{
    [TestMethod]
    public async Task BeforeTheDrop_EveryMemberPassesThrough()
    {
        var local = new IPEndPoint(IPAddress.Loopback, 8080);
        var remote = new IPEndPoint(IPAddress.Loopback, 50001);
        var inner = new InMemoryConnection(["hi"u8.ToArray()], localEndPoint: local, remoteEndPoint: remote);
        var drops = 0;
        var connection = new DroppingAfterBytesConnection(inner, 10, () => drops++);
        var buffer = new byte[8];

        var peeked = await connection.PeekByteAsync(TimeSpan.Zero, CancellationToken.None);
        var read = await connection.ReadAsync(buffer, CancellationToken.None);
        await connection.WriteAsync("abc"u8.ToArray(), CancellationToken.None);
        var session = await connection.UpgradeToTlsAsync(CancellationToken.None);
        await connection.CompleteWritesAsync(CancellationToken.None);
        connection.Abort();
        await connection.DisposeAsync();

        Assert.AreEqual((local, remote), (connection.LocalEndPoint, connection.RemoteEndPoint));
        Assert.AreEqual((byte)'h', peeked);
        Assert.AreEqual(2, read);
        Assert.AreEqual("abc", Encoding.ASCII.GetString(inner.WrittenBytes));
        Assert.AreSame(session, connection.TlsSession);
        Assert.IsTrue(inner.WritesCompleted);
        Assert.IsTrue(inner.Aborted);
        Assert.IsTrue(inner.Disposed);
        Assert.AreEqual(0, drops);
    }

    [TestMethod]
    public async Task WriteAsync_ReachingTheCountExactly_SendsItAll_ThenDrops()
    {
        var inner = new InMemoryConnection([]);
        var drops = 0;
        var connection = new DroppingAfterBytesConnection(inner, 4, () => drops++);

        await connection.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        await connection.WriteAsync("cd"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("abcd", Encoding.ASCII.GetString(inner.WrittenBytes));
        Assert.IsTrue(inner.Aborted);
        Assert.AreEqual(1, drops);
    }

    [TestMethod]
    public async Task WriteAsync_CrossingTheCount_SendsUpToIt_DropsAndThrows()
    {
        var inner = new InMemoryConnection([]);
        var drops = 0;
        var connection = new DroppingAfterBytesConnection(inner, 3, () => drops++);

        var exception = await Assert.ThrowsExactlyAsync<IOException>(
            async () => await connection.WriteAsync("abcdef"u8.ToArray(), CancellationToken.None));

        Assert.AreEqual("abc", Encoding.ASCII.GetString(inner.WrittenBytes));
        Assert.IsTrue(inner.Aborted);
        Assert.AreEqual(1, drops);
        StringAssert.Contains(exception.Message, "after 3 bytes");
    }

    [TestMethod]
    public async Task AfterTheDrop_ReadsPeeksWritesAndHalfClosesThrow()
    {
        var inner = new InMemoryConnection(["hi"u8.ToArray()]);
        var drops = 0;
        var connection = new DroppingAfterBytesConnection(inner, 1, () => drops++);
        await connection.WriteAsync("a"u8.ToArray(), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.ReadAsync(new byte[4], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.PeekByteAsync(TimeSpan.Zero, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.WriteAsync("b"u8.ToArray(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.CompleteWritesAsync(CancellationToken.None));

        Assert.AreEqual("a", Encoding.ASCII.GetString(inner.WrittenBytes));
        Assert.AreEqual(1, drops);
    }
}
