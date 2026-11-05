using Surl.Protocol.Abstractions;

namespace Surl.Http2;

[TestClass]
public sealed class Http2DataStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_ArrayOverload_SendsTheBytesAsDataEndingTheBody()
    {
        var connection = new InMemoryConnection([]);
        var stream = Stream(connection, bodyLength: 3);

        await stream.WriteAsync(Http2TestBytes.Ascii("xabcx"), 1, 3, TestContext.CancellationToken);
        await stream.FlushAsync(TestContext.CancellationToken);
        stream.Flush();

        var data = (Http2DataFrame)Http2TestClient.Frames(connection.WrittenBytes).Single();
        Assert.AreEqual(1, data.StreamId);
        Assert.IsTrue(data.EndStream);
        CollectionAssert.AreEqual(Http2TestBytes.Ascii("abc"), data.Data);
        Assert.IsFalse(stream.HasFailedWrite);
    }

    [TestMethod]
    public void Members_OfAWriteOnlyStream_SayItCanOnlyBeWrittenAsynchronously()
    {
        using var stream = Stream(new InMemoryConnection([]), bodyLength: 1);

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
    }

    private static Http2DataStream Stream(InMemoryConnection connection, long bodyLength)
    {
        var windows = new Http2SendWindows();
        windows.OpenStream(1);

        return new Http2DataStream(new Http2FrameWriter(connection), windows, 1, bodyLength, CancellationToken.None, CancellationToken.None);
    }
}
