using System.Text;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// RFC 7252 Appendix A's example messages, encoded and decoded byte for byte. Each request is
/// <c>GET coap://server/temperature</c> (one Uri-Path option, delta 11, length 11) and each
/// response <c>2.05 Content</c> with the payload <c>22.3 C</c>; the bytes are written by hand
/// from the figures' header fields.
/// </summary>
[TestClass]
public sealed class CoapAppendixAExampleTests
{
    private const int UriPath = 11;

    private static readonly byte[] Temperature = Encoding.ASCII.GetBytes("temperature");
    private static readonly byte[] Reading = Encoding.ASCII.GetBytes("22.3 C");

    public static IEnumerable<object[]> Examples =>
    [
        // Figure 16: CON GET, MID 0x7d34, no token; ACK 2.05, MID 0x7d34.
        ["Figure 16 request", Request(CoapMessageType.Confirmable, 0x7D34, []), (byte[])[0x40, 0x01, 0x7D, 0x34, 0xBB, .. Temperature]],
        ["Figure 16 response", Response(CoapMessageType.Acknowledgement, 0x7D34, []), (byte[])[0x60, 0x45, 0x7D, 0x34, 0xFF, .. Reading]],

        // Figure 17: CON GET, MID 0x7d35, token 0x20; ACK 2.05 with the token.
        ["Figure 17 request", Request(CoapMessageType.Confirmable, 0x7D35, [0x20]), (byte[])[0x41, 0x01, 0x7D, 0x35, 0x20, 0xBB, .. Temperature]],
        ["Figure 17 response", Response(CoapMessageType.Acknowledgement, 0x7D35, [0x20]), (byte[])[0x61, 0x45, 0x7D, 0x35, 0x20, 0xFF, .. Reading]],

        // Figure 20: a separate response - Empty ACK MID 0x7d38, then CON 2.05 MID 0xad7b token 0x53, Empty ACK back.
        ["Figure 20 empty ACK", new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Empty, 0x7D38), (byte[])[0x60, 0x00, 0x7D, 0x38]],
        ["Figure 20 separate response", Response(CoapMessageType.Confirmable, 0xAD7B, [0x53]), (byte[])[0x41, 0x45, 0xAD, 0x7B, 0x53, 0xFF, .. Reading]],

        // Figure 21: the client rejects an unexpected separate response with RST MID 0xad7c.
        ["Figure 21 RST", CoapMessage.EmptyReset(0xAD7C), (byte[])[0x70, 0x00, 0xAD, 0x7C]],

        // Figure 22: NON GET MID 0x7d40 token 0x75; NON 2.05 MID 0xad7d token 0x75.
        ["Figure 22 request", Request(CoapMessageType.NonConfirmable, 0x7D40, [0x75]), (byte[])[0x51, 0x01, 0x7D, 0x40, 0x75, 0xBB, .. Temperature]],
        ["Figure 22 response", Response(CoapMessageType.NonConfirmable, 0xAD7D, [0x75]), (byte[])[0x51, 0x45, 0xAD, 0x7D, 0x75, 0xFF, .. Reading]],

        // Figure 23: server C's NON 4.04, MID 0x952a, token 0x86.
        ["Figure 23 not found", new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.NotFound, 0x952A, new byte[] { 0x86 }), (byte[])[0x51, 0x84, 0x95, 0x2A, 0x86]],
    ];

    [TestMethod]
    [DynamicData(nameof(Examples))]
    public void Write_AppendixAExample_WritesItsBytes(string example, CoapMessage message, byte[] expected) =>
        CollectionAssert.AreEqual(expected, CoapMessageWriter.Write(message), example);

    [TestMethod]
    [DynamicData(nameof(Examples))]
    public void Read_AppendixAExample_ReadsItsFieldsAndWritesTheSameBytesAgain(string example, CoapMessage expected, byte[] datagram)
    {
        var outcome = CoapMessageReader.Read(datagram);

        Assert.AreEqual(CoapReadOutcomeKind.Message, outcome.Kind, example);
        Assert.AreEqual(string.Empty, outcome.Fault);
        var message = outcome.Message!;
        Assert.AreEqual(expected.Type, message.Type);
        Assert.AreEqual(expected.Type, outcome.Type);
        Assert.AreEqual(expected.Code, message.Code);
        Assert.AreEqual(expected.MessageId, message.MessageId);
        Assert.AreEqual(expected.MessageId, outcome.MessageId);
        CollectionAssert.AreEqual(expected.Token.ToArray(), message.Token.ToArray());
        CollectionAssert.AreEqual(expected.Payload.ToArray(), message.Payload.ToArray());
        CollectionAssert.AreEqual(expected.Options.Select(option => option.Number).ToArray(), message.Options.Select(option => option.Number).ToArray());
        CollectionAssert.AreEqual(datagram, CoapMessageWriter.Write(message));
    }

    private static CoapMessage Request(CoapMessageType type, ushort messageId, byte[] token) =>
        new(type, CoapCode.Get, messageId, token, [new CoapOption(UriPath, Temperature)]);

    private static CoapMessage Response(CoapMessageType type, ushort messageId, byte[] token) =>
        new(type, CoapCode.Content, messageId, token, payload: Reading);
}
