namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The data representations of MQTT 5.0 section 1.5 and property lists (section 2.2.2), read
/// from bytes written by hand from the specification.
/// </summary>
[TestClass]
public sealed class Mqtt5BodyReaderTests
{
    [TestMethod]
    [DataRow(new byte[] { 0x00 }, 0)]
    [DataRow(new byte[] { 0x7F }, 127)]
    [DataRow(new byte[] { 0x80, 0x01 }, 128)]
    [DataRow(new byte[] { 0xFF, 0x7F }, 16_383)]
    [DataRow(new byte[] { 0x80, 0x80, 0x01 }, 16_384)]
    [DataRow(new byte[] { 0xFF, 0xFF, 0x7F }, 2_097_151)]
    [DataRow(new byte[] { 0x80, 0x80, 0x80, 0x01 }, 2_097_152)]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }, 268_435_455)]
    public void ReadVariableByteInteger_BoundaryOfTable1_1_ReadsIt(byte[] bytes, int expected)
    {
        var reader = new Mqtt5BodyReader(bytes);

        var value = reader.ReadVariableByteInteger();

        Assert.AreEqual(expected, value);
        Assert.AreEqual(0, reader.Remaining);
    }

    [TestMethod]
    public void ReadVariableByteInteger_FifthByte_IsMalformed()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x7F });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadVariableByteInteger()).Fault;

        Assert.AreEqual(Mqtt5PacketFault.Malformed("1.5.5"), fault);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x80, 0x00 })]
    [DataRow(new byte[] { 0xFF, 0x80, 0x00 })]
    public void ReadVariableByteInteger_NotMinimal_IsMalformed(byte[] bytes)
    {
        var reader = new Mqtt5BodyReader(bytes);

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadVariableByteInteger()).Fault;

        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-1.5.5-1]"), fault);
    }

    [TestMethod]
    public void ReadFourByteInteger_BigEndian_ReadsIt()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        Assert.AreEqual(0x01020304u, reader.ReadFourByteInteger());
    }

    [TestMethod]
    public void ReadTwoByteInteger_OneByteLeft_IsMalformed()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x01 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadTwoByteInteger()).Fault;

        Assert.AreEqual(Mqtt5ReasonCode.MalformedPacket, fault.ReasonCode);
    }

    [TestMethod]
    public void ReadUtf8String_WellFormed_ReadsIt()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x00, 0x05, 0x41, 0xF0, 0xAA, 0x9B, 0x94 });

        Assert.AreEqual("A\U0002A6D4", reader.ReadUtf8String());
    }

    [TestMethod]
    public void ReadUtf8String_ByteOrderMark_IsKept()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x00, 0x04, 0xEF, 0xBB, 0xBF, 0x41 });

        Assert.AreEqual("﻿A", reader.ReadUtf8String());
    }

    [TestMethod]
    public void ReadUtf8String_NullCharacter_IsMalformed0x81()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x00, 0x03, 0x41, 0x00, 0x42 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadUtf8String()).Fault;

        Assert.AreEqual(Mqtt5ReasonCode.MalformedPacket, fault.ReasonCode);
        Assert.AreEqual("[MQTT-1.5.4-2]", fault.Statement);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x03, 0xED, 0xA0, 0x80 }, DisplayName = "U+D800, an unpaired high surrogate")]
    [DataRow(new byte[] { 0x00, 0x03, 0xED, 0xBF, 0xBF }, DisplayName = "U+DFFF, an unpaired low surrogate")]
    [DataRow(new byte[] { 0x00, 0x01, 0xC3 }, DisplayName = "a truncated sequence")]
    [DataRow(new byte[] { 0x00, 0x02, 0xC0, 0x80 }, DisplayName = "an overlong encoding")]
    public void ReadUtf8String_SurrogateOrIllFormed_IsMalformed0x81(byte[] bytes)
    {
        var reader = new Mqtt5BodyReader(bytes);

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadUtf8String()).Fault;

        Assert.AreEqual(Mqtt5ReasonCode.MalformedPacket, fault.ReasonCode);
        Assert.AreEqual("[MQTT-1.5.4-1]", fault.Statement);
    }

    [TestMethod]
    public void ReadUtf8String_LengthPastTheEnd_IsMalformed()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x00, 0x05, 0x41 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadUtf8String()).Fault;

        Assert.AreEqual(Mqtt5PacketFault.Malformed("4.13.1"), fault);
    }

    [TestMethod]
    public void ReadBinaryData_LengthThenBytes_ReadsThem()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x00, 0x02, 0x00, 0xFF });

        CollectionAssert.AreEqual(new byte[] { 0x00, 0xFF }, reader.ReadBinaryData());
    }

    [TestMethod]
    public void ReadPacketIdentifier_Zero_IsProtocolError()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x00, 0x00 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadPacketIdentifier()).Fault;

        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("[MQTT-2.2.1-3]"), fault);
    }

    [TestMethod]
    public void ReadProperties_EveryType_ReadsEachValue()
    {
        var reader = new Mqtt5BodyReader(new byte[]
        {
            0x1C,
            0x01, 0x01,
            0x23, 0x00, 0x0A,
            0x02, 0x00, 0x00, 0x01, 0x00,
            0x0B, 0x80, 0x01,
            0x03, 0x00, 0x01, 0x74,
            0x09, 0x00, 0x01, 0xAB,
            0x26, 0x00, 0x01, 0x6B, 0x00, 0x01, 0x76,
        });

        var properties = reader.ReadProperties(Mqtt5PropertyContext.Publish);

        Assert.AreEqual(1u, properties[0].Integer);
        Assert.AreEqual(10u, properties[1].Integer);
        Assert.AreEqual(256u, properties[2].Integer);
        Assert.AreEqual(128u, properties[3].Integer);
        Assert.AreEqual("t", properties[4].Text);
        CollectionAssert.AreEqual(new byte[] { 0xAB }, properties[5].Binary);
        Assert.AreEqual("k", properties[6].Text);
        Assert.AreEqual("v", properties[6].PairValue);
    }

    [TestMethod]
    public void ReadProperties_DuplicateProperty_IsProtocolError0x82()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x06, 0x21, 0x00, 0x01, 0x21, 0x00, 0x02 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadProperties(Mqtt5PropertyContext.Connect)).Fault;

        Assert.AreEqual(Mqtt5ReasonCode.ProtocolError, fault.ReasonCode);
    }

    [TestMethod]
    public void ReadProperties_SubscriptionIdentifierTwiceInSubscribe_IsProtocolError()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x04, 0x0B, 0x01, 0x0B, 0x02 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadProperties(Mqtt5PropertyContext.Subscribe)).Fault;

        Assert.AreEqual(Mqtt5ReasonCode.ProtocolError, fault.ReasonCode);
    }

    [TestMethod]
    public void ReadProperties_SubscriptionIdentifierTwiceInPublish_ReadsBoth()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x04, 0x0B, 0x01, 0x0B, 0x02 });

        var properties = reader.ReadProperties(Mqtt5PropertyContext.Publish);

        Assert.HasCount(2, properties);
    }

    [TestMethod]
    public void ReadProperties_PropertyNotAllowedInItsPacket_IsMalformed0x81()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x02, 0x24, 0x01 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadProperties(Mqtt5PropertyContext.Connect)).Fault;

        Assert.AreEqual(Mqtt5PacketFault.Malformed("2.2.2.2"), fault);
    }

    [TestMethod]
    public void ReadProperties_UndefinedIdentifier_IsMalformed()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x02, 0x04, 0x01 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadProperties(Mqtt5PropertyContext.Publish)).Fault;

        Assert.AreEqual(Mqtt5PacketFault.Malformed("2.2.2.2"), fault);
    }

    [TestMethod]
    public void ReadProperties_ValuePastThePropertyLength_IsMalformed()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x02, 0x21, 0x00, 0x01 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.ReadProperties(Mqtt5PropertyContext.Connect)).Fault;

        Assert.AreEqual(Mqtt5PacketFault.Malformed("4.13.1"), fault);
    }

    [TestMethod]
    public void ReadOptionalReasonCodeAndProperties_NothingLeft_IsSuccessWithNoProperties()
    {
        var reader = new Mqtt5BodyReader(ReadOnlyMemory<byte>.Empty);

        var (reasonCode, properties) = reader.ReadOptionalReasonCodeAndProperties(Mqtt5PropertyContext.Disconnect);

        Assert.AreEqual(Mqtt5ReasonCode.Success, reasonCode);
        Assert.IsEmpty(properties);
    }

    [TestMethod]
    public void ReadOptionalReasonCodeAndProperties_OneByteLeft_IsThatReasonCodeWithNoProperties()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x04 });

        var (reasonCode, properties) = reader.ReadOptionalReasonCodeAndProperties(Mqtt5PropertyContext.Disconnect);

        Assert.AreEqual(Mqtt5ReasonCode.DisconnectWithWillMessage, reasonCode);
        Assert.IsEmpty(properties);
    }

    [TestMethod]
    public void RequireEnd_ByteLeft_IsMalformed()
    {
        var reader = new Mqtt5BodyReader(new byte[] { 0x00 });

        var fault = Assert.ThrowsExactly<Mqtt5PacketFaultException>(() => reader.RequireEnd("3.12.2")).Fault;

        Assert.AreEqual(Mqtt5PacketFault.Malformed("3.12.2"), fault);
    }
}
