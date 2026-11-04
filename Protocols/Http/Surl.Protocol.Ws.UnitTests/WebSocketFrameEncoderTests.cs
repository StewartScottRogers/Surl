namespace Surl.Protocol.Ws;

[TestClass]
public sealed class WebSocketFrameEncoderTests
{
    [TestMethod]
    public void EncodeFrame_SingleFrameText_IsTheRfcsUnmaskedHello()
    {
        CollectionAssert.AreEqual(
            Rfc6455Examples.UnmaskedTextHello,
            WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Text, Rfc6455Examples.Hello));
    }

    [TestMethod]
    public void EncodeFrame_FragmentedText_IsTheRfcsTwoFrames()
    {
        CollectionAssert.AreEqual(
            Rfc6455Examples.FragmentedTextFirst,
            WebSocketFrameEncoder.EncodeFrame(fin: false, WebSocketOpcode.Text, "Hel"u8));
        CollectionAssert.AreEqual(
            Rfc6455Examples.FragmentedTextSecond,
            WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Continuation, "lo"u8));
    }

    [TestMethod]
    public void EncodeFrame_Ping_IsTheRfcsUnmaskedPing()
    {
        CollectionAssert.AreEqual(
            Rfc6455Examples.UnmaskedPingHello,
            WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Ping, Rfc6455Examples.Hello));
    }

    [TestMethod]
    public void EncodeFrame_256ByteBinary_HasTheRfcsSixteenBitHeader()
    {
        var payload = Rfc6455Examples.CountingPayload(256);

        CollectionAssert.AreEqual(
            Rfc6455Examples.Join(Rfc6455Examples.Binary256Header, payload),
            WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Binary, payload));
    }

    [TestMethod]
    public void EncodeFrame_64KiBBinary_HasTheRfcsSixtyFourBitHeader()
    {
        var payload = Rfc6455Examples.CountingPayload(65536);

        CollectionAssert.AreEqual(
            Rfc6455Examples.Join(Rfc6455Examples.Binary64KiBHeader, payload),
            WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Binary, payload));
    }

    [TestMethod]
    [DataRow(125, 2)]
    [DataRow(126, 4)]
    [DataRow(65535, 4)]
    [DataRow(65536, 10)]
    public void EncodeFrame_LengthAtEachEncodingBoundary_UsesTheMinimalEncoding(int payloadLength, int headerLength)
    {
        var frame = WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Binary, new byte[payloadLength]);

        Assert.AreEqual(headerLength + payloadLength, frame.Length);
    }

    [TestMethod]
    public void EncodeFrame_NeverSetsTheMaskBit()
    {
        var frame = WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Binary, new byte[200]);

        Assert.AreEqual(0, frame[1] & 0x80);
    }

    [TestMethod]
    public void EncodeFrame_ControlFrameWithFinClear_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => WebSocketFrameEncoder.EncodeFrame(fin: false, WebSocketOpcode.Ping, []));
    }

    [TestMethod]
    public void EncodeFrame_ControlFrameOver125Bytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Pong, new byte[126]));
    }

    [TestMethod]
    public void EncodeFrame_ControlFrameOf125Bytes_IsEncoded()
    {
        Assert.AreEqual(127, WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Pong, new byte[125]).Length);
    }

    [TestMethod]
    public void EncodeClose_CodeAndReason_IsCodeInNetworkOrderThenUtf8Reason()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0x88, 0x05, 0x03, 0xE8, 0x62, 0x79, 0x65 },
            WebSocketFrameEncoder.EncodeClose(1000, "bye"));
    }

    [TestMethod]
    public void EncodeClose_ReasonOf123Utf8Bytes_IsEncoded()
    {
        Assert.AreEqual(127, WebSocketFrameEncoder.EncodeClose(1001, new string('a', 123)).Length);
    }

    [TestMethod]
    public void EncodeClose_ReasonOver123Utf8Bytes_Throws()
    {
        // 62 two-byte code points: 124 bytes as UTF-8 though only 62 characters.
        Assert.ThrowsExactly<ArgumentException>(() => WebSocketFrameEncoder.EncodeClose(1000, new string('é', 62)));
    }

    [TestMethod]
    [DataRow(1005)]
    [DataRow(999)]
    public void EncodeClose_CodeNotAllowedOnTheWire_Throws(int closeCode)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WebSocketFrameEncoder.EncodeClose((ushort)closeCode, ""));
    }
}
