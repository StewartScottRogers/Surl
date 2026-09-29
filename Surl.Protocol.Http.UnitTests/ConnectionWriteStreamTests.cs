using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class ConnectionWriteStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullConnection_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConnectionWriteStream(null!));
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using var stream = new ConnectionWriteStream(new InMemoryConnection([]));

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public async Task WriteAsync_BothOverloads_HandTheBytesToTheConnection()
    {
        var connection = new InMemoryConnection([]);
        using var stream = new ConnectionWriteStream(connection);

        await stream.WriteAsync("ab"u8.ToArray().AsMemory(), TestContext.CancellationToken);
        await stream.WriteAsync("xcdx"u8.ToArray(), 1, 2, TestContext.CancellationToken);
        await stream.FlushAsync(TestContext.CancellationToken);
        stream.Flush();

        CollectionAssert.AreEqual("abcd"u8.ToArray(), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task WriteAsync_ConnectionThrows_RethrowsAndRecordsTheFailedWrite()
    {
        using var stream = new ConnectionWriteStream(new FailingWriteConnection([]));

        await stream.WriteAsync("a"u8.ToArray(), TestContext.CancellationToken);
        Assert.IsFalse(stream.HasFailedWrite);
        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync("b"u8.ToArray(), TestContext.CancellationToken).AsTask());

        Assert.IsTrue(stream.HasFailedWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using var stream = new ConnectionWriteStream(new InMemoryConnection([]));

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
