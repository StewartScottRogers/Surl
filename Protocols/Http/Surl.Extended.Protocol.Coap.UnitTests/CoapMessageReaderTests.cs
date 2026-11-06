namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapMessageReader"/> on RFC 7252 section 3's option format and its message format
/// errors, from bytes written by hand.
/// </summary>
[TestClass]
public sealed class CoapMessageReaderTests
{
    [TestMethod]
    [DataRow(12, new byte[] { 0xC0 })]
    [DataRow(13, new byte[] { 0xD0, 0x00 })]
    [DataRow(268, new byte[] { 0xD0, 0xFF })]
    [DataRow(269, new byte[] { 0xE0, 0x00, 0x00 })]
    [DataRow(1000, new byte[] { 0xE0, 0x02, 0xDB })]
    public void Read_OptionDelta_ReadsTheNumberFromTheNibbleAndItsExtension(int number, byte[] option)
    {
        var message = ReadMessage([0x50, 0x01, 0x00, 0x01, .. option]);

        Assert.AreEqual(number, message.Options.Single().Number);
        Assert.IsTrue(message.Options.Single().Value.IsEmpty);
    }

    [TestMethod]
    [DataRow(12, new byte[] { 0x1C })]
    [DataRow(13, new byte[] { 0x1D, 0x00 })]
    [DataRow(269, new byte[] { 0x1E, 0x00, 0x00 })]
    public void Read_OptionLength_ReadsThatManyValueBytes(int length, byte[] optionHeader)
    {
        var message = ReadMessage([0x50, 0x01, 0x00, 0x01, .. optionHeader, .. new byte[length]]);

        Assert.AreEqual(1, message.Options.Single().Number);
        Assert.AreEqual(length, message.Options.Single().Value.Length);
        Assert.IsTrue(message.Payload.IsEmpty);
    }

