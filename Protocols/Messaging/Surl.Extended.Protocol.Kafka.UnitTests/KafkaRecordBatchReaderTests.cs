using System.Text;
using Surl.MessageBus;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaRecordBatches;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The record batch checks of ADR-0105 decision 4, each batch built by hand from the Kafka
/// "Record Batch" layout with its CRC-32C computed bit by bit.
/// </summary>
[TestClass]
public sealed class KafkaRecordBatchReaderTests
{
    private static readonly byte[] Valid = Batch([Record(0, "a")]);

    [TestMethod]
    public void Read_ABatchOfThreeRecords_ReturnsEachKeyValueAndHeaderInOrder()
    {
        byte[] batch = Batch(
        [
            Record(0, "k"u8.ToArray(), "v0"u8.ToArray(), ("h"u8.ToArray(), "1"u8.ToArray())),
            Record(1, null, null),
            Record(2, "v2"),
        ]);

        var result = KafkaRecordBatchReader.Read(batch);

        Assert.AreEqual(KafkaErrorCode.None, result.Error);
        Assert.HasCount(3, result.Records);
        CollectionAssert.AreEqual("k"u8.ToArray(), result.Records[0].Key!.Value.ToArray());
        CollectionAssert.AreEqual("v0"u8.ToArray(), result.Records[0].Value!.Value.ToArray());
        CollectionAssert.AreEqual(new[] { new BusMessageHeader("h", "1") }, result.Records[0].Headers.ToArray());
        Assert.IsNull(result.Records[1].Key);
        Assert.IsNull(result.Records[1].Value);
        Assert.AreEqual(0, result.Records[1].KeyAndValueBytes);
        Assert.AreEqual("v2", Encoding.UTF8.GetString(result.Records[2].Value!.Value.Span));
        Assert.AreEqual(3, result.Records[0].KeyAndValueBytes);
    }

    [TestMethod]
    public void Read_TwoBatches_ReturnsTheRecordsOfBoth()
    {
        var result = KafkaRecordBatchReader.Read((byte[])[.. Batch([Record(0, "a"), Record(1, "b")]), .. Batch([Record(0, "c")])]);

        Assert.AreEqual(KafkaErrorCode.None, result.Error);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result.Records.Select(record => Encoding.UTF8.GetString(record.Value!.Value.Span)).ToArray());
    }

    [TestMethod]
    public void Read_HeadersWithANullValueOrMalformedUtf8_AreDropped()
    {
        byte[] invalidUtf8 = [0xC3];
        var batch = Batch([Record(0, null, "v"u8.ToArray(), ("null"u8.ToArray(), null), (invalidUtf8, "x"u8.ToArray()), ("bad"u8.ToArray(), invalidUtf8), ("kept"u8.ToArray(), "y"u8.ToArray()))]);

        var result = KafkaRecordBatchReader.Read(batch);

        CollectionAssert.AreEqual(new[] { new BusMessageHeader("kept", "y") }, result.Records.Single().Headers.ToArray());
    }

    [TestMethod]
    public void Read_MagicOne_IsUnsupportedForMessageFormat() =>
        Assert.AreEqual(KafkaErrorCode.UnsupportedForMessageFormat, KafkaRecordBatchReader.Read(Batch([Record(0, "a")], magic: 1)).Error);

    [TestMethod]
    public void Read_ACrcThatDoesNotMatch_IsCorruptMessage() =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Batch([Record(0, "a")], crcFlip: 1)).Error);

    [TestMethod]
    public void Read_AFlippedByteAfterTheCrc_IsCorruptMessage()
    {
        var batch = (byte[])Valid.Clone();
        batch[^1] ^= 0x01;

        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(batch).Error);
    }

    [TestMethod]
    [DataRow((short)0x10, KafkaErrorCode.UnsupportedForMessageFormat, DisplayName = "transactional")]
    [DataRow((short)0x20, KafkaErrorCode.UnsupportedForMessageFormat, DisplayName = "control")]
    [DataRow((short)0x01, KafkaErrorCode.UnsupportedForMessageFormat, DisplayName = "gzip, served by BL-521")]
    [DataRow((short)0x04, KafkaErrorCode.UnsupportedForMessageFormat, DisplayName = "zstd, served by BL-521")]
    [DataRow((short)0x05, KafkaErrorCode.CorruptMessage, DisplayName = "codec 5")]
    [DataRow((short)0x08, KafkaErrorCode.None, DisplayName = "LogAppendTime timestamp type")]
    public void Read_Attributes_AreAnsweredByTheirBits(short attributes, KafkaErrorCode expected) =>
        Assert.AreEqual(expected, KafkaRecordBatchReader.Read(Batch([Record(0, "a")], attributes: attributes)).Error);

    [TestMethod]
    public void Read_EmptyRecords_IsCorruptMessage() =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(ReadOnlyMemory<byte>.Empty).Error);

    [TestMethod]
    public void Read_BytesShorterThanABatchHeaderAfterABatch_IsCorruptMessage() =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read((byte[])[.. Valid, 0]).Error);

    [TestMethod]
    public void Read_ABatchLengthPastTheRecords_IsCorruptMessage() =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Valid[..^1]).Error);

    [TestMethod]
    public void Read_ABatchLengthShorterThanItsHeader_IsCorruptMessage()
    {
        var batch = (byte[])Valid.Clone();
        Int32(48).CopyTo(batch, 8);

        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(batch).Error);
    }

    [TestMethod]
    [DataRow(0, 0, DisplayName = "no records")]
    [DataRow(1, 1, DisplayName = "lastOffsetDelta past the count")]
    public void Read_ACountOrLastOffsetDeltaThatDisagrees_IsCorruptMessage(int count, int lastOffsetDelta) =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Batch([Record(0, "a")], count: count, lastOffsetDelta: lastOffsetDelta)).Error);

    [TestMethod]
    public void Read_FewerRecordsThanTheCount_IsCorruptMessage() =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Batch([Record(0, "a")], count: 2)).Error);

    [TestMethod]
    public void Read_BytesAfterTheLastRecord_IsCorruptMessage() =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Batch([Record(0, "a"), Record(1, "b")], count: 1)).Error);

    [TestMethod]
    public void Read_AnOffsetDeltaOutOfPlace_IsCorruptMessage() =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Batch([Record(1, "a")])).Error);

    [TestMethod]
    public void Read_ARecordLengthThatDisagreesWithItsBytes_IsCorruptMessage()
    {
        var record = Record(0, "a");
        record[0] += 2;

        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Batch([[.. record, 0]])).Error);
    }

    [TestMethod]
    public void Read_ANegativeHeaderCount_IsCorruptMessage()
    {
        byte[] body = [0, .. VarLong(0), .. VarLong(0), .. VarBytes(null), .. VarBytes("v"u8.ToArray()), .. VarLong(-1)];

        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Batch([[.. VarLong(body.Length), .. body]])).Error);
    }

    [TestMethod]
    public void Read_ANullHeaderKey_IsCorruptMessage() =>
        Assert.AreEqual(KafkaErrorCode.CorruptMessage, KafkaRecordBatchReader.Read(Batch([Record(0, null, null, (null, "x"u8.ToArray()))])).Error);
}
