using System.Text;

namespace Surl.Compression;

/// <summary>
/// The Snappy encoder: exact bytes for a small input, written from <c>format_description.txt</c>,
/// and round trips through <see cref="SnappyDecoder"/> for every copy and literal form.
/// </summary>
[TestClass]
public sealed class SnappyEncoderTests
{
    [TestMethod]
    public void Encode_RepeatedFourBytes_WritesALiteralAndAOneByteOffsetCopy()
    {
        byte[] expected = [0x08, 0x0C, .. "abcd"u8, 0x01, 0x04];

        byte[] encoded = SnappyEncoder.Encode("abcdabcd"u8);

        CollectionAssert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void Encode_Empty_WritesOnlyTheZeroLength()
    {
        CollectionAssert.AreEqual(new byte[] { 0x00 }, SnappyEncoder.Encode([]));
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(100)]
    [DataRow(70_000)]
    public void Encode_Incompressible_RoundTripsThroughTheDecoder(int length)
    {
        AssertRoundTrips(CompressionTestInputs.Incompressible(length, length));
    }

    [TestMethod]
    public void Encode_Repetitive_RoundTripsAndIsSmaller()
    {
        byte[] input = CompressionTestInputs.Repetitive(10_000);

        byte[] encoded = SnappyEncoder.Encode(input);

        Assert.IsLessThan(input.Length / 4, encoded.Length);
        AssertRoundTrips(input);
    }

    [TestMethod]
    [DataRow(3_000)]
    [DataRow(70_000)]
    public void Encode_RepeatFarBack_RoundTripsThroughTheDecoder(int distance)
    {
        AssertRoundTrips(CompressionTestInputs.RepeatedAfter(distance));
    }

    [TestMethod]
    public void Encode_ShortMatchesAtSmallAndLargeOffsets_RoundTrip()
    {
        byte[] input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 2000).Select(index => $"k{index % 97}=v{index % 89};")));

        AssertRoundTrips(input);
    }

    private static void AssertRoundTrips(byte[] input)
    {
        DecompressionResult result = SnappyDecoder.Decode(SnappyEncoder.Encode(input), input.Length);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        CollectionAssert.AreEqual(input, result.Output);
    }
}
