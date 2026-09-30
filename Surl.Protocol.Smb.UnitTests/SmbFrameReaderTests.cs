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
    public async Task ReadFrameAsync_LengthOverTheMaximum_IsRefusedBeforeItsBodyIsRead()
    {
        var connection = Connection(SmbTestBytes.Hex("00000005"), SmbTestBytes.Hex("0102030405"));

        var result = await new SmbFrameReader(connection, 4).ReadFrameAsync(TestContext.CancellationToken);
        var unread = new byte[5];
        var unreadCount = await connection.ReadAsync(unread, TestContext.CancellationToken);

        Assert.AreEqual(SmbFrameReadOutcome.MessageTooLarge, result.Outcome);
        Assert.AreEqual((byte)0x00, result.FrameType);
        Assert.AreEqual(5, unreadCount);
        CollectionAssert.AreEqual(SmbTestBytes.Hex("0102030405"), unread);
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
