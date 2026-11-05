namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The size prefix and response headers v0 and v1 (the Kafka protocol guide, "Common Request and
/// Response Structure", "Request and Response Headers").
/// </summary>
[TestClass]
public sealed class KafkaResponseFrameTests
{
    [TestMethod]
    public void Encode_HeaderVersionZero_IsSizeCorrelationIdAndBody()
    {
        var frame = KafkaResponseFrame.Encode(0x01020304, 0, [0xAA]);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 5, 0x01, 0x02, 0x03, 0x04, 0xAA }, frame);
    }

    [TestMethod]
    public void Encode_HeaderVersionOne_AddsAnEmptyTaggedFieldSection()
    {
        var frame = KafkaResponseFrame.Encode(-1, 1, []);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 5, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 }, frame);
    }

    [TestMethod]
    [DataRow((short)-1)]
    [DataRow((short)2)]
    public void Encode_HeaderVersionOutsideZeroToOne_Throws(short headerVersion) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaResponseFrame.Encode(0, headerVersion, []));
}
