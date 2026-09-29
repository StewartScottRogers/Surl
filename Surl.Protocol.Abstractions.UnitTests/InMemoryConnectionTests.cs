using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class InMemoryConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NoEndPoints_UsesTheLoopbackDefaults()
    {
        var connection = new InMemoryConnection([]);

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 80), connection.LocalEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50000), connection.RemoteEndPoint);
    }

    [TestMethod]
    public void Constructor_GivenEndPoints_KeepsThem()
    {
        var local = new IPEndPoint(IPAddress.IPv6Loopback, 8080);
        var remote = new IPEndPoint(IPAddress.IPv6Loopback, 60000);

        var connection = new InMemoryConnection([], local, remote);

        Assert.AreSame(local, connection.LocalEndPoint);
        Assert.AreSame(remote, connection.RemoteEndPoint);
    }

    [TestMethod]
    public void Constructor_NullScript_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new InMemoryConnection(null!));
    }

    [TestMethod]
    public async Task ReadAsync_TwoChunks_NeverJoinsThemInOneRead()
    {
        var connection = new InMemoryConnection([Bytes("GET "), Bytes("/ HTTP/1.1")]);
        var buffer = new byte[64];

        var first = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        var firstText = Text(buffer, first);
        var second = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual("GET ", firstText);
        Assert.AreEqual("/ HTTP/1.1", Text(buffer, second));
    }

    [TestMethod]
    public async Task ReadAsync_BufferSmallerThanTheChunk_ReturnsTheChunkAcrossReads()
    {
        var connection = new InMemoryConnection([Bytes("abcde")]);
        var buffer = new byte[2];
        var read = new List<string>();

        for (var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);
            count > 0;
            count = await connection.ReadAsync(buffer, TestContext.CancellationToken))
        {
            read.Add(Text(buffer, count));
        }

        CollectionAssert.AreEqual(new[] { "ab", "cd", "e" }, read);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyChunkInTheScript_IsSkipped()
    {
        var connection = new InMemoryConnection([Bytes("a"), ReadOnlyMemory<byte>.Empty, Bytes("b")]);
        var buffer = new byte[8];

        await connection.ReadAsync(buffer, TestContext.CancellationToken);
        var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual("b", Text(buffer, count));
    }

    [TestMethod]
    public async Task ReadAsync_ScriptExhaustedAndPeerHalfCloses_ReturnsZeroEveryTime()
    {
        var connection = new InMemoryConnection([Bytes("x")]);
        var buffer = new byte[8];
        await connection.ReadAsync(buffer, TestContext.CancellationToken);

        var first = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        var second = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual(0, first);
        Assert.AreEqual(0, second);
    }

    [TestMethod]
    public async Task ReadAsync_ScriptExhaustedAndPeerStaysOpen_WaitsUntilCancelled()
    {
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        using var cancellation = new CancellationTokenSource();

        var read = connection.ReadAsync(new byte[8], cancellation.Token).AsTask();
        Assert.IsFalse(read.IsCompleted);
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => read);
    }

    [TestMethod]
    public async Task ReadAsync_PendingWhenAborted_ThrowsIOException()
    {
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var read = connection.ReadAsync(new byte[8], TestContext.CancellationToken).AsTask();
        Assert.IsFalse(read.IsCompleted);
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(() => read);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_ThrowsArgumentException()
    {
        var connection = new InMemoryConnection([Bytes("x")]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => connection.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ReadAsync_AlreadyCancelled_ThrowsOperationCanceledException()
    {
        var connection = new InMemoryConnection([Bytes("x")]);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => connection.ReadAsync(new byte[8], new CancellationToken(true)).AsTask());
    }

    [TestMethod]
    public async Task ReadAsync_AfterAbort_ThrowsIOException()
    {
        var connection = new InMemoryConnection([Bytes("x")]);
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.ReadAsync(new byte[8], TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ReadAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var connection = new InMemoryConnection([Bytes("x")]);
        await connection.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => connection.ReadAsync(new byte[8], TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ReadAsync_AfterCompleteWrites_StillReads()
    {
        var connection = new InMemoryConnection([Bytes("x")]);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        var buffer = new byte[8];

        var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual("x", Text(buffer, count));
    }

    [TestMethod]
    public async Task WrittenBytes_SeveralWrites_RecordsEveryByteInOrder()
    {
        var connection = new InMemoryConnection([]);

        await connection.WriteAsync(Bytes("HTTP/1.1 200 OK\r\n"), TestContext.CancellationToken);
        await connection.WriteAsync(Bytes("\r\n"), TestContext.CancellationToken);

        Assert.AreEqual("HTTP/1.1 200 OK\r\n\r\n", Text(connection.WrittenBytes, connection.WrittenBytes.Length));
    }

    [TestMethod]
    public async Task WrittenBytes_ChangedByTheCaller_DoesNotChangeTheRecord()
    {
        var connection = new InMemoryConnection([]);
        await connection.WriteAsync(Bytes("a"), TestContext.CancellationToken);

        connection.WrittenBytes[0] = (byte)'z';

        CollectionAssert.AreEqual(Bytes("a").ToArray(), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task WriteAsync_AfterCompleteWrites_ThrowsInvalidOperationException()
    {
        var connection = new InMemoryConnection([]);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.WriteAsync(Bytes("x"), TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task WriteAsync_AfterAbort_ThrowsIOException()
    {
        var connection = new InMemoryConnection([]);
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.WriteAsync(Bytes("x"), TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task WriteAsync_AlreadyCancelled_ThrowsOperationCanceledExceptionAndWritesNothing()
    {
        var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => connection.WriteAsync(Bytes("x"), new CancellationToken(true)).AsTask());

        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_CalledTwice_IsHarmless()
    {
        var connection = new InMemoryConnection([]);

        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);

        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_AlreadyCancelled_ThrowsOperationCanceledException()
    {
        var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => connection.CompleteWritesAsync(new CancellationToken(true)).AsTask());

        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_AfterAbort_ThrowsIOException()
    {
        var connection = new InMemoryConnection([]);
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.CompleteWritesAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public void Abort_CalledTwice_IsHarmless()
    {
        var connection = new InMemoryConnection([]);

        connection.Abort();
        connection.Abort();

        Assert.IsTrue(connection.Aborted);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task DisposeAsync_WritesNotCompleted_CompletesThem()
    {
        var connection = new InMemoryConnection([]);

        await connection.DisposeAsync();

        Assert.IsTrue(connection.Disposed);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task DisposeAsync_AfterAbort_LeavesWritesUncompleted()
    {
        var connection = new InMemoryConnection([]);
        connection.Abort();

        await connection.DisposeAsync();

        Assert.IsTrue(connection.Disposed);
        Assert.IsFalse(connection.WritesCompleted);
        Assert.IsTrue(connection.Aborted);
    }

    [TestMethod]
    public async Task DisposeAsync_CalledTwice_IsHarmless()
    {
        var connection = new InMemoryConnection([]);

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.IsTrue(connection.Disposed);
    }

    [TestMethod]
    public void State_NewConnection_IsOpen()
    {
        var connection = new InMemoryConnection([]);

        Assert.IsFalse(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.IsFalse(connection.Disposed);
        Assert.IsEmpty(connection.WrittenBytes);
    }

    private static ReadOnlyMemory<byte> Bytes(string text) => System.Text.Encoding.ASCII.GetBytes(text);

    private static string Text(byte[] buffer, int count) => System.Text.Encoding.ASCII.GetString(buffer, 0, count);
}
