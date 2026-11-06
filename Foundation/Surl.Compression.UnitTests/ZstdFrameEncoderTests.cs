namespace Surl.Compression;

/// <summary>
/// The zstd frame encoder: one small frame pinned byte for byte from RFC 8878, the block type each
/// kind of input gets, and round trips through <see cref="ZstdFrameDecoder"/>.
/// </summary>
[TestClass]
public sealed class ZstdFrameEncoderTests
{
    [TestMethod]
    public void Encode_AbcdFourTimes_WritesTheFrameWorkedOutFromRfc8878()
    {
        // "abcd" four times, 16 bytes. The matcher finds nothing at positions 0 to 3; at 4 it finds
        // "abcd" 4 back and extends the match to the block's end: one sequence, literal length 4,
        // offset 4, match length 12. Offset 4 is the second of the starting repeat offsets 1, 4, 8,
        // and the literal length is not 0, so its Offset_Value is the repeat code 2 (section 3.1.1.5).
        //
        // Codes (section 3.1.1.3.2.1.1): literal length 4 is code 4, no extra bits; match length 12
        // is code 9 (baseline 12), no extra bits; Offset_Value 2 is offset code 1 (2 = 2^1 + 0), one
        // extra bit of 0.
        //
        // Initial states, the lowest state decoding each code in Appendix A's predefined tables:
        // literal length state 4 (symbol 4), offset state 23 (symbol 1), match length state 28
        // (symbol 9). The decoder reads LL state (6 bits), OF state (5), ML state (6), then the
        // offset extra bit, then the match and literal length extra bits (0 bits each). Written
        // backward, least significant bit first: offset extra bit 0 at bit 0, ML state 28 at bits
        // 1-6, OF state 23 at bits 7-11, LL state 4 at bits 12-17, the end mark at bit 18:
        // 0 + 28*2 + 23*128 + 4*4096 + 262144 = 281528 = 0x044BB8, bytes B8 4B 04.
        //
        // Sequences section: 1 sequence, modes 0 (all Predefined_Mode), the stream. Literals
        // section: a Raw header of size 4 in one byte (4 << 3 = 0x20), then "abcd". The block
        // content is 10 bytes, fewer than 16, so it is a last Compressed block: (10 << 3) | (2 << 1)
        // | 1 = 0x55. Frame header: the magic, the descriptor 0x20 (single segment, a 1-byte
        // Frame_Content_Size, no checksum, no dictionary), the content size 16.
        byte[] expected =
        [
            0x28, 0xB5, 0x2F, 0xFD, 0x20, 0x10,
            0x55, 0x00, 0x00,
            0x20, (byte)'a', (byte)'b', (byte)'c', (byte)'d',
            0x01, 0x00, 0xB8, 0x4B, 0x04,
        ];

        byte[] encoded = ZstdFrameEncoder.Encode("abcdabcdabcdabcd"u8);

        CollectionAssert.AreEqual(expected, encoded);
        AssertRoundTrips("abcdabcdabcdabcd"u8.ToArray(), encoded);
    }

    [TestMethod]
    public void Encode_Empty_WritesOneEmptyLastRawBlock()
    {
        byte[] encoded = ZstdFrameEncoder.Encode([]);

        CollectionAssert.AreEqual(new byte[] { 0x28, 0xB5, 0x2F, 0xFD, 0x20, 0x00, 0x01, 0x00, 0x00 }, encoded);
        AssertRoundTrips([], encoded);
    }

    [TestMethod]
    public void Encode_OneByte_WritesARawBlock()
    {
        byte[] encoded = ZstdFrameEncoder.Encode([0x7A]);

        CollectionAssert.AreEqual(new byte[] { 0x28, 0xB5, 0x2F, 0xFD, 0x20, 0x01, 0x09, 0x00, 0x00, 0x7A }, encoded);
        AssertRoundTrips([0x7A], encoded);
    }

