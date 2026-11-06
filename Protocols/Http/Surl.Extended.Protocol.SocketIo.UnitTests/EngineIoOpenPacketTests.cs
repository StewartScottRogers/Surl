using System.Text;

namespace Surl.Extended.Protocol.SocketIo.UnitTests;

[TestClass]
public sealed class EngineIoOpenPacketTests
{
    [TestMethod]
    public void Write_HandshakeExampleValues_IsTheExampleOpenPacketByteForByte()
    {
        var packet = EngineIoOpenPacket.Write(
            "lv_VI97HAXpY6yYWAAAC",
            ["websocket"],
            EngineIoOpenPacket.PingIntervalMilliseconds,
            EngineIoOpenPacket.PingTimeoutMilliseconds,
            1000000);

        Assert.AreEqual(
            "0{\"sid\":\"lv_VI97HAXpY6yYWAAAC\",\"upgrades\":[\"websocket\"],\"pingInterval\":25000,\"pingTimeout\":20000,\"maxPayload\":1000000}",
            Encoding.UTF8.GetString(EngineIoPacketCodec.WriteWebSocketMessage(packet)));
    }

    [TestMethod]
    public void Write_WebSocketSessionWithNoUpgrades_HasAnEmptyUpgradesArray()
    {
        var packet = EngineIoOpenPacket.Write("abc", [], 25000, 20000, 1048576);

        Assert.AreEqual(
            "0{\"sid\":\"abc\",\"upgrades\":[],\"pingInterval\":25000,\"pingTimeout\":20000,\"maxPayload\":1048576}",
            Encoding.UTF8.GetString(EngineIoPacketCodec.WriteWebSocketMessage(packet)));
    }

    [TestMethod]
    public void Write_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EngineIoOpenPacket.Write(null!, [], 1, 1, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => EngineIoOpenPacket.Write("a", null!, 1, 1, 1));
    }
}
