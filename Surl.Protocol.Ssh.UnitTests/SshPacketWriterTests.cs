using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshPacketWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(4, 7, DisplayName = "5 + 4 bytes leave room for 7 padding bytes")]
    [DataRow(3, 8, DisplayName = "5 + 3 bytes need a whole block of padding, not 0")]
    [DataRow(0, 11, DisplayName = "5 bytes would need 3; they get 11")]
    [DataRow(2, 9, DisplayName = "5 + 2 bytes would need 1; they get 9")]
    public void PaddingLengthFor_Payload_IsTheFewestOf4OrMoreThatFillABlock(int payloadLength, int expected)
    {
        var padding = SshPacketWriter.PaddingLengthFor(payloadLength);

        Assert.AreEqual(expected, padding);
    }

    [TestMethod]
    public async Task WriteAsync_Payload_IsFramedWithRandomPadding()
    {
        var connection = new InMemoryConnection([]);

        await new SshPacketWriter(connection, new FixedRandomSource()).WriteAsync(new byte[] { 21 }, TestContext.CancellationToken);

        CollectionAssert.AreEqual(Concat(UInt32(12), [10, 21], Enumerable.Repeat(RandomByte, 10).ToArray()), connection.WrittenBytes);
    }
}
