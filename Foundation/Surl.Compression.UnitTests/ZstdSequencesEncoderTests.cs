namespace Surl.Compression;

/// <summary>
/// The sequences encoder: a section of none, and sections whose sequence count takes one, two and
/// three bytes, each executed by <see cref="ZstdSequencesSection"/> back to the bytes it describes.
/// </summary>
[TestClass]
public sealed class ZstdSequencesEncoderTests
{
    [TestMethod]
    public void Encode_NoSequences_IsTheSingleByteZero()
    {
        CollectionAssert.AreEqual(new byte[] { 0 }, ZstdSequencesEncoder.Encode([]));
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(200, 2)]
    [DataRow(0x7F00 + 300, 3)]
    public void Encode_SequenceCounts_TakeTheirWidthAndExecuteBack(int count, int countBytes)
    {
        // After the prefix 0 1 2 3 4, each sequence appends the literal 0, then copies the 4 bytes 5
        // back (Offset_Value 8), 1 2 3 4, so the output repeats the prefix; the last literal is 0 too.
        byte[] literals = new byte[count + 1];
        ZstdSequence[] sequences = [.. Enumerable.Repeat(new ZstdSequence(1, 8, 4), count)];
        byte[] prefix = [0, 1, 2, 3, 4];

        byte[] section = ZstdSequencesEncoder.Encode(sequences);

        Assert.AreEqual(0, section[countBytes], "every table is in Predefined_Mode");
        BoundedDecompressionOutput output = new(int.MaxValue, DecompressionOutcome.OutputBoundPassed, "unbounded");
        output.Append(prefix);
        new ZstdSequencesSection(0).Execute(section, literals, output);
        byte[] written = output.ToArray();
        Assert.AreEqual(prefix.Length + (count * 5) + 1, written.Length);
        for (int index = 0; index < written.Length; index++)
        {
            Assert.AreEqual((byte)(index % 5), written[index], $"byte {index}");
        }
    }
}
