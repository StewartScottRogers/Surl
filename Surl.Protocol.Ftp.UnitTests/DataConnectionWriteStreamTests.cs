using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

[TestClass]
public sealed class DataConnectionWriteStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_HandsEveryByteToTheConnection()
    {
        var connection = new InMemoryConnection([]);
        var stream = new DataConnectionWriteStream(connection);

        await stream.WriteAsync("hel"u8.ToArray(), 0, 3, TestContext.CancellationToken);
        await stream.WriteAsync("lo"u8.ToArray().AsMemory(), TestContext.CancellationToken);
        stream.Flush();
        await stream.FlushAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual("hello"u8.ToArray(), connection.WrittenBytes);
        Assert.IsFalse(stream.ConnectionWriteFailed);
    }

    [TestMethod]
    public async Task WriteAsync_ConnectionFails_RethrowsAndRemembersIt()
    {
        var connection = new InMemoryConnection([]);
        connection.Abort();
        var stream = new DataConnectionWriteStream(connection);

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync("x"u8.ToArray(), 0, 1, TestContext.CancellationToken));

        Assert.IsTrue(stream.ConnectionWriteFailed);
    }

    [TestMethod]
    public void Stream_IsWriteOnlyAndAsynchronousOnly()
    {
        var stream = new DataConnectionWriteStream(new InMemoryConnection([]));

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DataConnectionWriteStream(null!));
    }
}
