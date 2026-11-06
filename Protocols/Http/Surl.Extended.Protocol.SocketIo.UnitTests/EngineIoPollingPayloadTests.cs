using System.Text;

namespace Surl.Extended.Protocol.SocketIo.UnitTests;

[TestClass]
public sealed class EngineIoPollingPayloadTests
{
    [TestMethod]
    public void TrySplit_TextAndBinaryPacketsSeparatedByRecordSeparator_ReadsEachAndJoinsBackByteForByte()
    {
        var payload = Encoding.UTF8.GetBytes("4hello\u001e4€\u001ebAQIDBA==");
        var packets = new List<EngineIoPacket>();

        Assert.IsTrue(EngineIoPollingPayload.TrySplit(payload, packets));

        Assert.HasCount(3, packets);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(packets[0].Data.Span));
        Assert.AreEqual("€", Encoding.UTF8.GetString(packets[1].Data.Span));
        Assert.IsTrue(packets[2].IsBinary);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, packets[2].Data.ToArray());
        CollectionAssert.AreEqual(payload, EngineIoPollingPayload.Join(packets));
    }

    [TestMethod]
    public void TrySplit_OnePacket_ReadsIt()
    {
        var packets = new List<EngineIoPacket>();

        Assert.IsTrue(EngineIoPollingPayload.TrySplit("2"u8.ToArray(), packets));

        Assert.AreEqual(EngineIoPacketType.Ping, packets.Single().Type);
    }

    [TestMethod]
    [DataRow("4hello\u001e")]
    [DataRow("4hello\u001e\u001e2")]
    [DataRow("4hello\u001e8")]
    public void TrySplit_AnEmptyOrMalformedPacket_IsMalformed(string payload)
    {
        Assert.IsFalse(EngineIoPollingPayload.TrySplit(Encoding.UTF8.GetBytes(payload), []));
    }

    [TestMethod]
    public void Join_NoPackets_IsEmpty()
    {
        Assert.IsEmpty(EngineIoPollingPayload.Join([]));
    }

    [TestMethod]
    public void NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EngineIoPollingPayload.Join(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => EngineIoPollingPayload.TrySplit("2"u8.ToArray(), null!));
    }
}
