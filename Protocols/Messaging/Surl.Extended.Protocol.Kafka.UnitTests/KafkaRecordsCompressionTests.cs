using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaCompressedPayloads;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Decoding and encoding one batch's records under its codec (BL-521; ADR-0105 decision 4), every
/// compressed payload written in the tests from its format document.
/// </summary>
[TestClass]
public sealed class KafkaRecordsCompressionTests
{
    private static readonly byte[] Text = [.. Enumerable.Range(0, 300).Select(index => (byte)('a' + (index % 26)))];

    [TestMethod]
    [DataRow(0L, 268435456, DisplayName = "no --max-message: 256 MiB")]
    [DataRow(1048576L, 4194304, DisplayName = "1 MiB: 4 MiB")]
    [DataRow(16L, 64, DisplayName = "16 bytes: 64")]
    [DataRow(long.MaxValue / 8, 0x7FFFFFC7, DisplayName = "past an array: the largest array")]
    public void DecompressionBound_MaxMessage_IsFourTimesItOr256MiB(long maxMessageBytes, int expected) =>
        Assert.AreEqual(expected, KafkaRecordsCompression.DecompressionBound(maxMessageBytes));

    [TestMethod]
    [DynamicData(nameof(Codecs), typeof(KafkaCompressedPayloads))]
    public void Decompress_APayloadWrittenFromItsFormat_IsTheRecords(short codec, string format)
    {
        var error = KafkaRecordsCompression.Decompress((KafkaCompressionCodec)codec, Compress(format, Text), 300, out var records);

        Assert.AreEqual(KafkaErrorCode.None, error);
        CollectionAssert.AreEqual(Text, records);
    }

    [TestMethod]
    [DynamicData(nameof(Codecs), typeof(KafkaCompressedPayloads))]
    public void Decompress_PastTheBound_IsMessageTooLarge(short codec, string format)
    {
        var error = KafkaRecordsCompression.Decompress((KafkaCompressionCodec)codec, Compress(format, Text), 299, out var records);

        Assert.AreEqual(KafkaErrorCode.MessageTooLarge, error);
        Assert.IsEmpty(records);
    }

    [TestMethod]
    [DataRow((short)1, DisplayName = "gzip")]
    [DataRow((short)2, DisplayName = "snappy")]
    [DataRow((short)3, DisplayName = "lz4")]
    [DataRow((short)4, DisplayName = "zstd")]
    public void Decompress_BytesItsCodecCannotDecode_IsCorruptMessage(short codec)
    {
        var error = KafkaRecordsCompression.Decompress((KafkaCompressionCodec)codec, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], 300, out var records);

        Assert.AreEqual(KafkaErrorCode.CorruptMessage, error);
        Assert.IsEmpty(records);
    }

    [TestMethod]
    public void Decompress_AnLz4FrameWithAWrongHeaderChecksum_IsCorruptMessage()
    {
        var frame = Lz4Frame(Text);
        frame[6] ^= 0x01;

        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordsCompression.Decompress(KafkaCompressionCodec.Lz4, frame, 300, out _));
    }

    [TestMethod]
    public void Decompress_AZstdFrameNamingADictionary_IsCorruptMessage()
    {
        // RFC 8878 section 3.1.1.1.1: Frame_Header_Descriptor 0x21 is Single_Segment_flag with a
        // 1-byte Dictionary_ID (7 here) before the 1-byte Frame_Content_Size.
        byte[] frame = [0x28, 0xB5, 0x2F, 0xFD, 0x21, 0x07, 0x01, 0x09, 0x00, 0x00, (byte)'a'];

        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordsCompression.Decompress(KafkaCompressionCodec.Zstd, frame, 300, out _));
    }

    [TestMethod]
    [DataRow((short)0, DisplayName = "none is not decompressed")]
    [DataRow((short)5, DisplayName = "codec 5")]
    [DataRow((short)7, DisplayName = "codec 7")]
    public void Decompress_ACodecOutsideOneToFour_IsCorruptMessage(short codec) =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordsCompression.Decompress((KafkaCompressionCodec)codec, Text, 300, out _));

    [TestMethod]
    [DynamicData(nameof(ServedCodecs), typeof(KafkaCompressedPayloads))]
    public void Compress_EachCodec_DecodesUnderItsSpecificationToTheRecords(KafkaCompressionCodec codec)
    {
        var compressed = KafkaRecordsCompression.Compress(codec, Text);

        CollectionAssert.AreEqual(Text, Decompress(codec, compressed));
    }

    [TestMethod]
    public void Compress_ACodecOutsideZeroToFour_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaRecordsCompression.Compress((KafkaCompressionCodec)5, Text));
}
