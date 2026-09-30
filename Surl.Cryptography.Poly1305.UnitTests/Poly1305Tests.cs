namespace Surl.Cryptography.Poly1305;

/// <summary>
/// Pins <see cref="Poly1305" /> to RFC 8439's published vectors: section 2.5.2 and
/// Appendix A.3 test vectors #1 to #11. Every key, message and expected tag is copied from
/// the RFC (ADR-0003); the key is r then s.
/// </summary>
[TestClass]
public sealed class Poly1305Tests
{
    private const string ZeroBlock = "00000000000000000000000000000000";

    // RFC 8439 section 2.5.2: key, message ("Cryptographic Forum Research Group") and tag.
    private const string ForumKey = "85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b";

    private const string ForumMessage = "43727970746f6772617068696320466f72756d2052657365617263682047726f7570";

    private const string ForumTag = "a8061dc1305136c6c22b8baf0c0127a9";

    // RFC 8439 Appendix A.3 test vectors #2 and #3: the 375-byte "Any submission to the IETF ..." text.
    private const string IetfContribution =
        "416e79207375626d697373696f6e20746f20746865204945544620696e74656e6465642062792074686520436f6e7472696275746f7220666f72207075626c69636174696f6e20617320616c6c206f722070617274206f6620616e204945544620496e7465726e65742d4472616674206f722052464320616e6420616e792073746174656d656e74206d6164652077697468696e2074686520636f6e74657874206f6620616e204945544620616374697669747920697320636f6e7369646572656420616e20224945544620436f6e747269627574696f6e222e20537563682073746174656d656e747320696e636c756465206f72616c2073746174656d656e747320696e20494554462073657373696f6e732c2061732077656c6c206173207772697474656e20616e6420656c656374726f6e696320636f6d6d756e69636174696f6e73206d61646520617420616e792074696d65206f7220706c6163652c207768696368206172652061646472657373656420746f";

    // RFC 8439 Appendix A.3 test vector #4: the 127-byte "'Twas brillig ..." text.
    private const string Jabberwocky =
        "2754776173206272696c6c69672c20616e642074686520736c6974687920746f7665730a446964206779726520616e642067696d626c6520696e2074686520776162653a0a416c6c206d696d737920776572652074686520626f726f676f7665732c0a416e6420746865206d6f6d65207261746873206f757467726162652e";

    // RFC 8439 section 2.5.2.
    [TestMethod]
    public void ComputeTag_Rfc8439Section252Example_ReturnsThePublishedTag() =>
        AssertTag(ForumKey, ForumMessage, ForumTag);

    // RFC 8439 Appendix A.3 test vector #1: all-zero key, 64 zero bytes.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector1_ReturnsThePublishedTag() =>
        AssertTag(ZeroBlock + ZeroBlock, ZeroBlock + ZeroBlock + ZeroBlock + ZeroBlock, ZeroBlock);

    // RFC 8439 Appendix A.3 test vector #2: r = 0, so the tag is s.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector2_ReturnsThePublishedTag() =>
        AssertTag(ZeroBlock + "36e5f6b5c5e06070f0efca96227a863e", IetfContribution, "36e5f6b5c5e06070f0efca96227a863e");

    // RFC 8439 Appendix A.3 test vector #3.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector3_ReturnsThePublishedTag() =>
        AssertTag("36e5f6b5c5e06070f0efca96227a863e" + ZeroBlock, IetfContribution, "f3477e7cd95417af89a6b8794c310cf0");

    // RFC 8439 Appendix A.3 test vector #4.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector4_ReturnsThePublishedTag() =>
        AssertTag("1c9240a5eb55d38af333888604f6b5f0473917c1402b80099dca5cbc207075c0", Jabberwocky, "4541669a7eaaee61e708dc7cbcc5eb62");

    // RFC 8439 Appendix A.3 test vector #5: h reaches 2^130 - 2, which the final reduction takes to 3.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector5_ReturnsThePublishedTag() =>
        AssertTag("02000000000000000000000000000000" + ZeroBlock, "ffffffffffffffffffffffffffffffff", "03000000000000000000000000000000");

