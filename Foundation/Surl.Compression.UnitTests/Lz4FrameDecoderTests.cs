using System.Buffers.Binary;
using System.Text;

namespace Surl.Compression;

/// <summary>
/// LZ4 frame decoding against frames written by hand from lz4's <c>doc/lz4_Frame_format.md</c> and
/// <c>doc/lz4_Block_format.md</c>, their checksums computed with <see cref="XxHash32"/>, which
/// <see cref="XxHash32Tests"/> holds to the published vectors.
/// </summary>
[TestClass]
public sealed class Lz4FrameDecoderTests
{
    private const byte Independent = 0x60;
    private const byte Linked = 0x40;
    private const byte BlockChecksums = 0x10;
    private const byte ContentSize = 0x08;
    private const byte ContentChecksum = 0x04;
    private const byte Max64KiB = 0x40;

    private static readonly byte[] Magic = [0x04, 0x22, 0x4D, 0x18];

    private static readonly byte[] EndMark = [0, 0, 0, 0];

    /// <summary>"abcd", a match of 8 at offset 4 (overlapping), then the literals "efghi".</summary>
    private static readonly byte[] CompressedBlock = [0x44, .. "abcd"u8, 0x04, 0x00, 0x50, .. "efghi"u8];

    private const string CompressedBlockContent = "abcdabcdabcdefghi";

    [TestMethod]
    public void Decode_EmptyFrameTheLz4ToolWrites_ReturnsNothing()
    {
        DecompressionResult result = Lz4FrameDecoder.Decode(
            [.. Magic, 0x64, 0x40, 0xA7, .. EndMark, 0x05, 0x5D, 0xCC, 0x02], 0);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        Assert.IsEmpty(result.Output);
    }

    [TestMethod]
    public void Decode_CompressedBlock_ReturnsItsContent()
    {
        AssertDecodes(CompressedBlockContent, [.. Header(Independent), .. Block(CompressedBlock), .. EndMark]);
    }

    [TestMethod]
    public void Decode_StoredBlock_ReturnsItsBytes()
    {
        AssertDecodes("stored", [.. Header(Independent), .. StoredBlock("stored"u8.ToArray()), .. EndMark]);
    }

    [TestMethod]
    public void Decode_LinkedBlocks_CopyFromTheEarlierBlock()
    {
        byte[] secondBlock = [0x04, 0x04, 0x00, 0x10, (byte)'z'];

        AssertDecodes("abcdabcdabcdz", [.. Header(Linked), .. StoredBlock("abcd"u8.ToArray()), .. Block(secondBlock), .. EndMark]);
    }

    [TestMethod]
    public void Decode_IndependentBlockCopyingFromAnEarlierBlock_IsMalformed()
    {
        byte[] secondBlock = [0x04, 0x04, 0x00, 0x10, (byte)'z'];

        AssertRefused(DecompressionOutcome.Malformed, [.. Header(Independent), .. StoredBlock("abcd"u8.ToArray()), .. Block(secondBlock), .. EndMark]);
    }

    [TestMethod]
    public void Decode_BlockAndContentChecksums_AreCheckedAndPass()
    {
        AssertDecodes(CompressedBlockContent, [
            .. Header(Independent | BlockChecksums | ContentChecksum),
            .. Block(CompressedBlock), .. UInt32(XxHash32.Hash(CompressedBlock)),
            .. EndMark, .. UInt32(XxHash32.Hash(Encoding.ASCII.GetBytes(CompressedBlockContent)))]);
    }

    [TestMethod]
    public void Decode_BadBlockChecksum_IsAChecksumMismatch()
    {
        AssertRefused(DecompressionOutcome.ChecksumMismatch, [
            .. Header(Independent | BlockChecksums), .. Block(CompressedBlock), .. UInt32(XxHash32.Hash(CompressedBlock) ^ 1), .. EndMark]);
    }

    [TestMethod]
    public void Decode_BadContentChecksum_IsAChecksumMismatch()
    {
        AssertRefused(DecompressionOutcome.ChecksumMismatch, [
            .. Header(Independent | ContentChecksum), .. Block(CompressedBlock), .. EndMark, 0, 0, 0, 0]);
    }

    [TestMethod]
    public void Decode_BadHeaderChecksum_IsAChecksumMismatch()
    {
        AssertRefused(DecompressionOutcome.ChecksumMismatch, [.. Magic, Independent, Max64KiB, 0x00, .. EndMark]);
    }

