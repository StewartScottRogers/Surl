namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapMessageWriter"/> on RFC 7252 section 3.1's option format: deltas and lengths
/// in a nibble below 13, with one extension byte from 13, and two from 269.
/// </summary>
[TestClass]
public sealed class CoapMessageWriterTests
{
    [TestMethod]
    public void Write_NullMessage_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CoapMessageWriter.Write(null!));

    [TestMethod]
    [DataRow(12, new byte[] { 0xC0 })]
    [DataRow(13, new byte[] { 0xD0, 0x00 })]
    [DataRow(268, new byte[] { 0xD0, 0xFF })]
    [DataRow(269, new byte[] { 0xE0, 0x00, 0x00 })]
    [DataRow(65535, new byte[] { 0xE0, 0xFE, 0xF2 })]
    public void Write_OptionDelta_UsesTheNibbleAndItsExtension(int number, byte[] option)
    {
        var message = new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Get, 1, options: [new CoapOption(number, ReadOnlyMemory<byte>.Empty)]);

        byte[] expected = [0x50, 0x01, 0x00, 0x01, .. option];

        CollectionAssert.AreEqual(expected, CoapMessageWriter.Write(message));
    }

    [TestMethod]
    [DataRow(12, new byte[] { 0x1C })]
    [DataRow(13, new byte[] { 0x1D, 0x00 })]
    [DataRow(269, new byte[] { 0x1E, 0x00, 0x00 })]
    [DataRow(CoapOption.MaxValueLength, new byte[] { 0x1E, 0xFF, 0xFF })]
    public void Write_OptionLength_UsesTheNibbleAndItsExtension(int length, byte[] optionHeader)
    {
        var message = new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Get, 1, options: [new CoapOption(1, new byte[length])]);

        var written = CoapMessageWriter.Write(message);

        CollectionAssert.AreEqual(optionHeader, written[4..(4 + optionHeader.Length)]);
        Assert.HasCount(4 + optionHeader.Length + length, written);
    }

    [TestMethod]
    public void Write_OptionsGivenOutOfOrder_WritesThemInOrderOfNumberKeepingRepeatsInOrder()
    {
        var message = new CoapMessage(
            CoapMessageType.Confirmable,
            CoapCode.Get,
            0x0102,
            options: [CoapOption.FromString(11, "a"), CoapOption.FromString(3, "h"), CoapOption.FromString(11, "b")]);

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x01, 0x01, 0x02, 0x31, 0x68, 0x81, 0x61, 0x01, 0x62 }, CoapMessageWriter.Write(message));
    }

    [TestMethod]
    public void Write_NoPayload_WritesNoPayloadMarker()
    {
        var message = new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.NotFound, 0x0102, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        CollectionAssert.AreEqual(new byte[] { 0x68, 0x84, 0x01, 0x02, 1, 2, 3, 4, 5, 6, 7, 8 }, CoapMessageWriter.Write(message));
    }
}
