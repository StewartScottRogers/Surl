using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

[TestClass]
public sealed class SmbFrameReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadFrameAsync_SessionMessage_ReturnsItsBody()
    {
        var connection = Connection(SmbTestBytes.Hex("00000003 AABBCC"));

        var result = await new SmbFrameReader(connection, 0).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.MessageRead, result.Outcome);
        Assert.AreEqual((byte)0x00, result.FrameType);
        CollectionAssert.AreEqual(SmbTestBytes.Hex("AABBCC"), result.Message);
    }

    [TestMethod]
    public async Task ReadFrameAsync_BytesSplitAcrossReads_ReadsOneFrameAndNoMore()
    {
        var connection = Connection(SmbTestBytes.Hex("00"), SmbTestBytes.Hex("0000"), SmbTestBytes.Hex("02 AA"), SmbTestBytes.Hex("BB 00000001 CC"));
        var reader = new SmbFrameReader(connection, 0);

        var first = await reader.ReadFrameAsync(TestContext.CancellationToken);
        var second = await reader.ReadFrameAsync(TestContext.CancellationToken);
        var third = await reader.ReadFrameAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(SmbTestBytes.Hex("AABB"), first.Message);
        CollectionAssert.AreEqual(SmbTestBytes.Hex("CC"), second.Message);
        Assert.AreEqual(SmbFrameReadOutcome.ConnectionClosed, third.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_LengthExtensionBitSet_ReadsA17BitLength()
    {
        var body = new byte[0x10002];
        body[^1] = 0x7E;
        var connection = Connection([.. SmbTestBytes.Hex("00010002"), .. body]);

        var result = await new SmbFrameReader(connection, 0).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.MessageRead, result.Outcome);
        Assert.HasCount(0x10002, result.Message);
        Assert.AreEqual((byte)0x7E, result.Message[^1]);
    }

    [TestMethod]
    public async Task ReadFrameAsync_KeepAlive_IsRecognised()
    {
        var connection = Connection(SmbTestBytes.Hex("85000000"));

        var result = await new SmbFrameReader(connection, 0).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.KeepAlive, result.Outcome);
        Assert.AreEqual((byte)0x85, result.FrameType);
        Assert.IsEmpty(result.Message);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SessionRequestFrame_IsReportedWithItsTypeAndItsBodyConsumed()
    {
        var connection = Connection(SmbTestBytes.Hex("81000002 2020 00000001 DD"));
        var reader = new SmbFrameReader(connection, 0);

        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);
        var next = await reader.ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.UnexpectedFrameType, result.Outcome);
        Assert.AreEqual((byte)0x81, result.FrameType);
        Assert.IsEmpty(result.Message);
        CollectionAssert.AreEqual(SmbTestBytes.Hex("DD"), next.Message);
    }

    [TestMethod]
    public async Task ReadFrameAsync_LengthOverTheMaximum_KeepsTheShortBodyWholeAndReadsNoFurther()
    {
        var connection = Connection(SmbTestBytes.Hex("00000005"), SmbTestBytes.Hex("0102030405"), SmbTestBytes.Hex("00000001 CC"));
        var reader = new SmbFrameReader(connection, 4);

        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.MessageTooLarge, result.Outcome);
        Assert.AreEqual((byte)0x00, result.FrameType);
        Assert.AreEqual(5, result.MessageLength);
        CollectionAssert.AreEqual(SmbTestBytes.Hex("0102030405"), result.Message);
        CollectionAssert.AreEqual(SmbTestBytes.Hex("CC"), (await reader.ReadFrameAsync(TestContext.CancellationToken)).Message);
    }

    [TestMethod]
    public async Task ReadFrameAsync_LengthOverTheMaximum_KeepsTheSmbHeaderAndDiscardsTheRestBeforeTheNextFrame()
    {
        var body = Enumerable.Range(0, 20000).Select(index => (byte)index).ToArray();
        var connection = Connection(SmbTestBytes.Hex("00004E20"), body[..100], body[100..], SmbTestBytes.Hex("00000001 CC"));
        var reader = new SmbFrameReader(connection, 1000);

        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.MessageTooLarge, result.Outcome);
        Assert.AreEqual(20000, result.MessageLength);
        CollectionAssert.AreEqual(body[..SmbHeader.Length], result.Message);
        CollectionAssert.AreEqual(SmbTestBytes.Hex("CC"), (await reader.ReadFrameAsync(TestContext.CancellationToken)).Message);
    }

    [TestMethod]
    public async Task ReadFrameAsync_LengthOverTheMaximumOfAnotherFrameType_IsDiscardedAndReportedAsUnexpected()
    {
        var connection = Connection(SmbTestBytes.Hex("81000005 0102030405"));

        var result = await new SmbFrameReader(connection, 4).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.UnexpectedFrameType, result.Outcome);
        Assert.AreEqual((byte)0x81, result.FrameType);
    }

    [TestMethod]
    [DataRow("00000040 0102")]
    [DataRow("00000040 0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20 2122")]
    public async Task ReadFrameAsync_ClosedInsideABodyOverTheMaximum_ReportsATruncatedFrame(string hex)
    {
        var result = await new SmbFrameReader(Connection(SmbTestBytes.Hex(hex)), 4).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.ConnectionClosedMidFrame, result.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_LengthAtTheMaximum_IsRead()
    {
        var connection = Connection(SmbTestBytes.Hex("00000004 01020304"));

        var result = await new SmbFrameReader(connection, 4).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.MessageRead, result.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_NoBytes_ReportsTheConnectionClosed()
    {
        var result = await new SmbFrameReader(Connection(), 0).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.ConnectionClosed, result.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ClosedInsideTheHeader_ReportsATruncatedFrame()
    {
        var result = await new SmbFrameReader(Connection(SmbTestBytes.Hex("0000")), 0).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.ConnectionClosedMidFrame, result.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ClosedInsideTheBody_ReportsATruncatedFrame()
    {
        var result = await new SmbFrameReader(Connection(SmbTestBytes.Hex("00000004 0102")), 0).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.ConnectionClosedMidFrame, result.Outcome);
        Assert.IsEmpty(result.Message);
    }

    private static InMemoryConnection Connection(params byte[][] chunks) =>
        new(chunks.Select(chunk => (ReadOnlyMemory<byte>)chunk));
}
