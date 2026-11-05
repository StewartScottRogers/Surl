using System.Buffers.Binary;
using System.Text;
using static Surl.Compression.ZstdTestFrames;

namespace Surl.Compression;

/// <summary>
/// zstd decoding against frames written by hand from RFC 8878: blocks and sections laid out as
/// section 3.1.1 says, Huffman codes assigned as section 4.2.1 says, FSE descriptions written as
/// section 4.1.1 says, and sequence codes from section 3.1.1.3.2.1.1's tables. The predefined FSE
/// tables are held to the RFC's Appendix A by <see cref="ZstdFiniteStateEntropyTableTests"/>, and
/// <see cref="XxHash64"/> to the published vectors by <see cref="XxHash64Tests"/>.
/// </summary>
[TestClass]
public sealed class ZstdFrameDecoderTests
{
    private const string HuffmanText = "abacabadabacabad";

    /// <summary>Weights for symbols 0 to 99: 'a' 3, 'b' 2, 'c' 1; 'd' (100) gets the implied weight 1.</summary>
    private static readonly byte[] HuffmanWeights = [.. new byte[97], 3, 2, 1];

    private static readonly byte[] NoSequences = [0];

    private static ZstdFiniteStateEntropyTable Rle(byte code) => ZstdFiniteStateEntropyTable.Rle(code);

    [TestMethod]
    public void Decode_OneRawBlockInASingleSegmentFrame_ReturnsItsBytes()
    {
        AssertDecodes("hello", FrameWithHeader([0x20, 5], RawBlock(true, "hello"u8.ToArray())));
    }

    [TestMethod]
    public void Decode_RawAndRleBlocks_JoinsThem()
    {
        AssertDecodes("abcccc", Frame(RawBlock(false, "ab"u8.ToArray()), RleBlock(true, (byte)'c', 4)));
    }

