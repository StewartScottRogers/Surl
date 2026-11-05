using System.Text;

namespace Surl.Compression;

/// <summary>
/// Raw Snappy decoding against inputs written by hand from google/snappy's
/// <c>format_description.txt</c>: the varint length, literals and each copy form.
/// </summary>
[TestClass]
public sealed class SnappyDecoderTests
{
    private const int Unbounded = int.MaxValue;

    [TestMethod]
    public void Decode_LiteralWithLengthInTag_ReturnsTheLiteral()
    {
        AssertDecodes("hello", [0x05, 0x10, .. "hello"u8]);
    }

    [TestMethod]
    public void Decode_LiteralWithOneLengthByte_ReturnsTheLiteral()
    {
        byte[] literal = Enumerable.Range(0, 61).Select(index => (byte)index).ToArray();

        DecompressionResult result = SnappyDecoder.Decode([0x3D, 0xF0, 0x3C, .. literal], Unbounded);

        CollectionAssert.AreEqual(literal, result.Output);
    }

    [TestMethod]
    public void Decode_CopyWithOneByteOffset_RepeatsEarlierOutput()
    {
        AssertDecodes("abcdabcd", [0x08, 0x0C, .. "abcd"u8, 0x01, 0x04]);
    }

    [TestMethod]
    public void Decode_CopyWithOneByteOffsetAndHighOffsetBits_ReachesPast256Bytes()
    {
        byte[] literal = Enumerable.Range(0, 260).Select(index => (byte)index).ToArray();

        // Length 264; a literal of 260 with a 2-byte length (tag 61); a copy with a 1-byte offset,
        // length 4, offset 0x104, its high bits 001 in the tag's top three bits.
        DecompressionResult result = SnappyDecoder.Decode([0x88, 0x02, 0xF4, 0x03, 0x01, .. literal, 0x21, 0x04], Unbounded);

        CollectionAssert.AreEqual(literal.Concat(literal[..4]).ToArray(), result.Output);
    }

    [TestMethod]
    public void Decode_CopyWithTwoByteOffset_RepeatsEarlierOutput()
    {
        AssertDecodes("abcdabcd", [0x08, 0x0C, .. "abcd"u8, 0x0E, 0x04, 0x00]);
    }

    [TestMethod]
    public void Decode_CopyWithFourByteOffset_RepeatsEarlierOutput()
    {
        AssertDecodes("abcdabcd", [0x08, 0x0C, .. "abcd"u8, 0x0F, 0x04, 0x00, 0x00, 0x00]);
    }

    [TestMethod]
    public void Decode_CopyLongerThanItsOffset_RepeatsWhatItHasJustWritten()
    {
        AssertDecodes("abababab", [0x08, 0x04, .. "ab"u8, 0x09, 0x02]);
    }

    [TestMethod]
    public void Decode_CopyWithOffsetZero_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x08, 0x0C, .. "abcd"u8, 0x01, 0x00]);
    }

    [TestMethod]
    public void Decode_CopyWithOffsetBeforeTheOutputsStart_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x08, 0x0C, .. "abcd"u8, 0x01, 0x05]);
    }

    [TestMethod]
    public void Decode_OutputShorterThanDeclared_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x08, 0x0C, .. "abcd"u8]);
    }

    [TestMethod]
    public void Decode_OutputLongerThanDeclared_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x02, 0x0C, .. "abcd"u8]);
    }

    [TestMethod]
    public void Decode_TruncatedLiteral_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x05, 0x10, .. "he"u8]);
    }

    [TestMethod]
    public void Decode_TruncatedCopyOffset_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x08, 0x0C, .. "abcd"u8, 0x0E, 0x04]);
    }

    [TestMethod]
    public void Decode_EmptyInput_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, []);
    }

    [TestMethod]
    public void Decode_LengthVarintLongerThanFiveBytes_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x80, 0x80, 0x80, 0x80, 0x80, 0x00]);
    }

    [TestMethod]
    public void Decode_ZeroLength_ReturnsNothing()
    {
        AssertDecodes(string.Empty, [0x00]);
    }

    [TestMethod]
    public void Decode_DeclaredLengthPastTheBound_StopsWithTheBoundOutcome()
    {
        DecompressionResult result = SnappyDecoder.Decode([0x05, 0x10, .. "hello"u8], 4);

        Assert.AreEqual(DecompressionOutcome.OutputBoundPassed, result.Outcome);
        Assert.IsEmpty(result.Output);
    }

    [TestMethod]
    public void Decode_DeclaredLengthAtTheBound_Decodes()
    {
        DecompressionResult result = SnappyDecoder.Decode([0x05, 0x10, .. "hello"u8], 5);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome);
        Assert.AreEqual(string.Empty, result.Reason);
    }

    [TestMethod]
    public void Decode_NegativeBound_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SnappyDecoder.Decode([0x00], -1));
    }

    private static void AssertDecodes(string expected, byte[] input)
    {
        DecompressionResult result = SnappyDecoder.Decode(input, Unbounded);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(result.Output));
    }

    private static void AssertRefused(DecompressionOutcome expected, byte[] input)
    {
        DecompressionResult result = SnappyDecoder.Decode(input, Unbounded);

        Assert.AreEqual(expected, result.Outcome);
        Assert.IsFalse(string.IsNullOrEmpty(result.Reason));
        Assert.IsEmpty(result.Output);
    }
}
