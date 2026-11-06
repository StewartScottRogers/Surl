using System.Text;

namespace Surl.Extended.Protocol.SocketIo.UnitTests;

[TestClass]
public sealed class SocketIoPacketWriterTests
{
    [TestMethod]
    public void Write_ConnectAnswerOnANamespace_PutsTheNamespaceAndCommaBeforeThePayload()
    {
        var packet = new SocketIoPacket(SocketIoPacketType.Connect, "/admin", 0, null, "{\"sid\":\"x\"}"u8.ToArray());

        Assert.AreEqual("0/admin,{\"sid\":\"x\"}", Encoding.UTF8.GetString(SocketIoPacketWriter.Write(packet)));
    }

    [TestMethod]
    public void Write_BinaryAckWithTwoAttachmentsAnIdAndANamespace_WritesCountDashNamespaceThenId()
    {
        var payload = "[{\"_placeholder\":true,\"num\":0},{\"_placeholder\":true,\"num\":1}]"u8.ToArray();
        var packet = new SocketIoPacket(SocketIoPacketType.BinaryAck, "/admin", 2, 7, payload);

        Assert.AreEqual(
            "62-/admin,7[{\"_placeholder\":true,\"num\":0},{\"_placeholder\":true,\"num\":1}]",
            Encoding.UTF8.GetString(SocketIoPacketWriter.Write(packet)));
    }

    [TestMethod]
    public void Write_NullPacket_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SocketIoPacketWriter.Write(null!));
    }
}
