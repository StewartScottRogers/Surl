namespace Surl.Networking;

[TestClass]
public sealed class PeekedBytePrefixStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_Memory_ReturnsThePeekedByteThenTheTransportsBytes()
    {
        var stream = new PeekedBytePrefixStream(0x16, new MemoryStream([0x03, 0x01]));
        var buffer = new byte[8];

        var first = await stream.ReadAsync(buffer.AsMemory(), TestContext.CancellationToken);
        var firstByte = buffer[0];
        var second = await stream.ReadAsync(buffer.AsMemory(), TestContext.CancellationToken);

        Assert.AreEqual(1, first);
        Assert.AreEqual((byte)0x16, firstByte);
        Assert.AreEqual(2, second);
        Assert.AreEqual((byte)0x03, buffer[0]);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_KeepsThePeekedByte()
    {
        var stream = new PeekedBytePrefixStream(0x16, new MemoryStream([0x03]));
        var buffer = new byte[1];

        var empty = await stream.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken);
        var first = await stream.ReadAsync(buffer.AsMemory(), TestContext.CancellationToken);

        Assert.AreEqual(0, empty);
        Assert.AreEqual(1, first);
        Assert.AreEqual((byte)0x16, buffer[0]);
    }

    [TestMethod]
    public async Task ReadAsync_Array_ReturnsThePeekedByteFirst()
    {
        var stream = new PeekedBytePrefixStream(0x16, new MemoryStream([0x03]));
        var buffer = new byte[4];

        var count = await stream.ReadAsync(buffer, 1, 3, TestContext.CancellationToken);

        Assert.AreEqual(1, count);
        Assert.AreEqual((byte)0x16, buffer[1]);
    }

    [TestMethod]
    public void Read_Array_ReturnsThePeekedByteThenTheTransportsBytes()
    {
        var stream = new PeekedBytePrefixStream(0x16, new MemoryStream([0x03, 0x01]));
        var buffer = new byte[8];

        var first = stream.Read(buffer, 0, 8);
        var firstByte = buffer[0];
        var second = stream.Read(buffer, 2, 6);

        Assert.AreEqual(1, first);
        Assert.AreEqual((byte)0x16, firstByte);
        Assert.AreEqual(2, second);
        Assert.AreEqual((byte)0x01, buffer[3]);
    }

    [TestMethod]
    public void Read_NullBuffer_Throws()
    {
        var stream = new PeekedBytePrefixStream(0x16, new MemoryStream());

        Assert.ThrowsExactly<ArgumentNullException>(() => stream.Read(null!, 0, 1));
    }

    [TestMethod]
    public async Task Write_EveryOverload_WritesToTheTransport()
    {
        var transport = new MemoryStream();
        var stream = new PeekedBytePrefixStream(0x16, transport);

        stream.Write([0x01], 0, 1);
        await stream.WriteAsync(new byte[] { 0x02 }.AsMemory(), TestContext.CancellationToken);
        await stream.WriteAsync([0x00, 0x03], 1, 1, TestContext.CancellationToken);
        stream.Flush();
        await stream.FlushAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, transport.ToArray());
    }

    [TestMethod]
    public void Capabilities_ReadAndWriteButNoSeek()
    {
        var stream = new PeekedBytePrefixStream(0x16, new MemoryStream());

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Dispose_LeavesTheTransportOpen()
    {
        var transport = new MemoryStream();
        var stream = new PeekedBytePrefixStream(0x16, transport);

        stream.Dispose();

        Assert.IsTrue(transport.CanRead);
    }
}
