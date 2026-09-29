namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpUploadStreamTests
{
    [TestMethod]
    public void Capabilities_AreReadOnlyAndForwardOnly()
    {
        using var stream = NewStream();

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([], 0, 0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        stream.Flush();
    }

    [TestMethod]
    public async Task ReadAsync_SmallBuffer_TakesOneBlockInPiecesBeforeAskingForTheNext()
    {
        var clock = new ManualTimeProvider();
        var flow = new ScriptedDatagramFlow([], clock, [ScriptedDatagramFlow.Datagram(UploadExchange.Data(1, "abc"u8.ToArray()))]);
        using var stream = NewStream(flow, clock);
        var buffer = new byte[2];

        Assert.AreEqual(2, await stream.ReadAsync(buffer));
        Assert.AreEqual(1, await stream.ReadAsync(buffer));
        Assert.AreEqual(0, await stream.ReadAsync(buffer));

        Assert.HasCount(1, flow.Sent);
        CollectionAssert.AreEqual(UploadExchange.Ack(1), stream.Acknowledgement);
        Assert.AreEqual(1, stream.BlocksReceived);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_ReturnsZeroAndAsksForNothing()
    {
        var clock = new ManualTimeProvider();
        var flow = new ScriptedDatagramFlow([], clock, []);
        using var stream = NewStream(flow, clock);

        Assert.AreEqual(0, await stream.ReadAsync(Memory<byte>.Empty));

        Assert.IsEmpty(flow.Sent);
    }

    [TestMethod]
    public async Task ReadAsync_ByteArray_CopiesIntoTheGivenRange()
    {
        var clock = new ManualTimeProvider();
        var flow = new ScriptedDatagramFlow([], clock, [ScriptedDatagramFlow.Datagram(UploadExchange.Data(1, "abc"u8.ToArray()))]);
        using var stream = NewStream(flow, clock);
        var buffer = new byte[5];

        Assert.AreEqual(3, await stream.ReadAsync(buffer, 1, 4, CancellationToken.None));

        CollectionAssert.AreEqual(new byte[] { 0, 97, 98, 99, 0 }, buffer);
    }

    [TestMethod]
    public async Task ReadAsync_MoreThan65535Blocks_WrapsTheBlockNumberToZero()
    {
        var clock = new ManualTimeProvider();
        var blocks = Enumerable.Range(1, 65536).Select(block => ScriptedDatagramFlow.Datagram(UploadExchange.Data((ushort)block, [42]))).ToList();
        blocks.Add(ScriptedDatagramFlow.Datagram(UploadExchange.Data(1, [])));
        var flow = new ScriptedDatagramFlow([], clock, blocks);
        using var stream = NewStream(flow, clock, blockSize: 1);
        var buffer = new byte[1];
        long total = 0;

        while (await stream.ReadAsync(buffer) is var read and > 0)
        {
            total += read;
        }

        Assert.AreEqual(65536, total);
        Assert.AreEqual(65537, stream.BlocksReceived);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 255, 255 }, flow.Sent[^2].Bytes);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 0 }, flow.Sent[^1].Bytes);
        CollectionAssert.AreEqual(UploadExchange.Ack(1), stream.Acknowledgement);
    }

    private static TftpUploadStream NewStream(ScriptedDatagramFlow? flow = null, ManualTimeProvider? clock = null, int blockSize = 512)
    {
        clock ??= new ManualTimeProvider();
        flow ??= new ScriptedDatagramFlow([], clock, []);
        var context = UploadExchange.Context(new Surl.Protocol.Abstractions.RecordingExchangeLog(), clock);
        return new TftpUploadStream(new TftpLockStep(flow, context, TimeSpan.FromSeconds(5)), UploadExchange.Ack(0), blockSize);
    }
}
