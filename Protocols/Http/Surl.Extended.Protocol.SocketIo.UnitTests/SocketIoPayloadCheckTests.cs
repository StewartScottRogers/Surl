using System.Text;

namespace Surl.Extended.Protocol.SocketIo.UnitTests;

[TestClass]
public sealed class SocketIoPayloadCheckTests
{
    [TestMethod]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":0},{\"_placeholder\":true,\"num\":1}]", 2, DisplayName = "two in order")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":1},{\"_placeholder\":true,\"num\":0}]", 2, DisplayName = "two out of order")]
    [DataRow("[\"a\",{\"num\":0,\"_placeholder\":true}]", 1, DisplayName = "members swapped")]
    [DataRow("[\"a\",{\"file\":{\"_placeholder\":true,\"num\":0},\"name\":\"x\"}]", 1, DisplayName = "nested in an object")]
    [DataRow("[\"a\",[[{\"_placeholder\":true,\"num\":0}]]]", 1, DisplayName = "nested in arrays")]
    [DataRow("[\"a\",{\"\\u005fplaceholder\":true,\"num\":0}]", 1, DisplayName = "escaped member name")]
    public void IsValid_BinaryEventWithGoodPlaceholders_IsValid(string payload, int attachments)
    {
        Assert.IsTrue(SocketIoPayloadCheck.IsValid(SocketIoPacketType.BinaryEvent, Encoding.UTF8.GetBytes(payload), attachments));
    }

    [TestMethod]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":0}]", 2, DisplayName = "an attachment unused")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":0},{\"_placeholder\":true,\"num\":0}]", 2, DisplayName = "num used twice")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":1}]", 1, DisplayName = "num out of range")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":-1}]", 1, DisplayName = "num negative")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":0.5}]", 1, DisplayName = "num not an integer")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":\"0\"}]", 1, DisplayName = "num a string")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":[0]}]", 1, DisplayName = "num an array")]
    [DataRow("[\"a\",{\"_placeholder\":true}]", 1, DisplayName = "num missing")]
    [DataRow("[\"a\",{\"_placeholder\":false,\"num\":0}]", 1, DisplayName = "placeholder false")]
    [DataRow("[\"a\",{\"_placeholder\":{},\"num\":0}]", 1, DisplayName = "placeholder an object")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"num\":0,\"x\":1}]", 1, DisplayName = "a third member")]
    [DataRow("[\"a\",{\"_placeholder\":true,\"_placeholder\":true}]", 1, DisplayName = "placeholder twice")]
    [DataRow("[\"a\",{\"num\":0}]", 1, DisplayName = "no placeholder")]
    public void IsValid_BinaryEventWithBadPlaceholders_IsMalformed(string payload, int attachments)
    {
        Assert.IsFalse(SocketIoPayloadCheck.IsValid(SocketIoPacketType.BinaryEvent, Encoding.UTF8.GetBytes(payload), attachments));
    }

    [TestMethod]
    public void IsValid_PlainEventHoldingAPlaceholderObject_IsReadAsAnyJson()
    {
        var payload = "[\"a\",{\"_placeholder\":true,\"num\":7,\"x\":1}]"u8.ToArray();

        Assert.IsTrue(SocketIoPayloadCheck.IsValid(SocketIoPacketType.Event, payload, 0));
    }

    [TestMethod]
    [DataRow(SocketIoPacketType.Connect, "", true)]
    [DataRow(SocketIoPacketType.Connect, "{}", true)]
    [DataRow(SocketIoPacketType.Disconnect, "", true)]
    [DataRow(SocketIoPacketType.Disconnect, "{}", false)]
    [DataRow(SocketIoPacketType.Event, "[\"a\",1,{}]", true)]
    [DataRow(SocketIoPacketType.Ack, "", false)]
    [DataRow(SocketIoPacketType.Ack, "[]", true)]
    [DataRow(SocketIoPacketType.ConnectError, "{\"message\":\"x\"}", true)]
    [DataRow(SocketIoPacketType.Connect, " ", false)]
    [DataRow(SocketIoPacketType.Connect, "{}{}", false)]
    public void IsValid_PayloadShapePerType_IsJudged(SocketIoPacketType type, string payload, bool expected)
    {
        Assert.AreEqual(expected, SocketIoPayloadCheck.IsValid(type, Encoding.UTF8.GetBytes(payload), 0));
    }
}
