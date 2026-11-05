namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapMessage"/>: what a message may hold (RFC 7252 sections 3 and 4.1).
/// </summary>
[TestClass]
public sealed class CoapMessageTests
{
    [TestMethod]
    public void Constructor_UndefinedType_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CoapMessage((CoapMessageType)4, CoapCode.Get, 1));

    [TestMethod]
    public void Constructor_TokenOf9Bytes_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CoapMessage(CoapMessageType.Confirmable, CoapCode.Get, 1, new byte[9]));

    [TestMethod]
    public void Constructor_EmptyMessageWithAToken_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => new CoapMessage(CoapMessageType.Confirmable, CoapCode.Empty, 1, new byte[1]));

    [TestMethod]
    public void Constructor_EmptyMessageWithAnOption_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => new CoapMessage(CoapMessageType.Confirmable, CoapCode.Empty, 1, options: [CoapOption.FromUInt(12, 0)]));

    [TestMethod]
    public void Constructor_EmptyMessageWithAPayload_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => new CoapMessage(CoapMessageType.Confirmable, CoapCode.Empty, 1, payload: new byte[1]));

    [TestMethod]
    public void EmptyReset_IsAnEmptyRstWithTheMessageId()
    {
        var reset = CoapMessage.EmptyReset(0xBEEF);

        Assert.AreEqual(CoapMessageType.Reset, reset.Type);
        Assert.IsTrue(reset.Code.IsEmpty);
        Assert.AreEqual((ushort)0xBEEF, reset.MessageId);
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x00, 0xBE, 0xEF }, CoapMessageWriter.Write(reset));
    }
}