    [TestMethod]
    public void Decode_RawLiteralsAndRleModeSequencesUsingRepeatOffsets_ExecutesThem()
    {
        // Literal length code 4, offset code 1 (Offset_Value 2 or 3 by its extra bit), match length
        // code 0 (3). The first sequence's Offset_Value 2 is the second repeat offset, 4; the
        // second's, 3, is the third, 8.
        byte[] sequences = ZstdSequencesWriter.Section(
            [new(4, 2, 3), new(4, 3, 3)], 0x54, [4, 1, 0], Rle(4), Rle(1), Rle(0));
        CollectionAssert.AreEqual(new byte[] { 2, 0x54, 4, 1, 0, 0x05 }, sequences);

        AssertDecodes(
            "abcdabcefghdab!",
            Frame(CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals("abcdefgh!"u8.ToArray()), sequences)));
    }

    [TestMethod]
    public void Decode_RepeatOffsetsWithNoLiterals_ShiftToTheSecondThirdAndFirstLessOne()
    {
        // With literal length 0, Offset_Value 2 is the third repeat offset (8) and 3 the first
        // repeat offset less one (8 - 1 = 7).
        byte[] sequences = ZstdSequencesWriter.Section(
            [new(0, 2, 3), new(0, 3, 3)], 0x54, [0, 1, 0], Rle(0), Rle(1), Rle(0));

        AssertDecodes(
            "abcdefghabcefg",
            Frame(RawBlock(false, "abcdefgh"u8.ToArray()), CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals([]), sequences)));
    }

    [TestMethod]
    public void Decode_OffsetValueOneAfterLiterals_RepeatsTheFirstRepeatOffset()
    {
        byte[] sequences = ZstdSequencesWriter.Section([new(1, 1, 3)], 0x54, [1, 0, 0], Rle(1), Rle(0), Rle(0));

        AssertDecodes("aaaa", Frame(CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals("a"u8.ToArray()), sequences)));
    }

    [TestMethod]
    public void Decode_HuffmanLiteralsInOneStreamWithDirectWeights_ReturnsThem()
    {
        byte[] literals = ZstdHuffmanLiteralsWriter.HuffmanLiterals(
            Encoding.ASCII.GetBytes(HuffmanText), HuffmanWeights, ZstdHuffmanLiteralsWriter.DirectTree(HuffmanWeights), false);

        AssertDecodes(HuffmanText, Frame(CompressedBlock(true, literals, NoSequences)));
    }

    [TestMethod]
    public void Decode_HuffmanLiteralsInFourStreamsWithDirectWeights_ReturnsThem()
    {
        byte[] literals = ZstdHuffmanLiteralsWriter.HuffmanLiterals(
            Encoding.ASCII.GetBytes(HuffmanText + "dcb"), HuffmanWeights, ZstdHuffmanLiteralsWriter.DirectTree(HuffmanWeights), true);

        AssertDecodes(HuffmanText + "dcb", Frame(CompressedBlock(true, literals, NoSequences)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Decode_HuffmanLiteralsWithFseCompressedWeights_ReturnsThem(bool oddWeightCount)
    {
        // Weights 0 to 3 at accuracy log 5; 100 weights end on the first state's update, 99 (the
        // last symbol 'c', implied weight 1) on the second's.
        byte[] weights = oddWeightCount ? HuffmanWeights[..^1] : HuffmanWeights;
        string text = oddWeightCount ? "abacabac" : HuffmanText;
        byte[] tree = ZstdHuffmanLiteralsWriter.CompressedTree(weights, [20, 4, 4, 4], 5);
        Assert.IsLessThan(128, (int)tree[0]);
        byte[] literals = ZstdHuffmanLiteralsWriter.HuffmanLiterals(Encoding.ASCII.GetBytes(text), weights, tree, false);

        AssertDecodes(text, Frame(CompressedBlock(true, literals, NoSequences)));
    }

    [TestMethod]
    public void Decode_TreelessLiterals_ReuseTheFramesEarlierTree()
    {
        byte[] first = ZstdHuffmanLiteralsWriter.HuffmanLiterals(
            Encoding.ASCII.GetBytes(HuffmanText), HuffmanWeights, ZstdHuffmanLiteralsWriter.DirectTree(HuffmanWeights), false);
        byte[] second = ZstdHuffmanLiteralsWriter.HuffmanLiterals("dcbadcba"u8.ToArray(), HuffmanWeights, [], true);

        AssertDecodes(
            HuffmanText + "dcbadcba",
            Frame(CompressedBlock(false, first, NoSequences), CompressedBlock(true, second, NoSequences)));
    }

    [TestMethod]
    public void Decode_SequencesInPredefinedFseCompressedAndRepeatModes_MixedInOneBlock()
    {
        short[] matchLengthCounts = [8, 7, -1, 0, 16];
        short[] offsetCounts = [8, 7, 0, 16, -1];
        ZstdFiniteStateEntropyTable matchLengths = ZstdFiniteStateEntropyTable.FromNormalizedCounts(matchLengthCounts, 5);
        ZstdFiniteStateEntropyTable offsets = ZstdFiniteStateEntropyTable.FromNormalizedCounts(offsetCounts, 5);

        // Block 1: literal lengths and offsets Predefined, match lengths FSE_Compressed.
        byte[] firstSequences = ZstdSequencesWriter.Section(
            [new(5, 8, 7)],
            0x08,
            ZstdFseDescriptionWriter.Write(matchLengthCounts, 5),
            ZstdFiniteStateEntropyTable.PredefinedLiteralLength,
            ZstdFiniteStateEntropyTable.PredefinedOffset,
            matchLengths);

        // Block 2: literal lengths Predefined, offsets FSE_Compressed, match lengths Repeat.
        byte[] secondSequences = ZstdSequencesWriter.Section(
            [new(3, 8, 3), new(0, 1, 4), new(1, 3, 7)],
            0x2C,
            ZstdFseDescriptionWriter.Write(offsetCounts, 5),
            ZstdFiniteStateEntropyTable.PredefinedLiteralLength,
            offsets,
            matchLengths);

        AssertDecodes(
            "abcdeabcdeab" + "xyzabx" + "yzab" + "w" + "wwwwwww",
            Frame(
                CompressedBlock(false, ZstdHuffmanLiteralsWriter.RawLiterals("abcde"u8.ToArray()), firstSequences),
                CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals("xyzw"u8.ToArray()), secondSequences)));
    }

    [TestMethod]
    public void Decode_RleLiterals_RepeatTheirByte()
    {
        AssertDecodes("zzzzz", Frame(CompressedBlock(true, ZstdHuffmanLiteralsWriter.RleLiterals((byte)'z', 5), NoSequences)));
    }

    [TestMethod]
    [DataRow(40)]
    [DataRow(5000)]
    public void Decode_RawLiteralsInLongerSizeFormats_ReturnsThem(int length)
    {
        string text = new('q', length);

        AssertDecodes(text, Frame(CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals(Encoding.ASCII.GetBytes(text)), NoSequences)));
    }

    [TestMethod]
    [DataRow(200, new byte[] { 0x80, 200 })]
    [DataRow(0x7F00, new byte[] { 255, 0, 0 })]
    public void Decode_SequenceCountInTwoAndThreeBytes_ExecutesThatMany(int count, byte[] countBytes)
    {
        // Each sequence: no literals, Offset_Value 1 (with no literals, the second repeat offset,
        // which swaps with the first: 4, then 1, then 4...), match length 3.
        byte[] sequences = [.. countBytes, 0x54, 0, 0, 0, 0x01];

        DecompressionResult result = ZstdFrameDecoder.Decode(
            Frame(RawBlock(false, "abcd"u8.ToArray()), CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals([]), sequences)),
            int.MaxValue);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        Assert.HasCount(4 + (count * 3), result.Output);
        Assert.AreEqual("abcdabcccc", Encoding.ASCII.GetString(result.Output, 0, 10));
    }

    [TestMethod]
    [DataRow((byte)0x60, new byte[] { 0x00, 0x00 })]
    [DataRow((byte)0xA0, new byte[] { 0x00, 0x01, 0x00, 0x00 })]
    [DataRow((byte)0xE0, new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })]
    public void Decode_ContentSizeInTwoFourAndEightBytes_IsChecked(byte descriptor, byte[] contentSize)
    {
        AssertDecodes(new string('z', 256), FrameWithHeader([descriptor, .. contentSize], RleBlock(true, (byte)'z', 256)));
    }

    [TestMethod]
    public void Decode_DictionaryIdOfZero_MeansNoDictionary()
    {
        AssertDecodes("ok", FrameWithHeader([0x01, 0x00, 0x00], RawBlock(true, "ok"u8.ToArray())));
    }

    [TestMethod]
    public void Decode_SkippableFramesAroundAFrame_AreSkipped()
    {
        AssertDecodes(
            "ab",
            [.. SkippableFrame(0xF, "xyz"u8.ToArray()), .. Frame(RawBlock(true, "ab"u8.ToArray())), .. SkippableFrame(0x0, [])]);
    }

    [TestMethod]
    public void Decode_TwoFrames_JoinsTheirContents()
    {
        AssertDecodes("abcd", [.. Frame(RawBlock(true, "ab"u8.ToArray())), .. Frame(RawBlock(true, "cd"u8.ToArray()))]);
    }

    [TestMethod]
    public void Decode_ContentChecksum_IsCheckedAndMatches()
    {
        AssertDecodes("hello", [.. FrameWithHeader([0x04, 0x00], RawBlock(true, "hello"u8.ToArray())), .. Checksum("hello"u8)]);
    }

    [TestMethod]
    public void Decode_WrongContentChecksum_IsChecksumMismatch()
    {
        AssertRefused(
            DecompressionOutcome.ChecksumMismatch,
            [.. FrameWithHeader([0x04, 0x00], RawBlock(true, "hello"u8.ToArray())), .. Checksum("hellp"u8)]);
    }

    [TestMethod]
    public void Decode_ContentSizePastTheBound_IsOutputBoundPassed()
    {
        AssertRefused(DecompressionOutcome.OutputBoundPassed, FrameWithHeader([0x20, 6], RawBlock(true, "hello!"u8.ToArray())), 5);
    }

    [TestMethod]
    public void Decode_BlocksPastTheBound_AreOutputBoundPassed()
    {
        AssertRefused(DecompressionOutcome.OutputBoundPassed, Frame(RleBlock(true, (byte)'z', 6)), 5);
    }

    [TestMethod]
    public void Decode_NegativeOutputBound_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ZstdFrameDecoder.Decode(Frame(RawBlock(true, [])), -1));
    }

    [TestMethod]
    public void Decode_WrongMagic_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x04, 0x22, 0x4D, 0x18, 0x00, 0x00, .. RawBlock(true, [])]);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x01, 0x00, 0x07 })]
    [DataRow(new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x80 })]
    public void Decode_DictionaryId_IsMalformed(byte[] header)
    {
        AssertRefused(DecompressionOutcome.Malformed, FrameWithHeader(header, RawBlock(true, [])));
    }

    [TestMethod]
    public void Decode_TreelessLiteralsWithNoEarlierTree_AreMalformed()
    {
        byte[] literals = ZstdHuffmanLiteralsWriter.HuffmanLiterals(Encoding.ASCII.GetBytes(HuffmanText), HuffmanWeights, [], false);

        AssertRefused(DecompressionOutcome.Malformed, Frame(CompressedBlock(true, literals, NoSequences)));
    }

    [TestMethod]
    public void Decode_TreelessLiteralsInALaterFrame_DoNotReuseTheEarlierFramesTree()
    {
        byte[] first = ZstdHuffmanLiteralsWriter.HuffmanLiterals(
            Encoding.ASCII.GetBytes(HuffmanText), HuffmanWeights, ZstdHuffmanLiteralsWriter.DirectTree(HuffmanWeights), false);
        byte[] second = ZstdHuffmanLiteralsWriter.HuffmanLiterals("dcba"u8.ToArray(), HuffmanWeights, [], false);

        AssertRefused(
            DecompressionOutcome.Malformed,
            [.. Frame(CompressedBlock(true, first, NoSequences)), .. Frame(CompressedBlock(true, second, NoSequences))]);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xD4, 0, 0 })]
    [DataRow(new byte[] { 0x74, 1, 0 })]
    [DataRow(new byte[] { 0x5C, 1, 0 })]
    public void Decode_RepeatModeWithNoEarlierTable_IsMalformed(byte[] modesAndRleCodes)
    {
        // Literal lengths, offsets or match lengths in Repeat mode (3), the others RLE.
        byte[] sequences = [1, .. modesAndRleCodes, 0x01];

        AssertRefused(DecompressionOutcome.Malformed, Frame(CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals("a"u8.ToArray()), sequences)));
    }

    [TestMethod]
    public void Decode_MatchReachingIntoAnEarlierFrame_IsMalformed()
    {
        // Offset_Value 2 with no literals is the third repeat offset, 8: all of the first frame.
        byte[] sequences = ZstdSequencesWriter.Section([new(0, 2, 3)], 0x54, [0, 1, 0], Rle(0), Rle(1), Rle(0));

        AssertRefused(
            DecompressionOutcome.Malformed,
            [.. Frame(RawBlock(true, "abcdefgh"u8.ToArray())), .. Frame(CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals([]), sequences))]);
    }

    [TestMethod]
    public void Decode_FirstRepeatOffsetLessOneOfZero_IsMalformed()
    {
        byte[] sequences = ZstdSequencesWriter.Section([new(0, 3, 3)], 0x54, [0, 1, 0], Rle(0), Rle(1), Rle(0));

        AssertRefused(
            DecompressionOutcome.Malformed,
            Frame(RawBlock(false, "ab"u8.ToArray()), CompressedBlock(true, ZstdHuffmanLiteralsWriter.RawLiterals([]), sequences)));
    }

    [TestMethod]
    [DynamicData(nameof(MalformedFrames))]
    public void Decode_MalformedInput_IsMalformed(string reason, byte[] input)
    {
        DecompressionResult result = ZstdFrameDecoder.Decode(input, int.MaxValue);

        Assert.AreEqual(DecompressionOutcome.Malformed, result.Outcome, reason);
        Assert.IsEmpty(result.Output);
        Assert.IsFalse(string.IsNullOrEmpty(result.Reason), reason);
    }

    /// <summary>Inputs each breaking RFC 8878 in one place, named by the place.</summary>
    public static IEnumerable<object[]> MalformedFrames()
    {
        byte[] oneLiteral = ZstdHuffmanLiteralsWriter.RawLiterals("a"u8.ToArray());
        byte[] directTree = ZstdHuffmanLiteralsWriter.DirectTree(HuffmanWeights);
        yield return ["no frame", Array.Empty<byte>()];
        yield return ["a truncated magic", Magic[..3]];
        yield return ["a truncated frame header", Magic];
        yield return ["the reserved frame header bit", FrameWithHeader([0x08, 0x00], RawBlock(true, []))];
        yield return ["a content size the blocks disagree with", FrameWithHeader([0x20, 3], RawBlock(true, "ab"u8.ToArray()))];
        yield return ["a missing last block", Frame(RawBlock(false, "ab"u8.ToArray()))];
        yield return ["a truncated block", Frame(Block(true, 0, 5, "ab"u8.ToArray()))];
        yield return ["a block over 128 KiB", Frame(Block(true, 1, (128 * 1024) + 1, [0]))];
        yield return ["the reserved block type", Frame(Block(true, 3, 0, []))];
        yield return ["a truncated content checksum", FrameWithHeader([0x04, 0x00], RawBlock(true, []), [0, 0])];
        yield return ["a truncated skippable frame", SkippableFrame(0, "abc"u8.ToArray())[..^1]];
        yield return ["an empty compressed block", Frame(Block(true, 2, 0, []))];
        yield return ["truncated raw literals", Frame(Block(true, 2, 3, [5 << 3, (byte)'a', (byte)'b']))];
        yield return ["a truncated literals header", Frame(Block(true, 2, 1, [0x0C]))];
        yield return ["RLE literals over 128 KiB", Frame(CompressedBlock(true, ZstdHuffmanLiteralsWriter.RleLiterals(0, (128 * 1024) + 1), [0]))];
        yield return ["a missing Huffman tree", Frame(CompressedBlock(true, ZstdHuffmanLiteralsWriter.CompressedHeader(2, false, 4, 0), [0]))];
        yield return ["truncated direct weights", Frame(CompressedBlock(true, [.. ZstdHuffmanLiteralsWriter.CompressedHeader(2, false, 4, 3), .. directTree[..3]], [0]))];
        yield return ["every weight zero", HuffmanFrame([0, 0], "")];
        yield return ["weights completing no power of two", HuffmanFrame([3, 1], "")];
        yield return ["weights past a table log of 11", HuffmanFrame([12], "")];
        yield return ["empty FSE-compressed weights", Frame(CompressedBlock(true, [.. ZstdHuffmanLiteralsWriter.CompressedHeader(2, false, 4, 1), 0], [0]))];
        yield return ["truncated FSE-compressed weights", Frame(CompressedBlock(true, [.. ZstdHuffmanLiteralsWriter.CompressedHeader(2, false, 4, 2), 5, 0], [0]))];
        yield return ["FSE weights too short for the initial states", CompressedWeightsFrame([.. ZstdFseDescriptionWriter.Write([16, 16], 5), 0x01])];
        yield return ["FSE weights naming 256 symbols", CompressedWeightsFrame([.. ZstdFseDescriptionWriter.Write([32], 5), 0x00, 0x08])];
        yield return ["FSE weights at accuracy log 7", CompressedWeightsFrame([.. ZstdFseDescriptionWriter.Write([64, 64], 7), 0x00, 0x00, 0x01])];
        yield return ["a literals stream ending in a zero byte", HuffmanFrame(HuffmanWeights, "ab", [.. ZstdHuffmanLiteralsWriter.Stream("ab"u8, ZstdHuffmanLiteralsWriter.Codes(HuffmanWeights)), 0])];
        yield return ["a literals stream with bits left over", HuffmanFrame(HuffmanWeights, "ab", new ZstdBackwardBitStreamWriter().Add(1, 1).Add(2, 1).Add(1, 1).ToArray())];
        yield return ["too few literals for four streams", Frame(CompressedBlock(true, [.. ZstdHuffmanLiteralsWriter.CompressedHeader(2, true, 5, directTree.Length + 6), .. directTree, 0, 0, 0, 0, 0, 0], [0]))];
        yield return ["a truncated jump table", Frame(CompressedBlock(true, [.. ZstdHuffmanLiteralsWriter.CompressedHeader(2, true, 8, directTree.Length + 5), .. directTree, 0, 0, 0, 0, 0], [0]))];
        yield return ["a jump table past the section", Frame(CompressedBlock(true, [.. ZstdHuffmanLiteralsWriter.CompressedHeader(2, true, 8, directTree.Length + 6), .. directTree, 9, 0, 0, 0, 0, 0], [0]))];
        yield return ["a missing sequences section", Frame(CompressedBlock(true, oneLiteral, []))];
        yield return ["a truncated two-byte sequence count", Frame(CompressedBlock(true, oneLiteral, [0x80]))];
        yield return ["bytes after no sequences", Frame(CompressedBlock(true, oneLiteral, [0, 0]))];
        yield return ["missing compression modes", Frame(CompressedBlock(true, oneLiteral, [1]))];
        yield return ["the reserved compression mode bits", Frame(CompressedBlock(true, oneLiteral, [1, 0x01, 0x01]))];
        yield return ["a missing RLE code", Frame(CompressedBlock(true, oneLiteral, [1, 0x40]))];
        yield return ["an RLE literal length code above 35", Frame(CompressedBlock(true, oneLiteral, [1, 0x40, 36, 0x01]))];
        yield return ["an FSE offset description at accuracy log 9", Frame(CompressedBlock(true, oneLiteral, [1, 0x20, .. ZstdFseDescriptionWriter.Write([256, 256], 9), 0x01]))];
        yield return ["an FSE description truncated", Frame(CompressedBlock(true, oneLiteral, [1, 0x20, 0x00]))];
        yield return ["FSE counts in a zero run past the most codes", Frame(CompressedBlock(true, oneLiteral, [1, 0x20, .. ZstdFseDescriptionWriter.Write([1, .. new short[31], 31], 5), 0x01]))];
        yield return ["FSE counts naming too many codes", Frame(CompressedBlock(true, oneLiteral, [1, 0x20, .. ZstdFseDescriptionWriter.Write([.. Enumerable.Repeat((short)1, 32), 32], 6), 0x01]))];
        yield return ["a sequence taking more literals than the block holds", Frame(CompressedBlock(true, oneLiteral, ZstdSequencesWriter.Section([new(2, 4, 3)], 0x54, [2, 2, 0], Rle(2), Rle(2), Rle(0))))];
        yield return ["a sequences stream with bits left over", Frame(CompressedBlock(true, oneLiteral, [1, 0x54, 1, 0, 0, 0x03]))];
        yield return ["a sequences stream with no end mark", Frame(CompressedBlock(true, oneLiteral, [1, 0x54, 1, 0, 0]))];
    }

    /// <summary>A frame of one block of Huffman literals <paramref name="text"/> in one stream, <paramref name="stream"/> or an empty one.</summary>
    private static byte[] HuffmanFrame(byte[] weights, string text, byte[]? stream = null)
    {
        byte[] payload = [.. ZstdHuffmanLiteralsWriter.DirectTree(weights), .. stream ?? [0x01]];
        return Frame(CompressedBlock(true, [.. ZstdHuffmanLiteralsWriter.CompressedHeader(2, false, text.Length, payload.Length), .. payload], NoSequences));
    }

    private static byte[] CompressedWeightsFrame(byte[] compressedWeights)
    {
        byte[] payload = [(byte)compressedWeights.Length, .. compressedWeights];
        return Frame(CompressedBlock(true, [.. ZstdHuffmanLiteralsWriter.CompressedHeader(2, false, 1, payload.Length + 1), .. payload, 0x01], NoSequences));
    }

    private static byte[] Checksum(ReadOnlySpan<byte> content)
    {
        byte[] checksum = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(checksum, (uint)XxHash64.Hash(content));
        return checksum;
    }

    private static void AssertDecodes(string expected, byte[] input)
    {
        DecompressionResult result = ZstdFrameDecoder.Decode(input, int.MaxValue);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(result.Output));
    }

    private static void AssertRefused(DecompressionOutcome outcome, byte[] input, int outputBound = int.MaxValue)
    {
        DecompressionResult result = ZstdFrameDecoder.Decode(input, outputBound);

        Assert.AreEqual(outcome, result.Outcome, result.Reason);
        Assert.IsEmpty(result.Output);
    }
}
