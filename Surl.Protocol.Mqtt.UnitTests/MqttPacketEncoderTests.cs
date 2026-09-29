namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttPacketEncoderTests
{
    [TestMethod]
    [DataRow(0, new byte[] { 0x00 })]
    [DataRow(127, new byte[] { 0x7F })]
    [DataRow(128, new byte[] { 0x80, 0x01 })]
    [DataRow(203, new byte[] { 0xCB, 0x01 })]
    [DataRow(16_383, new byte[] { 0xFF, 0x7F })]
    [DataRow(16_384, new byte[] { 0x80, 0x80, 0x01 })]
    [DataRow(2_097_151, new byte[] { 0xFF, 0xFF, 0x7F })]
    [DataRow(2_097_152, new byte[] { 0x80, 0x80, 0x80, 0x01 })]
    [DataRow(268_435_455, new byte[] { 0xFF, 0xFF, 0xFF, 0x7F })]
    public void EncodeRemainingLength_WritesSection223Encoding(int remainingLength, byte[] expected)
    {
        var destination = new byte[4];

        var count = MqttPacketEncoder.EncodeRemainingLength(remainingLength, destination);

        CollectionAssert.AreEqual(expected, destination[..count]);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(268_435_456)]
    public void EncodeRemainingLength_OutOfRange_Throws(int remainingLength)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MqttPacketEncoder.EncodeRemainingLength(remainingLength, new byte[4]));
    }

    [TestMethod]
    public void Encode_BodyOf128Bytes_PutsTwoLengthBytesBeforeIt()
    {
        var body = Enumerable.Repeat((byte)0x55, 128).ToArray();

        var packet = MqttPacketEncoder.Encode(0x30, body);

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x80, 0x01 }, packet[..3]);
        CollectionAssert.AreEqual(body, packet[3..]);
    }

    [TestMethod]
    public void RetainedPublish_SetsTheRetainFlagAndEncodesTopicAsUtf8()
    {
        var packet = MqttPacketEncoder.RetainedPublish("é", "x"u8);

        CollectionAssert.AreEqual(new byte[] { 0x31, 0x05, 0x00, 0x02, 0xC3, 0xA9, (byte)'x' }, packet);
    }

    [TestMethod]
    public void SubscribeAcknowledgement_CarriesIdentifierAndEveryReturnCode()
    {
        var packet = MqttPacketEncoder.SubscribeAcknowledgement(0xABCD, [0x00, 0x80]);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0xAB, 0xCD, 0x00, 0x80 }, packet);
    }
}
