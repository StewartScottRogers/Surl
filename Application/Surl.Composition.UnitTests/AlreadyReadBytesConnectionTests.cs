using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class AlreadyReadBytesConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_ReturnsTheBytesAlreadyReadFirstThenReadsTheConnection()
    {
        var inner = new InMemoryConnection([System.Text.Encoding.ASCII.GetBytes("cd")]);
        var connection = new AlreadyReadBytesConnection(inner, System.Text.Encoding.ASCII.GetBytes("ab"));
        var buffer = new byte[1];

        var first = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        var firstByte = buffer[0];
        var rest = new byte[8];
        var second = await connection.ReadAsync(rest, TestContext.CancellationToken);
        var secondByte = rest[0];
        var third = await connection.ReadAsync(rest, TestContext.CancellationToken);

        Assert.AreEqual(1, first);
        Assert.AreEqual((byte)'a', firstByte);
        Assert.AreEqual(1, second);
        Assert.AreEqual((byte)'b', secondByte);
        Assert.AreEqual(2, third);
        Assert.AreEqual((byte)'c', rest[0]);
    }

    [TestMethod]
    public async Task ReadAsync_CancelledWithBytesLeft_Throws()
    {
        var connection = new AlreadyReadBytesConnection(new InMemoryConnection([]), System.Text.Encoding.ASCII.GetBytes("ab"));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await connection.ReadAsync(new byte[1], new CancellationToken(true)));
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_GoesToTheConnection()
    {
        var connection = new AlreadyReadBytesConnection(new InMemoryConnection([]), System.Text.Encoding.ASCII.GetBytes("ab"));

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await connection.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task EveryOtherMember_GoesToTheConnection()
    {
        var local = new IPEndPoint(IPAddress.Loopback, 1);
        var remote = new IPEndPoint(IPAddress.Loopback, 2);
        var inner = new InMemoryConnection([], local, remote);
        var connection = new AlreadyReadBytesConnection(inner, ReadOnlyMemory<byte>.Empty);

        await connection.WriteAsync(System.Text.Encoding.ASCII.GetBytes("x"), TestContext.CancellationToken);
        var tls = await connection.UpgradeToTlsAsync(TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        connection.Abort();
        await connection.DisposeAsync();

        Assert.AreSame(local, connection.LocalEndPoint);
        Assert.AreSame(remote, connection.RemoteEndPoint);
        Assert.AreSame(tls, connection.TlsSession);
        CollectionAssert.AreEqual(System.Text.Encoding.ASCII.GetBytes("x"), inner.WrittenBytes);
        Assert.IsTrue(inner.WritesCompleted);
        Assert.IsTrue(inner.Aborted);
        Assert.IsTrue(inner.Disposed);
    }
}
