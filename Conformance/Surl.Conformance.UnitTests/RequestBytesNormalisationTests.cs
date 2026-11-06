using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// ADR-0089 decision 4's request-byte rules, through <see cref="CurlPortResultNormaliser.NormaliseRequestBytes"/>.
/// The requests are written from each protocol's specification (RFC 9110, RFC 7616, RFC 2831, RFC 959,
/// RFC 2428, RFC 2046, MS-NLMP), not taken from any client.
/// </summary>
[TestClass]
public sealed class RequestBytesNormalisationTests
{
    private const int ClientFacingPort = 50101;

    private const string DigestMd5Response =
        "username=\"{0}\",realm=\"r\",nonce=\"n\",cnonce=\"{1}\",nc=00000001,qop=auth,digest-uri=\"smtp/127.0.0.1\",response={2}";

    [TestMethod]
    public void NormaliseRequestBytes_ReplacesTheClientFacingPortInHost()
    {
        Assert.AreEqual("Host: 127.0.0.1:<port>\r\n", Normalise("Host: 127.0.0.1:50101\r\n", 50101));
    }

    [TestMethod]
    public void NormaliseRequestBytes_ReplacesTheUserAgentValueInAnyCase()
    {
        Assert.AreEqual(
            "GET / HTTP/1.1\r\nuser-agent: <user-agent>\r\nX-User-Agent: kept\r\n",
            Normalise("GET / HTTP/1.1\r\nuser-agent: curl/8.21.0\r\nX-User-Agent: kept\r\n"));
    }

    [TestMethod]
    public void NormaliseRequestBytes_ReplacesTheClientPortInFtpPortAndEprt()
    {
        Assert.AreEqual(
            "PORT 127,0,0,1,<port>\r\nEPRT |1|127.0.0.1|<port>|\r\nEPRT |2|::1|<port>|\r\nRETR 1,2,3,4,5,6\r\n",
            Normalise("PORT 127,0,0,1,195,80\r\nEPRT |1|127.0.0.1|50000|\r\nEPRT |2|::1|50001|\r\nRETR 1,2,3,4,5,6\r\n"));
    }

    [TestMethod]
    public void NormaliseRequestBytes_ReplacesEveryOccurrenceOfEachMultipartBoundary_QuotedOrNot()
    {
        Assert.AreEqual(
            "Content-Type: multipart/form-data; boundary=<boundary>\r\n\r\n--<boundary>\r\nx\r\n--<boundary>--\r\n"
            + "Content-Type: multipart/mixed; boundary=\"<boundary>\"\r\n--<boundary>--\r\n",
            Normalise(
                "Content-Type: multipart/form-data; boundary=------abc123\r\n\r\n--------abc123\r\nx\r\n--------abc123--\r\n"
                + "Content-Type: multipart/mixed; boundary=\"q9\"\r\n--q9--\r\n"));
    }

    [TestMethod]
    public void NormaliseRequestBytes_ReplacesDigestNonceCnonceAndResponse_KeepingEveryOtherParameter()
    {
        Assert.AreEqual(
            "Authorization: Digest username=\"u\", nonce=\"<nonce>\", uri=\"/\", cnonce=\"<cnonce>\", nc=00000001, qop=auth, response=\"<response>\"\r\n",
            Normalise("Authorization: Digest username=\"u\", nonce=\"n1\", uri=\"/\", cnonce=\"Zm9vYmFy\", nc=00000001, qop=auth, response=\"6629fae49393a05397450978507c4ef1\"\r\n"));
    }

    [TestMethod]
    public void NormaliseRequestBytes_SaslDigestMd5Responses_DifferingOnlyInCnonceAndResponse_Agree()
    {
        var upstream = Normalise(DigestMd5Line("u", "OA6MHXh6VqTrRk", "d388dad90d4bbd760a152321f2143af7"));
        var port = Normalise(DigestMd5Line("u", "a1b2c3d4e5f6a7", "ea40f60335c427b5527b84dbabcdfffd"));
        var otherUser = Normalise(DigestMd5Line("v", "OA6MHXh6VqTrRk", "d388dad90d4bbd760a152321f2143af7"));

        Assert.AreEqual(upstream, port);
        Assert.AreNotEqual(upstream, otherUser);
        StringAssert.Contains(Encoding.Latin1.GetString(Convert.FromBase64String(upstream.TrimEnd('\r', '\n'))), "cnonce=\"<cnonce>\"");
    }

    [TestMethod]
    public void NormaliseRequestBytes_Base64TokenThatIsNeitherNtlmNorDigest_OrNotBase64_IsLeftAlone()
    {
        var plain = Base64Line("username=\"u\",realm=\"r\",nonce=\"n\"");

        Assert.AreEqual(plain, Normalise(plain));
        Assert.AreEqual("abcdefghijklmnopqrstu\r\n", Normalise("abcdefghijklmnopqrstu\r\n"));
    }

    [TestMethod]
    public void NormaliseRequestBytes_NtlmAuthenticateInBase64_DifferingOnlyInResponsesSessionKeyAndMic_Agree()
    {
        var upstream = Normalise("Authorization: NTLM " + Convert.ToBase64String(NtlmAuthenticate(0x11, "user")) + "\r\n");
        var port = Normalise("Authorization: NTLM " + Convert.ToBase64String(NtlmAuthenticate(0x22, "user")) + "\r\n");
        var otherUser = Normalise("Authorization: NTLM " + Convert.ToBase64String(NtlmAuthenticate(0x11, "uzer")) + "\r\n");

        Assert.AreEqual(upstream, port);
        Assert.AreNotEqual(upstream, otherUser);
    }