    [TestMethod]
    public void Encode_RunOfOneByte_WritesAnRleBlock()
    {
        byte[] input = Enumerable.Repeat((byte)0x41, 1000).ToArray();

        byte[] encoded = ZstdFrameEncoder.Encode(input);

        // Descriptor 0x60: a 2-byte content size, 1000 - 256 = 744 = E8 02; block (1000 << 3) | (1 << 1) | 1.
        CollectionAssert.AreEqual(new byte[] { 0x28, 0xB5, 0x2F, 0xFD, 0x60, 0xE8, 0x02, 0x43, 0x1F, 0x00, 0x41 }, encoded);
        AssertRoundTrips(input, encoded);
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(1000)]
    public void Encode_Incompressible_WritesARawBlock(int length)
    {
        byte[] input = CompressionTestInputs.Incompressible(length, length);

        byte[] encoded = ZstdFrameEncoder.Encode(input);

        int blockHeader = length < 256 ? 5 + 1 : 5 + 2;
        Assert.AreEqual(1, encoded[blockHeader] & 0b111, "a last Raw block");
        Assert.AreEqual(blockHeader + 3 + length, encoded.Length);
        AssertRoundTrips(input, encoded);
    }

    [TestMethod]
    public void Encode_RepetitiveText_WritesACompressedBlockSmallerThanTheInput()
    {
        byte[] input = CompressionTestInputs.Repetitive(5000);

        byte[] encoded = ZstdFrameEncoder.Encode(input);

        Assert.AreEqual(0b101, encoded[7] & 0b111, "a last Compressed block");
        Assert.IsLessThan(input.Length / 10, encoded.Length);
        AssertRoundTrips(input, encoded);
    }

    [TestMethod]
    public void Encode_InputPastOneBlock_WritesSeveralBlocksAndRoundTrips()
    {
        byte[] input = CompressionTestInputs.Repetitive((ZstdFrameEncoder.BlockInputLength * 2) + 1000);

        byte[] encoded = ZstdFrameEncoder.Encode(input);

        Assert.AreEqual(0xA0, encoded[4], "single segment, a 4-byte content size");
        Assert.IsLessThan(input.Length / 20, encoded.Length);
        AssertRoundTrips(input, encoded);
    }

    [TestMethod]
    public void Encode_CompressedRawRleAndCompressedBlocks_KeepsRepeatOffsetsOnlyFromCompressedBlocks()
    {
        byte[] input =
        [
            .. CompressionTestInputs.Repetitive(ZstdFrameEncoder.BlockInputLength),
            .. CompressionTestInputs.Incompressible(ZstdFrameEncoder.BlockInputLength, 3),
            .. Enumerable.Repeat((byte)0x20, ZstdFrameEncoder.BlockInputLength),
            .. CompressionTestInputs.Repetitive(ZstdFrameEncoder.BlockInputLength),
        ];

        AssertRoundTrips(input, ZstdFrameEncoder.Encode(input));
    }

    [TestMethod]
    [DataRow(100)]
    [DataRow(3_000)]
    [DataRow(5_000)]
    public void Encode_LongLiteralsBeforeAFarMatch_RoundTrips(int length)
    {
        byte[] head = CompressionTestInputs.Incompressible(length, 11);
        byte[] input = [.. head, .. CompressionTestInputs.Repetitive(length), .. head];

        AssertRoundTrips(input, ZstdFrameEncoder.Encode(input));
    }

    [TestMethod]
    public void Encode_ShortLiteralRunsBetweenRepeatedWords_RoundTrips()
    {
        string text = string.Concat(Enumerable.Range(0, 4000).Select(index => $"{index % 7}word{index % 13}x{index % 5};"));
        byte[] input = System.Text.Encoding.ASCII.GetBytes(text);

        AssertRoundTrips(input, ZstdFrameEncoder.Encode(input));
    }

    private static void AssertRoundTrips(byte[] input, byte[] encoded)
    {
        DecompressionResult result = ZstdFrameDecoder.Decode(encoded, input.Length);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        CollectionAssert.AreEqual(input, result.Output);
    }
}
