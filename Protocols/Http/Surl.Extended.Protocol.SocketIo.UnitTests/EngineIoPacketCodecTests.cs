using System.Text;

namespace Surl.Extended.Protocol.SocketIo.UnitTests;

[TestClass]
public sealed class EngineIoPacketCodecTests
{
    [TestMethod]
    [DataRow("0{\"sid\":\"lv_VI97HAXpY6yYWAAAC\"}", EngineIoPacketType.Open, "{\"sid\":\"lv_VI97HAXpY6yYWAAAC\"}")]
    [DataRow("1", EngineIoPacketType.Close, "")]
    [DataRow("2", EngineIoPacketType.Ping, "")]
    [DataRow("2probe", EngineIoPacketType.Ping, "probe")]
    [DataRow("3", EngineIoPacketType.Pong, "")]
    [DataRow("3probe", EngineIoPacketType.Pong, "probe")]
    [DataRow("4hello", EngineIoPacketType.Message, "hello")]
    [DataRow("4€", EngineIoPacketType.Message, "€")]
    [DataRow("5", EngineIoPacketType.Upgrade, "")]
    [DataRow("6", EngineIoPacketType.Noop, "")]
    public void TryReadText_EachPacketTypeOfTheSpecification_ReadsItAndWritesItBackByteForByte(
        string message, EngineIoPacketType type, string data)
    {
        var bytes = Encoding.UTF8.GetBytes(message);

        Assert.IsTrue(EngineIoPacketCodec.TryReadText(bytes, out var packet));

        Assert.AreEqual(type, packet!.Type);
        Assert.AreEqual(data, Encoding.UTF8.GetString(packet.Data.Span));
        Assert.IsFalse(packet.IsBinary);
        CollectionAssert.AreEqual(bytes, EngineIoPacketCodec.WriteWebSocketMessage(packet));
        CollectionAssert.AreEqual(bytes, EngineIoPacketCodec.WritePollingPacket(packet));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("7")]
    [DataRow("/")]
    [DataRow("hello")]
    public void TryReadText_EmptyOrTypeCharacterOutsideZeroToSix_IsMalformed(string message)
    {
        Assert.IsFalse(EngineIoPacketCodec.TryReadText(Encoding.UTF8.GetBytes(message), out var packet));
        Assert.IsNull(packet);
    }

    [TestMethod]
    public void ReadBinary_BinaryWebSocketMessage_IsABinaryMessagePacketSentBackAsItsBareBytes()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04];

        var packet = EngineIoPacketCodec.ReadBinary(data);

        Assert.AreEqual(EngineIoPacketType.Message, packet.Type);
        Assert.IsTrue(packet.IsBinary);
        CollectionAssert.AreEqual(data, EngineIoPacketCodec.WriteWebSocketMessage(packet));
    }

    [TestMethod]
    public void TryReadPollingPacket_BPrefixedBase64_IsTheBinaryMessageAndWritesBack()
    {
        var encoded = "bAQIDBA=="u8.ToArray();

        Assert.IsTrue(EngineIoPacketCodec.TryReadPollingPacket(encoded, out var packet));

        Assert.IsTrue(packet!.IsBinary);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04 }, packet.Data.ToArray());
        CollectionAssert.AreEqual(encoded, EngineIoPacketCodec.WritePollingPacket(packet));
    }

    [TestMethod]
    [DataRow("bAQI!")]
    [DataRow("bAQIDBA")]
    public void TryReadPollingPacket_BadBase64_IsMalformed(string encoded)
    {
        Assert.IsFalse(EngineIoPacketCodec.TryReadPollingPacket(Encoding.ASCII.GetBytes(encoded), out var packet));
        Assert.IsNull(packet);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("9")]
    public void TryReadPollingPacket_EmptyOrUnknownType_IsMalformed(string encoded)
    {
        Assert.IsFalse(EngineIoPacketCodec.TryReadPollingPacket(Encoding.ASCII.GetBytes(encoded), out _));
    }

    [TestMethod]
    public void Text_MakesATextPacketOfTheUtf8Text()
    {
        var packet = EngineIoPacket.Text(EngineIoPacketType.Message, "€");

        CollectionAssert.AreEqual("4€"u8.ToArray(), EngineIoPacketCodec.WriteWebSocketMessage(packet));
    }

    [TestMethod]
    public void Write_NullPacket_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EngineIoPacketCodec.WriteWebSocketMessage(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => EngineIoPacketCodec.WritePollingPacket(null!));
    }
}
