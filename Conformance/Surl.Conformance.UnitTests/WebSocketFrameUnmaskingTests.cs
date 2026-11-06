using System.Text;

namespace Surl.Conformance;

/// <summary>
/// ADR-0089 decision 4's WebSocket rule. The upgrade requests and frames are written from RFC 6455
/// (sections 4.1 and 5.2, and section 5.7's examples), not taken from any client.
/// </summary>
[TestClass]
public sealed class WebSocketFrameUnmaskingTests
{
    private const string Upgrade =
        "GET /chat HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
        + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n";

    private static readonly byte[] KeyOne = [0x37, 0xFA, 0x21, 0x3D];

    private static readonly byte[] KeyTwo = [0x01, 0x02, 0x03, 0x04];

    [TestMethod]
    public void Unmask_NoUpgradeRequest_CopiesTheBytesUnchanged()
    {
        var bytes = Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: h\r\n\r\n\x81\x85abcd");

        var unmasked = WebSocketFrameUnmasking.Unmask(bytes);

        CollectionAssert.AreEqual(bytes, unmasked);
        Assert.AreNotSame(bytes, unmasked);
    }

    [TestMethod]
    public void Unmask_MaskedFrame_UnmasksThePayloadAndZeroesTheKey()
    {
        // RFC 6455 section 5.7: a masked text message "Hello".
        var unmasked = WebSocketFrameUnmasking.Unmask(WithUpgrade(Frame(0x81, "Hello"u8.ToArray(), KeyOne)));

        CollectionAssert.AreEqual(WithUpgrade([0x81, 0x85, 0, 0, 0, 0, .. "Hello"u8.ToArray()]), unmasked);
    }

    [TestMethod]
    public void Unmask_SameFramesUnderDifferentKeys_Agree()
    {
        byte[] close = [0x03, 0xE9];

        CollectionAssert.AreEqual(
            WebSocketFrameUnmasking.Unmask(WithUpgrade([.. Frame(0x88, close, KeyOne), .. Frame(0x81, "a"u8.ToArray(), KeyTwo)])),
            WebSocketFrameUnmasking.Unmask(WithUpgrade([.. Frame(0x88, close, KeyTwo), .. Frame(0x81, "a"u8.ToArray(), KeyOne)])));
    }

    [TestMethod]
    public void Unmask_SixteenBitLengthFrame_UnmasksTheWholePayload()
    {
        var payload = new byte[300];
        payload.AsSpan().Fill((byte)'x');

        var unmasked = WebSocketFrameUnmasking.Unmask(WithUpgrade(Frame(0x82, payload, KeyOne)));

        CollectionAssert.AreEqual(WithUpgrade([0x82, 0xFE, 0x01, 0x2C, 0, 0, 0, 0, .. payload]), unmasked);
    }

    [TestMethod]
    public void Unmask_SixtyFourBitLengthFrameCutShort_UnmasksWhatIsThere()
    {
        byte[] header = [0x82, 0xFF, 0, 0, 0, 0, 0, 1, 0, 0];
        byte[] masked = [(byte)('a' ^ KeyOne[0]), (byte)('b' ^ KeyOne[1])];

        var unmasked = WebSocketFrameUnmasking.Unmask(WithUpgrade([.. header, .. KeyOne, .. masked]));

        CollectionAssert.AreEqual(WithUpgrade([.. header, 0, 0, 0, 0, (byte)'a', (byte)'b']), unmasked);
    }

    [TestMethod]
    public void Unmask_UnmaskedFrameBeforeAMaskedOne_KeepsItAndUnmasksTheNext()
    {
        byte[] plain = [0x81, 0x02, (byte)'h', (byte)'i'];

        var unmasked = WebSocketFrameUnmasking.Unmask(WithUpgrade([.. plain, .. Frame(0x81, "yo"u8.ToArray(), KeyOne)]));

        CollectionAssert.AreEqual(WithUpgrade([.. plain, 0x81, 0x82, 0, 0, 0, 0, (byte)'y', (byte)'o']), unmasked);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x81 })]
    [DataRow(new byte[] { 0x82, 0xFE, 0x01 })]
    [DataRow(new byte[] { 0x82, 0xFF, 0, 0, 0 })]
    public void Unmask_FrameHeaderCutShort_KeepsItsBytes(byte[] cut)
    {
        var unmasked = WebSocketFrameUnmasking.Unmask(WithUpgrade(cut));

        CollectionAssert.AreEqual(WithUpgrade(cut), unmasked);
    }

    [TestMethod]
    public void Unmask_MaskingKeyCutShort_ZeroesWhatThereIsOfIt()
    {
        var unmasked = WebSocketFrameUnmasking.Unmask(WithUpgrade([0x88, 0x82, 0x37, 0xFA]));

        CollectionAssert.AreEqual(WithUpgrade([0x88, 0x82, 0, 0]), unmasked);
    }

    [TestMethod]
    public void Unmask_TwoUpgradeRequests_UnmasksOnlyAfterTheLast()
    {
        var first = Encoding.Latin1.GetBytes(Upgrade);

        var unmasked = WebSocketFrameUnmasking.Unmask([.. first, .. WithUpgrade(Frame(0x81, "Hello"u8.ToArray(), KeyOne))]);

        byte[] expected = [.. first, .. WithUpgrade([0x81, 0x85, 0, 0, 0, 0, .. "Hello"u8.ToArray()])];
        CollectionAssert.AreEqual(expected, unmasked);
    }

    [TestMethod]
    public void NormaliseRequestBytes_UpgradeWithMaskedFrames_ReplacesTheKeyAndUnmasksTheFrames()
    {
        var request = WithUpgrade(Frame(0x88, [0x03, 0xE9], KeyOne));

        var normalised = Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseRequestBytes(request, 50101));

        Assert.AreEqual(
            Upgrade.Replace("dGhlIHNhbXBsZSBub25jZQ==", "<websocket-key>", StringComparison.Ordinal) + "\x88\x82\0\0\0\0\x03\xE9",
            normalised);
    }

    private static byte[] WithUpgrade(byte[] frames) => [.. Encoding.Latin1.GetBytes(Upgrade), .. frames];

    /// <summary>A client frame (RFC 6455 section 5.2): FIN and opcode, the masked length, the key and the masked payload.</summary>
    private static byte[] Frame(byte finAndOpcode, byte[] payload, byte[] key)
    {
        byte[] length = payload.Length < 126
            ? [(byte)(0x80 | payload.Length)]
            : [0xFE, (byte)(payload.Length >> 8), (byte)payload.Length];
        var masked = new byte[payload.Length];
        for (var index = 0; index < payload.Length; index++)
        {
            masked[index] = (byte)(payload[index] ^ key[index % 4]);
        }

        return [finAndOpcode, .. length, .. key, .. masked];
    }
}
