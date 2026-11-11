using System.Text;

namespace Surl.Extended.Protocol.SocketIo.UnitTests;

[TestClass]
public sealed class SocketIoPacketReaderTests
{
    private const long NoAckId = -1;

    [TestMethod]
    [DataRow("0", SocketIoPacketType.Connect, "/", 0, NoAckId, "")]
    [DataRow("0/admin,{\"sid\":\"oSO0OpakMV_3jnilAAAA\"}", SocketIoPacketType.Connect, "/admin", 0, NoAckId, "{\"sid\":\"oSO0OpakMV_3jnilAAAA\"}")]
    [DataRow("4{\"message\":\"Not authorized\"}", SocketIoPacketType.ConnectError, "/", 0, NoAckId, "{\"message\":\"Not authorized\"}")]
    [DataRow("2[\"foo\"]", SocketIoPacketType.Event, "/", 0, NoAckId, "[\"foo\"]")]
    [DataRow("2/admin,[\"bar\"]", SocketIoPacketType.Event, "/admin", 0, NoAckId, "[\"bar\"]")]
    [DataRow("212[\"foo\"]", SocketIoPacketType.Event, "/", 0, 12L, "[\"foo\"]")]
    [DataRow("3/admin,13[\"bar\"]", SocketIoPacketType.Ack, "/admin", 0, 13L, "[\"bar\"]")]
    [DataRow("1", SocketIoPacketType.Disconnect, "/", 0, NoAckId, "")]
    [DataRow("1/admin,", SocketIoPacketType.Disconnect, "/admin", 0, NoAckId, "")]
    [DataRow("2[\"hello\",1]", SocketIoPacketType.Event, "/", 0, NoAckId, "[\"hello\",1]")]
    [DataRow("21[\"hello\"]", SocketIoPacketType.Event, "/", 0, 1L, "[\"hello\"]")]
    [DataRow("3/admin,1[]", SocketIoPacketType.Ack, "/admin", 0, 1L, "[]")]
    [DataRow("51-[\"baz\",{\"_placeholder\":true,\"num\":0}]", SocketIoPacketType.BinaryEvent, "/", 1, NoAckId, "[\"baz\",{\"_placeholder\":true,\"num\":0}]")]
    [DataRow("52-/admin,[\"baz\",{\"_placeholder\":true,\"num\":0},{\"_placeholder\":true,\"num\":1}]", SocketIoPacketType.BinaryEvent, "/admin", 2, NoAckId, "[\"baz\",{\"_placeholder\":true,\"num\":0},{\"_placeholder\":true,\"num\":1}]")]
    [DataRow("61-15[\"bar\",{\"_placeholder\":true,\"num\":0}]", SocketIoPacketType.BinaryAck, "/", 1, 15L, "[\"bar\",{\"_placeholder\":true,\"num\":0}]")]
    [DataRow("51-[\"event\",{\"_placeholder\":true,\"num\":0}]", SocketIoPacketType.BinaryEvent, "/", 1, NoAckId, "[\"event\",{\"_placeholder\":true,\"num\":0}]")]
    public void TryRead_EachExampleOfTheSpecification_ReadsItAndWritesItBackByteForByte(
        string text, SocketIoPacketType type, string name, int attachments, long ackId, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        Assert.IsTrue(SocketIoPacketReader.TryRead(bytes, out var packet));

        Assert.AreEqual(type, packet!.Type);
        Assert.AreEqual(name, packet.Namespace);
        Assert.AreEqual(attachments, packet.AttachmentCount);
        Assert.AreEqual(ackId == NoAckId ? null : ackId, packet.AckId);
        Assert.AreEqual(payload, Encoding.UTF8.GetString(packet.Payload.Span));
        CollectionAssert.AreEqual(bytes, SocketIoPacketWriter.Write(packet));
    }