    // RFC 8439 Appendix A.3 test vector #6: h + s wraps modulo 2^128.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector6_ReturnsThePublishedTag() =>
        AssertTag("02000000000000000000000000000000" + "ffffffffffffffffffffffffffffffff", "02000000000000000000000000000000", "03000000000000000000000000000000");

    // RFC 8439 Appendix A.3 test vector #7.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector7_ReturnsThePublishedTag() =>
        AssertTag(
            "01000000000000000000000000000000" + ZeroBlock,
            "ffffffffffffffffffffffffffffffff" + "f0ffffffffffffffffffffffffffffff" + "11000000000000000000000000000000",
            "05000000000000000000000000000000");

    // RFC 8439 Appendix A.3 test vector #8: h reduces to 0, so the tag is 0.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector8_ReturnsThePublishedTag() =>
        AssertTag(
            "01000000000000000000000000000000" + ZeroBlock,
            "ffffffffffffffffffffffffffffffff" + "fbfefefefefefefefefefefefefefefe" + "01010101010101010101010101010101",
            ZeroBlock);

    // RFC 8439 Appendix A.3 test vector #9: h is 2^130 - 6, one below 2^130 - 5.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector9_ReturnsThePublishedTag() =>
        AssertTag("02000000000000000000000000000000" + ZeroBlock, "fdffffffffffffffffffffffffffffff", "faffffffffffffffffffffffffffffff");

    // RFC 8439 Appendix A.3 test vector #10.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector10_ReturnsThePublishedTag() =>
        AssertTag(
            "01000000000000000400000000000000" + ZeroBlock,
            "e33594d7505e43b90000000000000000" + "3394d7505e4379cd0100000000000000" + ZeroBlock + "01000000000000000000000000000000",
            "14000000000000005500000000000000");

    // RFC 8439 Appendix A.3 test vector #11.
    [TestMethod]
    public void ComputeTag_Rfc8439A3Vector11_ReturnsThePublishedTag() =>
        AssertTag(
            "01000000000000000400000000000000" + ZeroBlock,
            "e33594d7505e43b90000000000000000" + "3394d7505e4379cd0100000000000000" + ZeroBlock,
            "13000000000000000000000000000000");

    // RFC 8439 section 2.5: with no blocks the accumulator stays 0, so the tag is s, the
    // second half of section 2.5.2's key.
    [TestMethod]
    public void ComputeTag_EmptyMessage_ReturnsS() =>
        AssertTag(ForumKey, string.Empty, "0103808afb0db2fd4abff6af4149f51b");

    [TestMethod]
    public void ComputeTag_IntoASpan_WritesTheSameTagAsTheReturningForm()
    {
        byte[] tag = new byte[Poly1305.TagSize];

        Poly1305.ComputeTag(Convert.FromHexString(ForumKey), Convert.FromHexString(ForumMessage), tag);

        Assert.AreEqual(ForumTag, Convert.ToHexStringLower(tag));
    }

    [TestMethod]
    [DataRow(31, 16)]
    [DataRow(33, 16)]
    [DataRow(32, 15)]
    [DataRow(32, 17)]
    public void ComputeTag_KeyOrTagOfTheWrongLength_ThrowsArgumentException(int keyLength, int tagLength) =>
        Assert.ThrowsExactly<ArgumentException>(() => Poly1305.ComputeTag(new byte[keyLength], [], new byte[tagLength]));

    [TestMethod]
    public void ComputeTag_ReturningFormWithAShortKey_ThrowsArgumentException() =>
        Assert.ThrowsExactly<ArgumentException>(() => Poly1305.ComputeTag(new byte[Poly1305.KeySize - 1], []));

    private static void AssertTag(string key, string message, string expectedTag) =>
        Assert.AreEqual(expectedTag, Convert.ToHexStringLower(Poly1305.ComputeTag(Convert.FromHexString(key), Convert.FromHexString(message))));
}
