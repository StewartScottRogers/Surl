namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The MessagePack hub protocol's messages (HubProtocol.md "MessagePack (MsgPack) encoding"): each
/// type 1 to 9 written out by hand from the layouts it gives and read and written back.
/// </summary>
[TestClass]
public sealed class HubMessagePackReaderTests
{
    // "xyz" is a3 78 79 7a, "method" is a6 6d 65 74 68 6f 64.
    [TestMethod]
    [DataRow("96 01 80 a3 78 79 7a a6 6d 65 74 68 6f 64 91 2a 90", DisplayName = "1 Invocation [1,{},\"xyz\",\"method\",[42],[]]")]
    [DataRow("96 01 80 c0 a6 6d 65 74 68 6f 64 91 2a 90", DisplayName = "1 Invocation without an invocationId")]
    [DataRow("96 01 80 a3 78 79 7a a6 6d 65 74 68 6f 64 91 2a 91 a2 73 31", DisplayName = "1 Invocation with a stream ID")]
    [DataRow("94 02 80 a3 78 79 7a 2a", DisplayName = "2 StreamItem [2,{},\"xyz\",42]")]
    [DataRow("95 03 80 a3 78 79 7a 01 a4 6f 6f 70 73", DisplayName = "3 Completion with an error")]
    [DataRow("94 03 80 a3 78 79 7a 02", DisplayName = "3 Completion with no result")]
    [DataRow("95 03 80 a3 78 79 7a 03 2a", DisplayName = "3 Completion with a result")]
    [DataRow("96 04 80 a3 78 79 7a a6 6d 65 74 68 6f 64 91 2a 90", DisplayName = "4 StreamInvocation")]
    [DataRow("93 05 80 a3 78 79 7a", DisplayName = "5 CancelInvocation [5,{},\"xyz\"]")]
    [DataRow("91 06", DisplayName = "6 Ping [6]")]
    [DataRow("93 07 c0 c2", DisplayName = "7 Close without an error")]
    [DataRow("93 07 a4 6f 6f 70 73 c2", DisplayName = "7 Close with an error")]
    [DataRow("93 07 c0 c3", DisplayName = "7 Close allowing a reconnect")]
    [DataRow("92 08 05", DisplayName = "8 Ack [8,5]")]
    [DataRow("92 09 cc c8", DisplayName = "9 Sequence [9,200]")]
    public void TryRead_ThenWrite_HubProtocolLayout_WritesTheSameBytesWithALengthPrefix(string hex)
    {
        var body = MessagePackHex.Bytes(hex);

        Assert.IsTrue(HubMessagePackReader.TryRead(body, out var message));

        CollectionAssert.AreEqual(MessagePackHex.Prefixed(hex), HubMessagePackWriter.Write(message!));
    }

    [TestMethod]
    public void TryRead_Invocation_IsItsMembers()
    {
        var body = MessagePackHex.Bytes("96 01 80 a3 78 79 7a a4 45 63 68 6f 92 a2 68 69 81 a1 6b 90 91 a1 73");

        Assert.IsTrue(HubMessagePackReader.TryRead(body, out var message));

        var invocation = (HubInvocation)message!;
        Assert.IsFalse(invocation.IsStreaming);
        Assert.AreEqual("xyz", invocation.InvocationId);
        Assert.AreEqual("Echo", invocation.Target);
        CollectionAssert.AreEqual(MessagePackHex.Bytes("a2 68 69"), invocation.Arguments[0].Utf8Json.ToArray());
        CollectionAssert.AreEqual(MessagePackHex.Bytes("81 a1 6b 90"), invocation.Arguments[1].Utf8Json.ToArray());
        CollectionAssert.AreEqual(new[] { "s" }, invocation.StreamIds.ToArray());
    }

    [TestMethod]
    public void TryRead_InvocationOfAnOlderClientWithNoStreamIds_HasNone()
    {
        var body = MessagePackHex.Bytes("95 01 80 a3 78 79 7a a6 6d 65 74 68 6f 64 91 2a");

        Assert.IsTrue(HubMessagePackReader.TryRead(body, out var message));

        Assert.IsEmpty(((HubInvocation)message!).StreamIds);
    }

    [TestMethod]
    public void TryRead_HeadersAndElementsThisServerDoesNotKnow_AreSkipped()
    {
        var headers = MessagePackHex.Bytes("95 02 81 a1 61 a1 62 a1 31 2a c0");
        var ping = MessagePackHex.Bytes("93 06 01 90");

        Assert.IsTrue(HubMessagePackReader.TryRead(headers, out var item));
        Assert.IsTrue(HubMessagePackReader.TryRead(ping, out var message));

        Assert.AreEqual("1", ((HubStreamItem)item!).InvocationId);
        Assert.IsInstanceOfType<HubPing>(message);
    }