    [TestMethod]
    public void NormaliseRequestBytes_RawNtlmAuthenticatesInsideABinaryMessage_DifferingOnlyInRandomness_Agree()
    {
        static byte[] Smb(byte random) => [0x00, 0x00, 0x01, 0x00, .. NtlmAuthenticate(random, "user"), .. NtlmAuthenticate(random, "user")];

        var upstream = CurlPortResultNormaliser.NormaliseRequestBytes(Smb(0x11), ClientFacingPort);
        var port = CurlPortResultNormaliser.NormaliseRequestBytes(Smb(0x22), ClientFacingPort);

        CollectionAssert.AreEqual(upstream, port);
        CollectionAssert.AreNotEqual(Smb(0x11), upstream);
    }

    [TestMethod]
    public void NormaliseRequestBytes_NtlmAuthenticateWithoutAMic_KeepsWhatFollowsTheFixedFields()
    {
        var message = NtlmAuthenticate(0x11, "user", payloadOffset: 64);

        var normalised = CurlPortResultNormaliser.NormaliseRequestBytes(message, ClientFacingPort);

        Assert.IsLessThan(0, normalised.AsSpan(64, 24).IndexOfAnyExcept((byte)0));
        CollectionAssert.AreEqual("DDuser"u8.ToArray(), normalised[108..114]);
    }

    [TestMethod]
    public void NormaliseRequestBytes_NtlmAuthenticateCutShortOrPointingOutside_ClearsOnlyWhatItHolds()
    {
        var message = NtlmAuthenticate(0x11, "user");
        var tooShort = message[..40];
        var cut = message[..80];
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(24), 4000);

        var normalised = CurlPortResultNormaliser.NormaliseRequestBytes(message, ClientFacingPort);

        CollectionAssert.AreEqual(tooShort, CurlPortResultNormaliser.NormaliseRequestBytes(tooShort, ClientFacingPort));
        CollectionAssert.AreEqual(cut, CurlPortResultNormaliser.NormaliseRequestBytes(cut, ClientFacingPort));
        Assert.IsLessThan(0, normalised.AsSpan(88, 24).IndexOfAnyExcept((byte)0));
        Assert.AreEqual(0x11, normalised[88 + 24]);
    }

    [TestMethod]
    public void NormaliseRequestBytes_NtlmAuthenticateWithNoPayload_ClearsTheMicPlace()
    {
        var message = new byte[88];
        "NTLMSSP\0\u0003\0\0\0"u8.CopyTo(message);
        message.AsSpan(72).Fill(0x5A);

        var normalised = CurlPortResultNormaliser.NormaliseRequestBytes(message, ClientFacingPort);

        Assert.IsLessThan(0, normalised.AsSpan(72).IndexOfAnyExcept((byte)0));
    }

    [TestMethod]
    public void NormaliseRequestBytes_KeepsEveryOtherByte()
    {
        byte[] bytes = [0x00, 0xFF, 0x80, 0x0A];

        CollectionAssert.AreEqual(bytes, CurlPortResultNormaliser.NormaliseRequestBytes(bytes, ClientFacingPort));
    }

    /// <summary>
    /// An NTLM AUTHENTICATE_MESSAGE (MS-NLMP 2.2.1.3) whose LM and NT responses, encrypted session key
    /// and MIC are filled with <paramref name="random"/>: by default with a Version and a MIC, so its
    /// payload starts at 88; at 64 it has neither.
    /// </summary>
    private static byte[] NtlmAuthenticate(byte random, string user, int payloadOffset = 88)
    {
        var message = new byte[payloadOffset + 24 + 20 + 2 + 4 + 16];
        "NTLMSSP\0\u0003\0\0\0"u8.CopyTo(message);
        var at = payloadOffset;
        at = Field(message, 12, at, 24, random);
        at = Field(message, 20, at, 20, random);
        at = Field(message, 28, at, 2, (byte)'D');
        Encoding.ASCII.GetBytes(user).CopyTo(message, at);
        at = Field(message, 36, at, 4, null);
        Field(message, 52, at, 16, random);
        if (payloadOffset >= 88)
        {
            message.AsSpan(72, 16).Fill(random);
        }

        return message;
    }

    private static int Field(byte[] message, int field, int at, int length, byte? fill)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(field), (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(field + 2), (ushort)length);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(field + 4), (uint)at);
        if (fill is { } value)
        {
            message.AsSpan(at, length).Fill(value);
        }

        return at + length;
    }

    private static string DigestMd5Line(string user, string cnonce, string response) =>
        Base64Line(string.Format(CultureInfo.InvariantCulture, DigestMd5Response, user, cnonce, response));

    private static string Base64Line(string text) => Convert.ToBase64String(Encoding.Latin1.GetBytes(text)) + "\r\n";

    private static string Normalise(string request, int clientFacingPort = ClientFacingPort) =>
        Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseRequestBytes(Encoding.Latin1.GetBytes(request), clientFacingPort));
}
