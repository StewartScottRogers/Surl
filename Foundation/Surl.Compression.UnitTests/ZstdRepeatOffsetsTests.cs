namespace Surl.Compression;

/// <summary>
/// The repeat offsets (RFC 8878, section 3.1.1.5): the Offset_Value the encoder picks for each kind
/// of offset, resolved by a decoder's twin back to that offset, leaving both alike.
/// </summary>
[TestClass]
public sealed class ZstdRepeatOffsetsTests
{
    [TestMethod]
    [DataRow(1, 5, 1L, DisplayName = "the first repeat offset after literals")]
    [DataRow(4, 5, 2L, DisplayName = "the second repeat offset after literals")]
    [DataRow(8, 5, 3L, DisplayName = "the third repeat offset after literals")]
    [DataRow(9, 5, 12L, DisplayName = "a new offset after literals")]
    [DataRow(4, 0, 1L, DisplayName = "the second repeat offset after no literals")]
    [DataRow(8, 0, 2L, DisplayName = "the third repeat offset after no literals")]
    [DataRow(1, 0, 4L, DisplayName = "the first repeat offset after no literals is a new offset")]
    [DataRow(9, 0, 12L, DisplayName = "a new offset after no literals")]
    public void ValueFor_EachKindOfOffset_PicksTheValueTheDecoderResolvesBack(int offset, int literalLength, long expectedValue)
    {
        ZstdRepeatOffsets encoder = new();
        ZstdRepeatOffsets decoder = new();

        long value = encoder.ValueFor(offset, literalLength);

        Assert.AreEqual(expectedValue, value);
        Assert.AreEqual(offset, decoder.Resolve(value, literalLength));
        AssertAlike(encoder, decoder);
    }

    [TestMethod]
    public void ValueFor_TheFirstRepeatOffsetLessOne_AfterNoLiteralsIsCode3()
    {
        ZstdRepeatOffsets encoder = new();
        ZstdRepeatOffsets decoder = new();
        decoder.Resolve(encoder.ValueFor(20, 3), 3);

        long value = encoder.ValueFor(19, 0);

        Assert.AreEqual(3L, value);
        Assert.AreEqual(19, decoder.Resolve(value, 0));
        AssertAlike(encoder, decoder);
    }

    [TestMethod]
    public void Copy_IsUpdatedApartFromTheOriginal()
    {
        ZstdRepeatOffsets original = new();

        ZstdRepeatOffsets copy = original.Copy();
        copy.ValueFor(100, 1);

        Assert.AreEqual(100, copy[0]);
        Assert.AreEqual(1, original[0]);
    }

    private static void AssertAlike(ZstdRepeatOffsets expected, ZstdRepeatOffsets actual)
    {
        for (int index = 0; index < ZstdRepeatOffsets.Count; index++)
        {
            Assert.AreEqual(expected[index], actual[index], $"repeat offset {index}");
        }
    }
}