    [TestMethod]
    public void TryRead_CloseOfAnOlderClientWithNoAllowReconnect_DoesNotAllowIt()
    {
        Assert.IsTrue(HubMessagePackReader.TryRead(MessagePackHex.Bytes("92 07 a1 65"), out var message));

        Assert.AreEqual(new HubClose("e", AllowReconnect: false), message);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("90", DisplayName = "an empty array")]
    [DataRow("c0", DisplayName = "not an array")]
    [DataRow("91 c0", DisplayName = "a type that is not an integer")]
    [DataRow("91 0a", DisplayName = "type 10")]
    [DataRow("91 00", DisplayName = "type 0")]
    [DataRow("91 ff", DisplayName = "a negative type")]
    [DataRow("91 01", DisplayName = "an Invocation with no headers")]
    [DataRow("92 01 80", DisplayName = "an Invocation with no invocationId")]
    [DataRow("93 01 80 a1 31", DisplayName = "an Invocation with no target")]
    [DataRow("94 01 80 a1 31 a1 74", DisplayName = "an Invocation with no arguments")]
    [DataRow("92 02 81 a1", DisplayName = "headers cut short")]
    [DataRow("94 02 80 a1 31 d9", DisplayName = "a StreamItem cut short")]
    [DataRow("91 07", DisplayName = "a Close with no error element")]
    [DataRow("94 03 80 a1 31 01", DisplayName = "a Completion with an error kind and no error")]
    [DataRow("96 01 a1 61 a1 31 a1 74 90 90", DisplayName = "headers that are not a map")]
    [DataRow("96 04 80 c0 a1 74 90 90", DisplayName = "a StreamInvocation with a nil invocationId")]
    [DataRow("96 01 80 01 a1 74 90 90", DisplayName = "an invocationId that is not a string")]
    [DataRow("96 01 80 a1 ff a1 74 90 90", DisplayName = "an invocationId that is not UTF-8")]
    [DataRow("96 01 80 a1 31 01 90 90", DisplayName = "a target that is not a string")]
    [DataRow("96 01 80 a1 31 c0 90 90", DisplayName = "a nil target")]
    [DataRow("96 01 80 a1 31 a1 74 c0 90", DisplayName = "arguments that are not an array")]
    [DataRow("95 01 80 a1 31 a1 74 91 d9", DisplayName = "an argument cut short")]
    [DataRow("96 01 80 a1 31 a1 74 90 c0", DisplayName = "stream IDs that are not an array")]
    [DataRow("96 01 80 a1 31 a1 74 90 91 01", DisplayName = "a stream ID that is not a string")]
    [DataRow("93 02 80 a1 31", DisplayName = "a StreamItem with no item")]
    [DataRow("94 02 80 a1 ff 01", DisplayName = "a StreamItem whose id is not UTF-8")]
    [DataRow("92 05 80", DisplayName = "a CancelInvocation with no id")]
    [DataRow("93 03 80 a1 31", DisplayName = "a Completion with no result kind")]
    [DataRow("94 03 c0 a1 31 02", DisplayName = "a Completion whose headers are not a map")]
    [DataRow("94 03 80 c0 02", DisplayName = "a Completion with a nil id")]
    [DataRow("94 03 80 a1 31 c0", DisplayName = "a Completion whose result kind is not an integer")]
    [DataRow("94 03 80 a1 31 04", DisplayName = "a Completion with result kind 4")]
    [DataRow("95 03 80 a1 31 01 01", DisplayName = "a Completion whose error is not a string")]
    [DataRow("94 03 80 a1 31 03", DisplayName = "a Completion with kind 3 and no result")]
    [DataRow("92 07 01", DisplayName = "a Close whose error is not a string")]
    [DataRow("93 07 c0 01", DisplayName = "a Close whose allowReconnect is not a boolean")]
    [DataRow("92 08 ff", DisplayName = "a negative Ack")]
    [DataRow("92 08 c0", DisplayName = "an Ack that is not an integer")]
    [DataRow("91 08", DisplayName = "an Ack with no sequenceId")]
    [DataRow("92 09 ff", DisplayName = "a negative Sequence")]
    [DataRow("91 06 00", DisplayName = "a byte after the message")]
    [DataRow("93 06 d9 00", DisplayName = "an element past the type cut short")]
    public void TryRead_ProtocolError_IsRefused(string hex)
    {
        Assert.IsFalse(HubMessagePackReader.TryRead(MessagePackHex.Bytes(hex), out var message));
        Assert.IsNull(message);
    }

    [TestMethod]
    public void TryRead_IdsPastTheLimit_AreRefusedAndAtTheLimitAreRead()
    {
        var atLimit = new string('a', HubMessageJsonReader.MaxIdLength);
        var past = atLimit + "a";

        Assert.IsTrue(HubMessagePackReader.TryRead(Body(new HubCancelInvocation(atLimit)), out var message));
        Assert.AreEqual(new HubCancelInvocation(atLimit), message);
        Assert.IsFalse(HubMessagePackReader.TryRead(Body(new HubCancelInvocation(past)), out _));
        Assert.IsFalse(HubMessagePackReader.TryRead(Body(new HubInvocation(false, "1", "t", [], [past])), out _));
    }

    // The message without its VarInt length prefix.
    private static ReadOnlyMemory<byte> Body(HubMessage message)
    {
        var framed = HubMessagePackWriter.Write(message);
        var prefixLength = Array.FindIndex(framed, value => value < 0x80) + 1;

        return framed.AsMemory(prefixLength);
    }
}
