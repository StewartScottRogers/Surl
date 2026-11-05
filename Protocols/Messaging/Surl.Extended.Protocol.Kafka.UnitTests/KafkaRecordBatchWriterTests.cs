using System.Buffers.Binary;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The batches a Fetch serves (BL-520; ADR-0105 decision 4): at most 1000 records a batch, and
/// the byte limit. Each batch's bytes are pinned in <see cref="KafkaFetchAnswererTests"/>.
/// </summary>
[TestClass]
public sealed class KafkaRecordBatchWriterTests
{
    private static readonly DateTimeOffset PublishedAt = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Write_NullRecords_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaRecordBatchWriter.Write(null!, 0, takeFirstWhole: true));

    [TestMethod]
    public void Write_OneThousandAndOneRecords_AreTwoBatchesOfOneThousandAndOne()
    {
        var records = Records(1001);

        var bytes = KafkaRecordBatchWriter.Write(records, long.MaxValue, takeFirstWhole: false);

        var firstLength = 12 + BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8));
        Assert.AreEqual(1000, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(57)));
        Assert.AreEqual(1000L, BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(firstLength)));
        Assert.AreEqual(1, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(firstLength + 57)));
        Assert.AreEqual(firstLength + KafkaRecordBatchWriter.BatchHeaderBytes + 8, bytes.Length);
    }

    [TestMethod]
    public void Write_ALimitBelowTheFirstRecordWithoutTakingItWhole_WritesNothing() =>
        Assert.IsEmpty(KafkaRecordBatchWriter.Write(Records(2), 10, takeFirstWhole: false));

    [TestMethod]
    public void Write_ALimitThatHoldsOneRecord_WritesOneBatchOfOne()
    {
        var one = KafkaRecordBatchWriter.Write(Records(1), long.MaxValue, takeFirstWhole: false);

        var bytes = KafkaRecordBatchWriter.Write(Records(3), one.Length, takeFirstWhole: false);

        CollectionAssert.AreEqual(one, bytes);
    }

    // count records of one-byte payloads at offsets 0 on, each record 8 bytes once encoded.
    private static List<BusLogRecord> Records(int count) =>
        [.. Enumerable.Range(0, count).Select(offset => new BusLogRecord(offset, new BusMessage("orders", "x"u8.ToArray()) { PublishedAt = PublishedAt }))];
}
