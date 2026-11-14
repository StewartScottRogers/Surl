namespace Surl.Networking;

/// <summary>The bytes already read come back first; writes and the rest go to the transport.</summary>
[TestClass]
public sealed class ReplayedPrefixStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_PrefixThenTransport_ReturnsEveryByteInOrder()
    {
        using var transport = new MemoryStream([4, 5, 6]);
        using var stream = new ReplayedPrefixStream([1, 2, 3], transport);
        var buffer = new byte[2];

        Assert.AreEqual(2, await stream.ReadAsync(buffer, TestContext.CancellationToken));
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
        Assert.AreEqual(1, await stream.ReadAsync(buffer.AsMemory(0, 2), TestContext.CancellationToken));
        Assert.AreEqual(3, buffer[0]);
        Assert.AreEqual(2, await stream.ReadAsync(buffer, 0, 2, TestContext.CancellationToken));
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, buffer);
        Assert.AreEqual(1, stream.Read(buffer, 0, 2));
        Assert.AreEqual(6, buffer[0]);
        Assert.AreEqual(0, stream.Read(buffer, 0, 2));
    }

    [TestMethod]
    public void Read_PrefixThenTransport_ReturnsPrefixFirst()
    {
        using var transport = new MemoryStream([9]);
        using var stream = new ReplayedPrefixStream([1], transport);
        var buffer = new byte[4];

        Assert.AreEqual(1, stream.Read(buffer, 0, 4));
        Assert.AreEqual(1, buffer[0]);
        Assert.AreEqual(1, stream.Read(buffer, 0, 4));
        Assert.AreEqual(9, buffer[0]);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBufferWhileBytesRemain_ReturnsZeroWithoutWaitingOnTheTransport()
    {
        using var transport = new MemoryStream();
        using var stream = new ReplayedPrefixStream([1], new NeverReadyStream());

        Assert.AreEqual(0, await stream.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken));
        Assert.AreEqual(0, stream.Read([], 0, 0));
    }

    [TestMethod]
    public async Task ReadAsync_EmptyPrefix_GoesToTheTransport()
    {
        using var stream = new ReplayedPrefixStream([], new MemoryStream([7]));
        var buffer = new byte[1];

        Assert.AreEqual(1, await stream.ReadAsync(buffer, TestContext.CancellationToken));
        Assert.AreEqual(7, buffer[0]);
    }

    [TestMethod]
    public async Task Write_GoesStraightToTheTransport()
    {
        using var transport = new MemoryStream();
        using var stream = new ReplayedPrefixStream([], transport);

        stream.Write([1], 0, 1);
        await stream.WriteAsync(new byte[] { 2 }, TestContext.CancellationToken);
        await stream.WriteAsync(new byte[] { 3 }, 0, 1, TestContext.CancellationToken);
        stream.Flush();
        await stream.FlushAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, transport.ToArray());
    }

    [TestMethod]
    public void Capabilities_AreAForwardOnlyReadWriteStream()
    {
        using var stream = new ReplayedPrefixStream([], new MemoryStream());

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
