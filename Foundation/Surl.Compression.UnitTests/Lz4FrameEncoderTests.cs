namespace Surl.Compression;

/// <summary>
/// The LZ4 frame encoder: the exact frame for empty input, from the frame format's header and end
/// mark, and round trips through <see cref="Lz4FrameDecoder"/>.
/// </summary>
[TestClass]
public sealed class Lz4FrameEncoderTests
{
    [TestMethod]
    public void Encode_Empty_WritesTheHeaderAndTheEndMark()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0x04, 0x22, 0x4D, 0x18, 0x60, 0x40, 0x82, 0, 0, 0, 0 },
            Lz4FrameEncoder.Encode([]));
    }

    [TestMethod]
    public void Encode_RepetitiveInputOfSeveralBlocks_RoundTripsAndIsSmaller()
    {
        byte[] input = CompressionTestInputs.Repetitive((Lz4FrameEncoder.BlockInputLength * 2) + 1000);

        byte[] encoded = Lz4FrameEncoder.Encode(input);

        Assert.IsLessThan(input.Length / 4, encoded.Length);
        AssertRoundTrips(input, encoded);
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(1000)]
    public void Encode_Incompressible_StoresItsBlockAndRoundTrips(int length)
    {
        byte[] input = CompressionTestInputs.Incompressible(length, length);

        byte[] encoded = Lz4FrameEncoder.Encode(input);

        Assert.AreEqual(0x80, encoded[10] & 0x80, "the block size's high bit marks it stored");
        AssertRoundTrips(input, encoded);
    }

    [TestMethod]
    public void Encode_LongLiteralsBetweenLongMatches_RoundTrip()
    {
        byte[] input = CompressionTestInputs.RepeatedAfter(3_000);

        AssertRoundTrips(input, Lz4FrameEncoder.Encode(input));
    }

    private static void AssertRoundTrips(byte[] input, byte[] encoded)
    {
        DecompressionResult result = Lz4FrameDecoder.Decode(encoded, input.Length);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        CollectionAssert.AreEqual(input, result.Output);
    }
}