    [TestMethod]
    public void TryRead_BinaryEventInsideAnEngineIoMessage_ReadsTheSocketIoPacketAfterTheEngineIoType()
    {
        var message = "451-[\"event\",{\"_placeholder\":true,\"num\":0}]"u8.ToArray();
        Assert.IsTrue(EngineIoPacketCodec.TryReadText(message, out var engineIo));

        Assert.IsTrue(SocketIoPacketReader.TryRead(engineIo!.Data, out var packet));

        Assert.AreEqual(EngineIoPacketType.Message, engineIo.Type);
        Assert.AreEqual(SocketIoPacketType.BinaryEvent, packet!.Type);
        Assert.AreEqual(1, packet.AttachmentCount);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("7[\"a\"]", DisplayName = "type 7")]
    [DataRow("x", DisplayName = "type not a digit")]
    [DataRow("/[\"a\"]", DisplayName = "type character below 0")]
    [DataRow("5[\"a\",{\"_placeholder\":true,\"num\":0}]", DisplayName = "binary without count")]
    [DataRow("51[\"a\",{\"_placeholder\":true,\"num\":0}]", DisplayName = "count without dash")]
    [DataRow("51", DisplayName = "count at end")]
    [DataRow("50-[\"a\"]", DisplayName = "count 0")]
    [DataRow("565-[\"a\"]", DisplayName = "count 65")]
    [DataRow("5100-[\"a\"]", DisplayName = "count of three digits")]
    [DataRow("2/admin[\"a\"]", DisplayName = "namespace without comma")]
    [DataRow("1/admin", DisplayName = "disconnect namespace without comma")]
    [DataRow("21234567890123456[\"a\"]", DisplayName = "ack id of 16 digits")]
    [DataRow("3[\"a\"]", DisplayName = "ack without id")]
    [DataRow("6/admin,1-[\"a\",{\"_placeholder\":true,\"num\":0}]", DisplayName = "binary ack without id")]
    [DataRow("1[]", DisplayName = "disconnect with payload")]
    [DataRow("2", DisplayName = "event without payload")]
    [DataRow("2[]", DisplayName = "event with empty array")]
    [DataRow("2[1]", DisplayName = "event name not a string")]
    [DataRow("2{\"a\":1}", DisplayName = "event payload an object")]
    [DataRow("2[\"a\"", DisplayName = "event JSON cut short")]
    [DataRow("2[\"a\"]x", DisplayName = "event JSON followed by more")]
    [DataRow("3 1[]", DisplayName = "ack id after a space")]
    [DataRow("31{}", DisplayName = "ack payload an object")]
    [DataRow("0[]", DisplayName = "connect payload an array")]
    [DataRow("0123", DisplayName = "connect payload a number")]
    [DataRow("4\"Not authorized\"", DisplayName = "connect error payload a string")]
    [DataRow("4", DisplayName = "connect error without payload")]
    public void TryRead_MalformedPacket_IsRefused(string text)
    {
        Assert.IsFalse(SocketIoPacketReader.TryRead(Encoding.UTF8.GetBytes(text), out var packet));
        Assert.IsNull(packet);
    }

    [TestMethod]
    public void TryRead_NamespaceOf128Bytes_IsRead()
    {
        var name = "/" + new string('a', 127);

        Assert.IsTrue(SocketIoPacketReader.TryRead(Encoding.UTF8.GetBytes($"2{name},[\"a\"]"), out var packet));

        Assert.AreEqual(name, packet!.Namespace);
    }

    [TestMethod]
    public void TryRead_NamespaceOf129Bytes_IsRefused()
    {
        var name = "/" + new string('a', 128);

        Assert.IsFalse(SocketIoPacketReader.TryRead(Encoding.UTF8.GetBytes($"2{name},[\"a\"]"), out _));
    }

    [TestMethod]
    public void TryRead_NamespaceNotUtf8_IsRefused()
    {
        byte[] text = [(byte)'1', (byte)'/', 0xFF, (byte)','];

        Assert.IsFalse(SocketIoPacketReader.TryRead(text, out _));
    }

    [TestMethod]
    public void TryRead_AckIdOf15Digits_IsRead()
    {
        Assert.IsTrue(SocketIoPacketReader.TryRead("2999999999999999[\"a\"]"u8.ToArray(), out var packet));

        Assert.AreEqual(999999999999999L, packet!.AckId);
    }

    [TestMethod]
    public void TryRead_SixtyFourAttachments_IsRead()
    {
        var placeholders = string.Join(",", Enumerable.Range(0, 64).Select(n => $"{{\"_placeholder\":true,\"num\":{n}}}"));

        Assert.IsTrue(SocketIoPacketReader.TryRead(Encoding.UTF8.GetBytes($"564-[\"a\",{placeholders}]"), out var packet));

        Assert.AreEqual(64, packet!.AttachmentCount);
    }

    [TestMethod]
    public void TryRead_PayloadNestedSixtyFourLevels_IsRead()
    {
        var payload = "[\"a\"," + new string('[', 63) + new string(']', 63) + "]";

        Assert.IsTrue(SocketIoPacketReader.TryRead(Encoding.UTF8.GetBytes("2" + payload), out _));
    }

    [TestMethod]
    public void TryRead_PayloadNestedSixtyFiveLevels_IsRefused()
    {
        var payload = "[\"a\"," + new string('[', 64) + new string(']', 64) + "]";

        Assert.IsFalse(SocketIoPacketReader.TryRead(Encoding.UTF8.GetBytes("2" + payload), out _));
    }

    [TestMethod]
    public void TryRead_AckIdOnAConnect_IsReadAsItsPayloadAndRefused()
    {
        Assert.IsFalse(SocketIoPacketReader.TryRead("01{}"u8.ToArray(), out _));
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("0{\"token\":\"abc\"}")]
    [DataRow("1/admin,")]
    [DataRow("2[\"a\"]")]
    [DataRow("31[]")]
    [DataRow("51-[\"a\",{\"_placeholder\":true,\"num\":0}]")]
    [DataRow("61-1[{\"_placeholder\":true,\"num\":0}]")]
    public void TryReadFromClient_EveryTypeButConnectError_IsRead(string text)
    {
        Assert.IsTrue(SocketIoPacketReader.TryReadFromClient(Encoding.UTF8.GetBytes(text), out var packet));
        Assert.IsNotNull(packet);
    }

    [TestMethod]
    [DataRow("4{\"message\":\"Not authorized\"}")]
    [DataRow("8")]
    public void TryReadFromClient_ConnectErrorOrMalformed_IsRefused(string text)
    {
        Assert.IsFalse(SocketIoPacketReader.TryReadFromClient(Encoding.UTF8.GetBytes(text), out var packet));
        Assert.IsNull(packet);
    }
}