    [TestMethod]
    public void Decode_WrongMagic_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x03, 0x22, 0x4D, 0x18, Independent, Max64KiB, 0x82, .. EndMark]);
    }

    [TestMethod]
    public void Decode_TruncatedMagic_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [0x04, 0x22]);
    }

    [TestMethod]
    public void Decode_MatchOffsetZero_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header(Independent), .. Block([0x40, .. "abcd"u8, 0x00, 0x00, 0x00]), .. EndMark]);
    }

    [TestMethod]
    public void Decode_TruncatedBlock_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header(Independent), .. Block([0x40, .. "abcd"u8, 0x04]), .. EndMark]);
    }

    [TestMethod]
    [DataRow((byte)0x80, Max64KiB)]
    [DataRow((byte)0x62, Max64KiB)]
    [DataRow(Independent, (byte)0x41)]
    [DataRow((byte)0x61, Max64KiB)]
    [DataRow(Independent, (byte)0x30)]
    public void Decode_UnservedDescriptorBits_AreMalformed(byte flags, byte blockDescriptor)
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header(flags, blockDescriptor), .. EndMark]);
    }

    [TestMethod]
    public void Decode_ContentSizeThatAgrees_Decodes()
    {
        AssertDecodes(CompressedBlockContent, [.. Header(Independent | ContentSize, contentSize: 17), .. Block(CompressedBlock), .. EndMark]);
    }

    [TestMethod]
    public void Decode_ContentSizeThatDisagrees_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header(Independent | ContentSize, contentSize: 16), .. Block(CompressedBlock), .. EndMark]);
    }

    [TestMethod]
    public void Decode_ContentSizePastTheBound_StopsWithTheBoundOutcome()
    {
        AssertRefused(DecompressionOutcome.OutputBoundPassed, [.. Header(Independent | ContentSize, contentSize: 17), .. Block(CompressedBlock), .. EndMark], 16);
    }

    [TestMethod]
    public void Decode_BlocksPastTheBound_StopWithTheBoundOutcome()
    {
        AssertRefused(DecompressionOutcome.OutputBoundPassed, [.. Header(Independent), .. Block(CompressedBlock), .. EndMark], 10);
    }

    [TestMethod]
    public void Decode_BlockLargerThanTheBlockMaximum_IsMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header(Independent), .. UInt32(0x10001), .. new byte[0x10001], .. EndMark]);
    }

    [TestMethod]
    public void Decode_BlockDecodingPastTheBlockMaximum_IsMalformed()
    {
        // One literal, then a match of 15 + 257 * 255 + 0 + 4 = 65554 bytes at offset 1, then an empty last sequence.
        byte[] block = [0x1F, (byte)'a', 0x01, 0x00, .. Enumerable.Repeat((byte)0xFF, 257), 0x00, 0x00];

        AssertRefused(DecompressionOutcome.Malformed, [.. Header(Independent), .. Block(block), .. EndMark]);
    }

    [TestMethod]
    public void Decode_BytesAfterTheFrame_AreMalformed()
    {
        AssertRefused(DecompressionOutcome.Malformed, [.. Header(Independent), .. EndMark, 0x00]);
    }

    [TestMethod]
    public void Decode_NegativeBound_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Lz4FrameDecoder.Decode([], -1));
    }

    private static byte[] Header(int flags, byte blockDescriptor = Max64KiB, ulong? contentSize = null)
    {
        byte[] descriptor = contentSize is ulong size
            ? [(byte)flags, blockDescriptor, .. UInt64(size)]
            : [(byte)flags, blockDescriptor];
        return [.. Magic, .. descriptor, (byte)(XxHash32.Hash(descriptor) >> 8)];
    }

    private static byte[] Block(byte[] compressed) => [.. UInt32((uint)compressed.Length), .. compressed];

    private static byte[] StoredBlock(byte[] bytes) => [.. UInt32((uint)bytes.Length | 0x80000000), .. bytes];

    private static byte[] UInt32(uint value)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] UInt64(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static void AssertDecodes(string expected, byte[] frame)
    {
        DecompressionResult result = Lz4FrameDecoder.Decode(frame, int.MaxValue);

        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(result.Output));
    }

    private static void AssertRefused(DecompressionOutcome expected, byte[] frame, int outputBound = int.MaxValue)
    {
        DecompressionResult result = Lz4FrameDecoder.Decode(frame, outputBound);

        Assert.AreEqual(expected, result.Outcome, result.Reason);
        Assert.IsEmpty(result.Output);
    }
}
