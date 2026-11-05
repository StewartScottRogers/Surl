using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5TestPackets;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// Packets that break MQTT 5.0, written by hand from chapters 2 and 3, and the reason code
/// section 4.13 gives each.
/// </summary>
[TestClass]
public sealed class Mqtt5PacketDecoderTests
{
    private const byte Connect = 0x10;

    [TestMethod]
    public void Decode_ReservedPacketType0_IsMalformed()
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("2.1.2"), FaultOf(0x00, 0x00));
    }

    [TestMethod]
    [DataRow((byte)0x11, DisplayName = "CONNECT with flag bit 0")]
    [DataRow((byte)0x28, DisplayName = "CONNACK with flag bit 3")]
    [DataRow((byte)0x42, DisplayName = "PUBACK with flag bit 1")]
    [DataRow((byte)0x60, DisplayName = "PUBREL without 0010")]
    [DataRow((byte)0x80, DisplayName = "SUBSCRIBE without 0010")]
    [DataRow((byte)0xA3, DisplayName = "UNSUBSCRIBE with 0011")]
    [DataRow((byte)0xC1, DisplayName = "PINGREQ with flag bit 0")]
    [DataRow((byte)0xE4, DisplayName = "DISCONNECT with flag bit 2")]
    [DataRow((byte)0xF1, DisplayName = "AUTH with flag bit 0")]
    public void Decode_ReservedFixedHeaderFlagsSet_IsMalformed0x81(byte firstByte)
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-2.1.3-1]"), FaultOf(firstByte, 0x00));
    }

    [TestMethod]
    [DataRow((byte)0x01, "[MQTT-3.1.2-3]", DisplayName = "the reserved flag")]
    [DataRow((byte)0x08, "[MQTT-3.1.2-11]", DisplayName = "Will QoS without the Will Flag")]
    [DataRow((byte)0x20, "[MQTT-3.1.2-13]", DisplayName = "Will Retain without the Will Flag")]
    [DataRow((byte)0x1C, "[MQTT-3.1.2-12]", DisplayName = "Will QoS 3")]
    public void Decode_ConnectWithForbiddenConnectFlags_IsMalformed(byte connectFlags, string statement)
    {
        var fault = FaultOf(Packet(Connect, ConnectBody(connectFlags)));

        Assert.AreEqual(Mqtt5PacketFault.Malformed(statement), fault);
    }

    [TestMethod]
    public void Decode_ConnectAtLevel4_IsUnsupportedProtocolLevel()
    {
        var fault = FaultOf(Packet(Connect, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x04, 0x02, 0x00, 0x3C, 0x00, 0x00));

        Assert.AreSame(Mqtt5PacketFault.UnsupportedProtocolLevel, fault);
    }

    [TestMethod]
    public void Decode_ConnectNamingMqisdp_IsUnsupportedProtocolLevel()
    {
        var fault = FaultOf(Packet(Connect, 0x00, 0x06, 0x4D, 0x51, 0x49, 0x73, 0x64, 0x70, 0x03, 0x02, 0x00, 0x3C));

        Assert.AreSame(Mqtt5PacketFault.UnsupportedProtocolLevel, fault);
    }

    [TestMethod]
    public void Decode_ConnectNamingAnotherProtocol_IsUnknownProtocolName()
    {
        var fault = FaultOf(Packet(Connect, 0x00, 0x04, 0x41, 0x42, 0x43, 0x44, 0x05, 0x02, 0x00, 0x3C, 0x00, 0x00, 0x00));

        Assert.AreSame(Mqtt5PacketFault.UnknownProtocolName, fault);
        Assert.AreEqual(Mqtt5ReasonCode.UnsupportedProtocolVersion, fault!.ReasonCode);
    }

    [TestMethod]
    public void Decode_ConnectWithAByteAfterThePayload_IsMalformed()
    {
        var fault = FaultOf(Packet(Connect, [.. ConnectBody(0x02), 0xFF]));

        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-3.1.3-1]"), fault);
    }

    [TestMethod]
    public void Decode_ConnectWhoseUserNameFlagHasNoUserName_IsMalformed()
    {
        var fault = FaultOf(Packet(Connect, ConnectBody(0x80)));

        Assert.AreEqual(Mqtt5PacketFault.Malformed("4.13.1"), fault);
    }

    [TestMethod]
    public void Decode_ConnectWithADuplicateProperty_IsProtocolError0x82()
    {
        var fault = FaultOf(Packet(Connect, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, 0x02, 0x00, 0x3C, 0x04, 0x17, 0x01, 0x17, 0x01, 0x00, 0x00));

        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("2.2.2.2"), fault);
    }

    [TestMethod]
    public void Decode_ConnectWithAPropertyOnlyConnackCarries_IsMalformed0x81()
    {
        var fault = FaultOf(Packet(Connect, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, 0x02, 0x00, 0x3C, 0x02, 0x24, 0x01, 0x00, 0x00));

        Assert.AreEqual(Mqtt5PacketFault.Malformed("2.2.2.2"), fault);
    }

    [TestMethod]
    public void Decode_ConnectWithAWillPropertyNotAllowedInTheWill_IsMalformed()
    {
        var fault = FaultOf(Packet(Connect, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, 0x06, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x02, 0x23, 0x00));

        Assert.AreEqual(Mqtt5PacketFault.Malformed("2.2.2.2"), fault);
    }

    [TestMethod]
    public void Decode_ConnectAcknowledgementWithReservedFlags_IsMalformed()
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-3.2.2-1]"), FaultOf(Packet(0x20, 0x02, 0x00, 0x00)));
    }

    [TestMethod]
    public void Decode_ConnectAcknowledgementWithATrailingByte_IsMalformed()
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("3.2.3"), FaultOf(Packet(0x20, 0x00, 0x00, 0x00, 0xFF)));
    }

    [TestMethod]
    public void Decode_PublishWithBothQualityOfServiceBitsSet_IsMalformed()
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-3.3.1-4]"), FaultOf(Packet(0x36, 0x00, 0x01, 0x61, 0x00, 0x01, 0x00)));
    }

    [TestMethod]
    public void Decode_PublishWithDuplicateAtQualityOfService0_IsProtocolError()
    {
        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("[MQTT-3.3.1-2]"), FaultOf(Packet(0x38, 0x00, 0x01, 0x61, 0x00)));
    }

    [TestMethod]
    [DataRow((byte)0x23, DisplayName = "#")]
    [DataRow((byte)0x2B, DisplayName = "+")]
    public void Decode_PublishTopicNameWithAWildcard_IsMalformed(byte wildcard)
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-3.3.2-2]"), FaultOf(Packet(0x30, 0x00, 0x03, 0x61, 0x2F, wildcard, 0x00)));
    }

    [TestMethod]
    public void Decode_PublishAtQualityOfService1WithPacketIdentifier0_IsProtocolError()
    {
        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("[MQTT-2.2.1-3]"), FaultOf(Packet(0x32, 0x00, 0x01, 0x61, 0x00, 0x00, 0x00)));
    }

    [TestMethod]
    public void Decode_PublishWithAPropertyNotAllowedInPublish_IsMalformed()
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("2.2.2.2"), FaultOf(Packet(0x30, 0x00, 0x01, 0x61, 0x02, 0x21, 0x00)));
    }

    [TestMethod]
    public void Decode_SubscribeWithNoTopicFilter_IsProtocolError()
    {
        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("[MQTT-3.8.3-2]"), FaultOf(Packet(0x82, 0x00, 0x01, 0x00)));
    }

    [TestMethod]
    [DataRow((byte)0x40, DisplayName = "bit 6")]
    [DataRow((byte)0x80, DisplayName = "bit 7")]
    public void Decode_SubscribeOptionsWithReservedBits_IsMalformed(byte options)
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-3.8.3-5]"), FaultOf(Packet(0x82, 0x00, 0x01, 0x00, 0x00, 0x01, 0x61, options)));
    }

    [TestMethod]
    [DataRow((byte)0x03, DisplayName = "Maximum QoS 3")]
    [DataRow((byte)0x30, DisplayName = "Retain Handling 3")]
    public void Decode_SubscribeOptionsWithValue3_IsProtocolError(byte options)
    {
        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("3.8.3.1"), FaultOf(Packet(0x82, 0x00, 0x01, 0x00, 0x00, 0x01, 0x61, options)));
    }

    [TestMethod]
    public void Decode_UnsubscribeWithNoTopicFilter_IsProtocolError()
    {
        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("[MQTT-3.10.3-2]"), FaultOf(Packet(0xA2, 0x00, 0x01, 0x00)));
    }

    [TestMethod]
    public void Decode_PingRequestWithABody_IsMalformed()
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("3.12.2"), FaultOf(Packet(0xC0, 0x00)));
    }

    [TestMethod]
    public void Decode_PublishAcknowledgementWithATrailingByte_IsMalformed()
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("3.4.2"), FaultOf(Packet(0x40, 0x00, 0x01, 0x00, 0x00, 0xFF)));
    }

    [TestMethod]
    public void Decode_DisconnectWithATrailingByte_IsMalformed()
    {
        Assert.AreEqual(Mqtt5PacketFault.Malformed("3.14.2"), FaultOf(Packet(0xE0, 0x00, 0x00, 0xFF)));
    }

    [TestMethod]
    public void Decode_DisconnectWithOnlyAReasonCode_DecodesIt()
    {
        var packet = (Mqtt5ReasonCodeAndPropertiesPacket)Mqtt5PacketDecoder.Decode(0xE0, new byte[] { 0x8B }).Packet!;

        Assert.AreEqual(Mqtt5ReasonCode.ServerShuttingDown, packet.ReasonCode);
        Assert.IsEmpty(packet.Properties);
    }

    // CONNECT's variable header with the given Connect Flags, no properties, and an empty Client Identifier.
    private static byte[] ConnectBody(byte connectFlags) =>
        [0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, connectFlags, 0x00, 0x3C, 0x00, 0x00, 0x00];
}
