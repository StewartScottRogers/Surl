namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The data representations of MQTT 5.0 section 1.5, written.
/// </summary>
[TestClass]
public sealed class Mqtt5BodyWriterTests
{
    [TestMethod]
    [DataRow(0, new byte[] { 0x00 })]
    [DataRow(127, new byte[] { 0x7F })]
    [DataRow(128, new byte[] { 0x80, 0x01 })]
    [DataRow(16_383, new byte[] { 0xFF, 0x7F })]
    [DataRow(16_384, new byte[] { 0x80, 0x80, 0x01 })]
    [DataRow(2_097_151, new byte[] { 0xFF, 0xFF, 0x7F })]
    [DataRow(2_097_152, new byte[] { 0x80, 0x80, 0x80, 0x01 })]
    [DataRow(268_435_455, new byte[] { 0xFF, 0xFF, 0xFF, 0x7F })]
    public void WriteVariableByteInteger_BoundaryOfTable1_1_WritesTheFewestBytes(int value, byte[] expected)
    {
        var writer = new Mqtt5BodyWriter();

        writer.WriteVariableByteInteger(value);

        CollectionAssert.AreEqual(expected, writer.WrittenSpan.ToArray());
    }

    [TestMethod]
    [DataRow(-1L)]
    [DataRow(268_435_456L)]
    public void WriteVariableByteInteger_OutOfRange_Throws(long value)
    {
        var writer = new Mqtt5BodyWriter();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => writer.WriteVariableByteInteger(value));
    }

    [TestMethod]
    public void WriteUtf8String_Text_WritesLengthThenUtf8()
    {
        var writer = new Mqtt5BodyWriter();

        writer.WriteUtf8String("Aé");

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x03, 0x41, 0xC3, 0xA9 }, writer.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void WriteUtf8String_UnpairedSurrogate_Throws()
    {
        var writer = new Mqtt5BodyWriter();

        Assert.ThrowsExactly<System.Text.EncoderFallbackException>(() => writer.WriteUtf8String("\uD800"));
    }

    [TestMethod]
    public void WriteBinaryData_Over65535Bytes_Throws()
    {
        var writer = new Mqtt5BodyWriter();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => writer.WriteBinaryData(new byte[65_536]));
    }

    [TestMethod]
    public void WriteFourByteInteger_Value_WritesBigEndian()
    {
        var writer = new Mqtt5BodyWriter();

        writer.WriteFourByteInteger(0x01020304);

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04 }, writer.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void WriteProperties_ByteValueOver255_Throws()
    {
        var writer = new Mqtt5BodyWriter();
        var property = Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.PayloadFormatIndicator, 256);

        Assert.ThrowsExactly<OverflowException>(() => writer.WriteProperties([property]));
    }

    [TestMethod]
    public void WriteProperties_TwoByteValueOver65535_Throws()
    {
        var writer = new Mqtt5BodyWriter();
        var property = Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ReceiveMaximum, 65_536);

        Assert.ThrowsExactly<OverflowException>(() => writer.WriteProperties([property]));
    }

    [TestMethod]
    public void WriteOptionalReasonCodeAndProperties_FailureWithProperties_WritesBoth()
    {
        var writer = new Mqtt5BodyWriter();

        writer.WriteOptionalReasonCodeAndProperties(
            Mqtt5ReasonCode.QuotaExceeded,
            [Mqtt5Property.FromText(Mqtt5PropertyIdentifier.ReasonString, "q")]);

        CollectionAssert.AreEqual(new byte[] { 0x97, 0x04, 0x1F, 0x00, 0x01, 0x71 }, writer.WrittenSpan.ToArray());
    }
}
