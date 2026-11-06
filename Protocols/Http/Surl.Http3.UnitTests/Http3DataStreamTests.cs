namespace Surl.Http3;

[TestClass]
public sealed class Http3DataStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task WriteAsync_ArrayWrite_SendsTheSliceAsOneFrame()
    {
        var written = new List<byte[]>();
        using var stream = new Http3DataStream((bytes, _) =>
        {
            written.Add(bytes.ToArray());
            return ValueTask.CompletedTask;
        });

        await stream.WriteAsync([1, 2, 3, 4], 1, 2, Token);

        CollectionAssert.AreEqual(new byte[] { 2, 3 }, written.Single());
    }

    [TestMethod]
    public void Members_AStreamOfFramesCannotHave_ThrowNotSupported()
    {
        using var stream = new Http3DataStream((_, _) => ValueTask.CompletedTask);

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
        stream.Flush();
        Assert.IsTrue(stream.FlushAsync(Token).IsCompletedSuccessfully);
    }
}
