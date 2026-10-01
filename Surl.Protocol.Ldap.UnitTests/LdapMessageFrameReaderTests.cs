using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapRequestBytes;

namespace Surl.Protocol.Ldap;

[TestClass]
public sealed class LdapMessageFrameReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadFrameAsync_TwoMessagesOneBytePerRead_ReadsEachWholeThenConnectionClosed()
    {
        var bind = Hex("300c020101600702010304008000");
        var unbind = Hex("30050201024200");
        var reader = new LdapMessageFrameReader(Connection(OneBytePerRead([.. bind, .. unbind])), 0);

        var first = await reader.ReadFrameAsync(TestContext.CancellationToken);
        var second = await reader.ReadFrameAsync(TestContext.CancellationToken);
        var third = await reader.ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(LdapFrameReadOutcome.FrameRead, first.Outcome);
        CollectionAssert.AreEqual(bind, first.Message);
        CollectionAssert.AreEqual(unbind, second.Message);
        Assert.AreEqual(LdapFrameReadResult.NoFrame(LdapFrameReadOutcome.ConnectionClosed), third);
    }

    [TestMethod]
    public async Task ReadFrameAsync_LongFormLength_ReadsTheWholeValue()
    {
        var message = Framed(0x82, 0x01, 0x2C, 300);

        var result = await new LdapMessageFrameReader(Connection([message]), 0).ReadFrameAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(message, result.Message);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ValueLongerThanTheFirstBuffer_GrowsTheBufferAndReadsItAll()
    {
        var message = Framed(0x83, 0x03, 0x0D, 0x40, 200_000);
        var chunks = message.Chunk(4096).Select(chunk => (ReadOnlyMemory<byte>)chunk);

        var result = await new LdapMessageFrameReader(Connection(chunks), 0).ReadFrameAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(message, result.Message);
    }

    [TestMethod]
    public async Task ReadFrameAsync_MessageOfExactlyTheLimit_IsRead()
    {
        var message = Framed(0x82, 0x01, 0x2C, 300);

        var result = await new LdapMessageFrameReader(Connection([message]), message.Length).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(LdapFrameReadOutcome.FrameRead, result.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_MessageOneByteOverTheLimit_IsRefusedBeforeItsValueIsRead()
    {
        var message = Framed(0x82, 0x01, 0x2C, 300);
        var connection = new ReadCountingConnection(new InMemoryConnection(OneBytePerRead(message)));

        var result = await new LdapMessageFrameReader(connection, message.Length - 1).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(LdapFrameReadResult.TooLarge(message.Length), result);
        Assert.AreEqual(4, connection.BytesRead);
    }

    [TestMethod]
    public async Task ReadFrameAsync_LengthNoArrayCanHold_IsTooLargeWithNoLimit()
    {
        var result = await ReadOnlyAsync("3084ffffffff00");

        Assert.AreEqual(LdapFrameReadResult.TooLarge(0xFFFFFFFFL + 6), result);
    }

    [TestMethod]
    [DataRow("31050201024200", (int)LdapFrameReadOutcome.NotASequence, DisplayName = "A SET, not a SEQUENCE")]
    [DataRow("30", (int)LdapFrameReadOutcome.ConnectionClosedMidMessage, DisplayName = "Closed after the tag")]
    [DataRow("3082", (int)LdapFrameReadOutcome.ConnectionClosedMidMessage, DisplayName = "Closed inside the long length")]
    [DataRow("30050201", (int)LdapFrameReadOutcome.ConnectionClosedMidMessage, DisplayName = "Closed inside the value")]
    [DataRow("30800201024200000000", (int)LdapFrameReadOutcome.IndefiniteLength, DisplayName = "Indefinite length")]
    [DataRow("30850000000005", (int)LdapFrameReadOutcome.MalformedLength, DisplayName = "Five length octets")]
    [DataRow("30ff", (int)LdapFrameReadOutcome.MalformedLength, DisplayName = "The reserved length octet")]
    public async Task ReadFrameAsync_MalformedOrTruncated_ReportsItsOutcome(string hex, int expected)
    {
        var result = await ReadOnlyAsync(hex);

        Assert.AreEqual(LdapFrameReadResult.NoFrame((LdapFrameReadOutcome)expected), result);
    }

    [TestMethod]
    public async Task ReadFrameAsync_TwoMessagesInOneRead_HoldsTheSecondForTheNextRead()
    {
        var bind = Hex("300c020101600702010304008000");
        var unbind = Hex("30050201024200");
        var reader = new LdapMessageFrameReader(Connection([(byte[])[.. bind, .. unbind]]), 0);

        var first = await reader.ReadFrameAsync(TestContext.CancellationToken);
        var second = await reader.ReadFrameAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(bind, first.Message);
        CollectionAssert.AreEqual(unbind, second.Message);
    }

    [TestMethod]
    public async Task DiscardReadAhead_BytesReadPastAMessage_AreThrownAwayAndCounted()
    {
        var bind = Hex("300c020101600702010304008000");
        var unbind = Hex("30050201024200");
        var reader = new LdapMessageFrameReader(Connection([(byte[])[.. bind, .. unbind], unbind]), 0);
        await reader.ReadFrameAsync(TestContext.CancellationToken);

        var discarded = reader.DiscardReadAhead();
        var next = await reader.ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(unbind.Length, discarded);
        CollectionAssert.AreEqual(unbind, next.Message);
        Assert.AreEqual(0, reader.DiscardReadAhead());
    }

    [TestMethod]
    public async Task ReadSecurityLayerBufferAsync_LengthThenBytes_ReturnsTheBytesWithoutTheLength()
    {
        var reader = new LdapMessageFrameReader(Connection([Hex("00000003 AABBCC 00000000")]), 0);

        var first = await reader.ReadSecurityLayerBufferAsync(100, TestContext.CancellationToken);
        var empty = await reader.ReadSecurityLayerBufferAsync(100, TestContext.CancellationToken);
        var closed = await reader.ReadSecurityLayerBufferAsync(100, TestContext.CancellationToken);

        CollectionAssert.AreEqual(Hex("AABBCC"), first.Message);
        Assert.IsEmpty(empty.Message!);
        Assert.AreEqual(LdapFrameReadResult.NoFrame(LdapFrameReadOutcome.ConnectionClosed), closed);
    }

    [TestMethod]
    [DataRow(3, 0, DisplayName = "Past the layer's maximum")]
    [DataRow(100, 4, DisplayName = "Past the message limit")]
    public async Task ReadSecurityLayerBufferAsync_TooLong_IsRefusedBeforeItsBytesAreRead(int maxProtectedBytes, int maxMessageBytes)
    {
        var connection = new ReadCountingConnection(new InMemoryConnection(OneBytePerRead(Hex("00000005 0102030405"))));

        var result = await new LdapMessageFrameReader(connection, maxMessageBytes).ReadSecurityLayerBufferAsync(maxProtectedBytes, TestContext.CancellationToken);

        Assert.AreEqual(LdapFrameReadResult.TooLarge(5), result);
        Assert.AreEqual(4, connection.BytesRead);
    }

    [TestMethod]
    [DataRow("0000", DisplayName = "Closed inside the length")]
    [DataRow("0000000501", DisplayName = "Closed inside the bytes")]
    public async Task ReadSecurityLayerBufferAsync_ClosedPartWay_IsClosedMidMessage(string hex)
    {
        var result = await new LdapMessageFrameReader(Connection([Hex(hex)]), 0).ReadSecurityLayerBufferAsync(100, TestContext.CancellationToken);

        Assert.AreEqual(LdapFrameReadResult.NoFrame(LdapFrameReadOutcome.ConnectionClosedMidMessage), result);
    }

    private static InMemoryConnection Connection(IEnumerable<ReadOnlyMemory<byte>> chunks) => new(chunks);

    private static IEnumerable<ReadOnlyMemory<byte>> OneBytePerRead(byte[] bytes) => bytes.Select(octet => (ReadOnlyMemory<byte>)new[] { octet });

    // A SEQUENCE tag, the length octets given and a value of valueLength bytes.
    private static byte[] Framed(byte lengthOctet, byte first, byte second, int valueLength) =>
        [0x30, lengthOctet, first, second, .. new byte[valueLength]];

    private static byte[] Framed(byte lengthOctet, byte first, byte second, byte third, int valueLength) =>
        [0x30, lengthOctet, first, second, third, .. new byte[valueLength]];

    private async Task<LdapFrameReadResult> ReadOnlyAsync(string hex) =>
        await new LdapMessageFrameReader(Connection([Hex(hex)]), 0).ReadFrameAsync(TestContext.CancellationToken);
}