    [TestMethod]
    public void Read_SeveralOptions_AddsEachDeltaToThePreviousNumber()
    {
        var message = ReadMessage([0x50, 0x01, 0x00, 0x01, 0x31, 0x61, 0x81, 0x62, 0x01, 0x63, 0xFF, 0x2A]);

        CollectionAssert.AreEqual(new[] { 3, 11, 11 }, message.Options.Select(option => option.Number).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x62 }, message.Options[1].Value.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x63 }, message.Options[2].Value.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x2A }, message.Payload.ToArray());
    }

    [TestMethod]
    public void Read_EmptyMessage_IsAMessage()
    {
        var message = ReadMessage([0x40, 0x00, 0x12, 0x34]);

        Assert.IsTrue(message.Code.IsEmpty);
        Assert.AreEqual(CoapMessageType.Confirmable, message.Type);
        Assert.AreEqual((ushort)0x1234, message.MessageId);
    }

    [TestMethod]
    public void Read_DatagramShorterThanTheHeader_IsNotCoap()
    {
        var outcome = CoapMessageReader.Read([0x40, 0x01, 0x00]);

        Assert.AreEqual(CoapReadOutcomeKind.NotCoap, outcome.Kind);
        Assert.IsNull(outcome.Message);
        Assert.AreEqual("A datagram shorter than the 4-byte CoAP header.", outcome.Fault);
    }

    [TestMethod]
    [DataRow((byte)0x00, 0)]
    [DataRow((byte)0x80, 2)]
    [DataRow((byte)0xC0, 3)]
    public void Read_AnotherVersion_IsNotCoap(byte firstByte, int version)
    {
        var outcome = CoapMessageReader.Read([firstByte, 0x01, 0x00, 0x01]);

        Assert.AreEqual(CoapReadOutcomeKind.NotCoap, outcome.Kind);
        Assert.AreEqual($"A datagram of CoAP version {version}, not 1.", outcome.Fault);
    }

    [TestMethod]
    public void Read_PayloadMarkerWithNoPayload_IsAFormatError() =>
        AssertFormatError([0x40, 0x01, 0x00, 0x07, 0xFF], CoapMessageType.Confirmable, 7, "A payload marker with no payload.");

    [TestMethod]
    public void Read_OptionDelta15_IsAFormatError() =>
        AssertFormatError([0x50, 0x01, 0x00, 0x07, 0xF0], CoapMessageType.NonConfirmable, 7, "Option delta 15 is reserved outside the payload marker.");

    [TestMethod]
    public void Read_OptionLength15_IsAFormatError() =>
        AssertFormatError([0x40, 0x01, 0x00, 0x07, 0x1F], CoapMessageType.Confirmable, 7, "Option length 15 is reserved outside the payload marker.");

    [TestMethod]
    [DataRow(9)]
    [DataRow(15)]
    public void Read_TokenLength9To15_IsAFormatError(int tokenLength) =>
        AssertFormatError([(byte)(0x40 | tokenLength), 0x01, 0x00, 0x07, .. new byte[tokenLength]], CoapMessageType.Confirmable, 7, $"Token length {tokenLength} is reserved.");

    [TestMethod]
    public void Read_TokenPastTheDatagram_IsAFormatError() =>
        AssertFormatError([0x42, 0x01, 0x00, 0x07, 0xAA], CoapMessageType.Confirmable, 7, "The token runs past the end of the datagram.");

    [TestMethod]
    public void Read_OptionValuePastTheDatagram_IsAFormatError() =>
        AssertFormatError([0x40, 0x01, 0x00, 0x07, 0xB2, 0x61], CoapMessageType.Confirmable, 7, "Option 11 runs past the end of the datagram.");

    [TestMethod]
    [DataRow(new byte[] { 0xD0 }, "delta")]
    [DataRow(new byte[] { 0xE0, 0x00 }, "delta")]
    [DataRow(new byte[] { 0x0D }, "length")]
    public void Read_OptionExtensionPastTheDatagram_IsAFormatError(byte[] option, string field) =>
        AssertFormatError([0x40, 0x01, 0x00, 0x07, .. option], CoapMessageType.Confirmable, 7, $"An option {field} extension runs past the end of the datagram.");

    [TestMethod]
    public void Read_OptionNumberPast65535_IsAFormatError() =>
        AssertFormatError([0x40, 0x01, 0x00, 0x07, 0xE0, 0xFF, 0xFF], CoapMessageType.Confirmable, 7, "Option number 65804 is past 65535.");

    [TestMethod]
    public void Read_EmptyMessageWithAToken_IsAFormatError() =>
        AssertFormatError([0x41, 0x00, 0x00, 0x07, 0xAA], CoapMessageType.Confirmable, 7, "An Empty message with a token or bytes after its Message ID.");

    [TestMethod]
    public void Read_EmptyMessageWithBytesAfterItsMessageId_IsAFormatError() =>
        AssertFormatError([0x60, 0x00, 0x00, 0x07, 0xFF, 0x01], CoapMessageType.Acknowledgement, 7, "An Empty message with a token or bytes after its Message ID.");

    [TestMethod]
    public void Read_CopiesTheBytes_SoTheDatagramMayBeReused()
    {
        byte[] datagram = [0x41, 0x01, 0x00, 0x01, 0x20, 0xB1, 0x61, 0xFF, 0x2A];

        var message = ReadMessage(datagram);
        Array.Clear(datagram);

        CollectionAssert.AreEqual(new byte[] { 0x20 }, message.Token.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x61 }, message.Options.Single().Value.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x2A }, message.Payload.ToArray());
    }

    private static CoapMessage ReadMessage(byte[] datagram)
    {
        var outcome = CoapMessageReader.Read(datagram);

        Assert.AreEqual(CoapReadOutcomeKind.Message, outcome.Kind, outcome.Fault);
        return outcome.Message!;
    }

    private static void AssertFormatError(byte[] datagram, CoapMessageType type, ushort messageId, string fault)
    {
        var outcome = CoapMessageReader.Read(datagram);

        Assert.AreEqual(CoapReadOutcomeKind.FormatError, outcome.Kind);
        Assert.IsNull(outcome.Message);
        Assert.AreEqual(type, outcome.Type);
        Assert.AreEqual(messageId, outcome.MessageId);
        Assert.AreEqual(fault, outcome.Fault);
    }
}
