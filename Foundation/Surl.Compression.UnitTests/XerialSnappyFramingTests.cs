using System.Text;

namespace Surl.Compression;

/// <summary>
/// snappy-java's xerial framing: inputs written by hand from its header and chunk layout, and the
/// encoder's output round-tripped.
/// </summary>
[TestClass]
public sealed class XerialSnappyFramingTests
{
    private static readonly byte[] Header = [0x82, .. "SNAPPY"u8, 0x00, 0, 0, 0, 1, 0, 0, 0, 1];

    private static readonly byte[] HelloChunk = [0, 0, 0, 7, 0x05, 0x10, .. "hello"u8];

    private static readonly byte[] AbcdTwiceChunk = [0, 0, 0, 8, 0x08, 0x0C, .. "abcd"u8, 0x01, 0x04];

    [TestMethod]
    public void DecodeFramedOrRaw_TwoChunks_ReturnsBothInOrder()
    {
        DecompressionResult result = XerialSnappyFraming.DecodeFramedOrRaw([.. Header, .. HelloChunk, .. AbcdTwiceChunk], int.MaxValue);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        Assert.AreEqual("helloabcdabcd", Encoding.ASCII.GetString(result.Output));
    }

    [TestMethod]
    public void DecodeFramedOrRaw_HeaderOnly_ReturnsNothing()
    {
        DecompressionResult result = XerialSnappyFraming.DecodeFramedOrRaw(Header, 0);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome);
        Assert.IsEmpty(result.Output);
    }

    [TestMethod]
    public void DecodeFramedOrRaw_NoMagic_DecodesRawSnappy()
    {
        DecompressionResult result = XerialSnappyFraming.DecodeFramedOrRaw([0x05, 0x10, .. "hello"u8], 5);

        Assert.AreEqual("hello", Encoding.ASCII.GetString(result.Output));
    }

    [TestMethod]
    public void DecodeFramedOrRaw_TruncatedHeader_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, Header[..12], int.MaxValue);
    }

    [TestMethod]
    public void DecodeFramedOrRaw_TruncatedChunkLength_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header, 0, 0], int.MaxValue);
    }

    [TestMethod]
    public void DecodeFramedOrRaw_NegativeChunkLength_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header, 0xFF, 0xFF, 0xFF, 0xFF], int.MaxValue);
    }

    [TestMethod]
    public void DecodeFramedOrRaw_ChunkLongerThanTheInput_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header, .. HelloChunk[..^1]], int.MaxValue);
    }

    [TestMethod]
    public void DecodeFramedOrRaw_MalformedChunk_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header, 0, 0, 0, 2, 0x08, 0x0C], int.MaxValue);
    }

    [TestMethod]
    public void DecodeFramedOrRaw_ChunksPastTheBound_StopWithTheBoundOutcome()
    {
        AssertRefused(DecompressionOutcome.OutputBoundPassed, [.. Header, .. HelloChunk, .. AbcdTwiceChunk], 10);
    }

    [TestMethod]
    public void DecodeFramedOrRaw_NegativeBound_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => XerialSnappyFraming.DecodeFramedOrRaw(Header, -1));
    }

    [TestMethod]
    public void HasMagic_HeaderAndRawSnappy_TellsThemApart()
    {
        Assert.IsTrue(XerialSnappyFraming.HasMagic(Header));
        Assert.IsFalse(XerialSnappyFraming.HasMagic([0x05, 0x10, .. "hello"u8]));
    }

    [TestMethod]
    public void Encode_Empty_WritesOnlyTheHeaderWithVersionOneAndCompatibleVersionOne()
    {
        CollectionAssert.AreEqual(Header, XerialSnappyFraming.Encode([]));
    }

    [TestMethod]
    public void Encode_RepetitiveInputOfSeveralChunks_RoundTripsAndIsSmaller()
    {
        byte[] input = CompressionTestInputs.Repetitive((XerialSnappyFraming.ChunkInputLength * 2) + 100);

        byte[] encoded = XerialSnappyFraming.Encode(input);
        DecompressionResult result = XerialSnappyFraming.DecodeFramedOrRaw(encoded, input.Length);

        Assert.IsLessThan(input.Length / 4, encoded.Length);
        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        CollectionAssert.AreEqual(input, result.Output);
    }

    private static void AssertRefused(DecompressionOutcome expected, byte[] input, int outputBound)
    {
        DecompressionResult result = XerialSnappyFraming.DecodeFramedOrRaw(input, outputBound);

        Assert.AreEqual(expected, result.Outcome);
        Assert.IsEmpty(result.Output);
    }
}
