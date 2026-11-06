namespace Surl.Extended.Protocol.Grpc;

[TestClass]
public sealed class LengthPrefixedMessageTests
{
    [TestMethod]
    public void WriteUncompressed_Message_WritesFlagZeroAndItsBigEndianLength()
    {
        var framed = LengthPrefixedMessage.WriteUncompressed([0x08, 0x96, 0x01]);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x03, 0x08, 0x96, 0x01 }, framed);
    }

    [TestMethod]
    public void WriteUncompressed_EmptyMessage_WritesThePrefixAlone()
    {
        var framed = LengthPrefixedMessage.WriteUncompressed([]);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 }, framed);
    }
}
