namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpMessageBufferTests
{
    [TestMethod]
    public async Task Write_WithinTheLimit_KeepsEveryByte()
    {
        using McpMessageBuffer buffer = new(6);

        buffer.Write([1, 2], 0, 2);
        await buffer.WriteAsync(new byte[] { 3, 4 }.AsMemory());
        await buffer.WriteAsync([9, 5, 6], 1, 2, CancellationToken.None);
        buffer.Flush();

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, buffer.ToArray());
        Assert.IsFalse(buffer.IsPastLimit);
        Assert.AreEqual(6, buffer.Length);
        Assert.AreEqual(6, buffer.Position);
    }

    [TestMethod]
    public void Write_PastTheLimit_KeepsNothingMoreAndSaysSo()
    {
        using McpMessageBuffer buffer = new(3);

        buffer.Write([1, 2], 0, 2);
        buffer.Write([3, 4], 0, 2);
        buffer.Write([5], 0, 1);

        Assert.IsTrue(buffer.IsPastLimit);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer.ToArray());
    }

    [TestMethod]
    public void Stream_IsWriteOnly()
    {
        using McpMessageBuffer buffer = new(3);

        Assert.IsFalse(buffer.CanRead);
        Assert.IsFalse(buffer.CanSeek);
        Assert.IsTrue(buffer.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => buffer.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => buffer.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => buffer.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => buffer.Position = 0);
    }
}
