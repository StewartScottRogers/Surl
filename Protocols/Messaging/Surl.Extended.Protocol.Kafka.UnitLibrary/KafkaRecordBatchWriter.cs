using System.Buffers.Binary;
using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Builds the record batches a Fetch serves from a topic log's records (ADR-0105 decisions 3 and
/// 4; the Kafka "Record Batch" and "Record" layouts): each batch with <c>baseOffset</c> its first
/// record's offset, <c>partitionLeaderEpoch</c> 0, <c>magic</c> 2, attributes LogAppendTime (bit 3)
/// and the codec it is written with (<see cref="KafkaRecordsCompression.Compress"/> over its
/// records), <c>baseTimestamp</c> and <c>maxTimestamp</c> its first and last record's publish
/// time, <c>producerId</c> -1, <c>producerEpoch</c> -1, <c>baseSequence</c> -1, its records with
/// offset and timestamp deltas from the first, and its CRC-32C. A batch holds at most
/// <see cref="MaxRecordsPerBatch"/> records.
/// </summary>
public static class KafkaRecordBatchWriter
{
    /// <summary>The most records one batch holds: 1000 (ADR-0105 decision 4).</summary>
    public const int MaxRecordsPerBatch = 1000;

    /// <summary>The bytes of a batch before its first record: 61.</summary>
    public const int BatchHeaderBytes = 61;

    // The batch's attributes before the codec: timestamp type LogAppendTime (bit 3), not transactional.
    private const short LogAppendTimeAttributes = 0x08;

    // The bytes from the start of the batch to the end of the crc field: baseOffset,
    // batchLength, partitionLeaderEpoch, magic, crc.
    private const int BytesBeforeAttributes = 21;

    /// <summary>
    /// Writes <paramref name="records"/>, in order, as batches compressed with
    /// <paramref name="codec"/>, adding records while the batches stay within
    /// <paramref name="maxBytes"/>; when <paramref name="takeFirstWhole"/> is true the first
    /// record is written whatever it costs, so the first batch of a response always makes
    /// progress (KIP-74).
    /// </summary>
    /// <remarks>
    /// Records are taken while their uncompressed bytes fit; a compressed batch that still
    /// passes what is left of <paramref name="maxBytes"/> gives up its last records until it
    /// fits, and the batches end there.
    /// </remarks>
    /// <param name="records">The records, in offset order, contiguous.</param>
    /// <param name="maxBytes">The most bytes the batches may take together.</param>
    /// <param name="takeFirstWhole">Whether the first record is written even past <paramref name="maxBytes"/>.</param>
    /// <param name="codec">The codec of every batch; its value goes in the attributes.</param>
    /// <returns>The batches' bytes, back to back; empty when no record fits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="codec"/> is not 0 to 4.</exception>
    public static byte[] Write(IReadOnlyList<BusLogRecord> records, long maxBytes, bool takeFirstWhole, KafkaCompressionCodec codec = KafkaCompressionCodec.None)
    {
        ArgumentNullException.ThrowIfNull(records);
        RequireCodec(codec);

        var output = new List<byte>();
        var next = 0;
        while (next < records.Count)
        {
            var batch = TakeBatch(records, next, maxBytes - output.Count, takeFirstWhole && output.Count == 0, codec);
            output.AddRange(batch.Bytes);
            next += batch.Taken;
            if (batch.Taken < MaxRecordsPerBatch)
            {
                break;
            }
        }

        return [.. output];
    }

    /// <summary>Returns <paramref name="codec"/> when it is 0 to 4, and throws otherwise.</summary>
    /// <param name="codec">The codec.</param>
    /// <returns><paramref name="codec"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="codec"/> is not 0 to 4.</exception>
    public static KafkaCompressionCodec RequireCodec(KafkaCompressionCodec codec) =>
        codec is >= KafkaCompressionCodec.None and <= KafkaCompressionCodec.Zstd
            ? codec
            : throw new ArgumentOutOfRangeException(nameof(codec), codec, "The codec is not 0 to 4.");

    // One batch from records[start], within budget: records taken while their uncompressed bytes
    // fit (the first whatever it costs when takeFirstWhole), then the last given up while the
    // encoded batch passes the budget.
    private static (byte[] Bytes, int Taken) TakeBatch(IReadOnlyList<BusLogRecord> records, int start, long budget, bool takeFirstWhole, KafkaCompressionCodec codec)
    {
        var first = records[start];
        var batch = new List<(BusLogRecord Record, byte[] Encoded)>();
        var total = 0L;
        for (var index = start; index < records.Count && batch.Count < MaxRecordsPerBatch; index++)
        {
            var encoded = EncodeRecord(records[index], first);
            var cost = encoded.Length + (batch.Count == 0 ? BatchHeaderBytes : 0);
            if (total + cost > budget && !(batch.Count == 0 && takeFirstWhole))
            {
                break;
            }

            batch.Add((records[index], encoded));
            total += cost;
        }

        return ShrinkToBudget(batch, budget, takeFirstWhole, codec);
    }

    private static (byte[] Bytes, int Taken) ShrinkToBudget(List<(BusLogRecord Record, byte[] Encoded)> batch, long budget, bool takeFirstWhole, KafkaCompressionCodec codec)
    {
        while (batch.Count > 0)
        {
            var bytes = EncodeBatch(batch, codec);
            if (bytes.Length <= budget || (batch.Count == 1 && takeFirstWhole))
            {
                return (bytes, batch.Count);
            }

            batch.RemoveAt(batch.Count - 1);
        }

        return ([], 0);
    }

    /// <summary>One record of a batch whose first record is <paramref name="first"/>.</summary>
    private static byte[] EncodeRecord(BusLogRecord record, BusLogRecord first)
    {
        var message = record.Message;
        var body = new KafkaPrimitiveWriter();
        body.WriteInt8(0);
        body.WriteVarLong(message.PublishedAt.ToUnixTimeMilliseconds() - first.Message.PublishedAt.ToUnixTimeMilliseconds());
        body.WriteVarInt((int)(record.Offset - first.Offset));
        WriteVarBytes(body, message.Key);
        WriteVarBytes(body, message.Payload);
        body.WriteVarInt(message.Headers.Count);
        foreach (var header in message.Headers)
        {
            WriteVarBytes(body, Encoding.UTF8.GetBytes(header.Name));
            WriteVarBytes(body, Encoding.UTF8.GetBytes(header.Value));
        }

        var length = new KafkaPrimitiveWriter();
        length.WriteVarInt(body.Length);
        length.WriteRaw(body.ToArray());
        return length.ToArray();
    }

    // A varint length, -1 for null, then the bytes.
    private static void WriteVarBytes(KafkaPrimitiveWriter body, ReadOnlyMemory<byte>? bytes)
    {
        if (bytes is not { } present)
        {
            body.WriteVarInt(-1);
            return;
        }

        body.WriteVarInt(present.Length);
        body.WriteRaw(present.Span);
    }

    private static byte[] EncodeBatch(List<(BusLogRecord Record, byte[] Encoded)> batch, KafkaCompressionCodec codec)
    {
        var first = batch[0].Record;
        var last = batch[^1].Record;
        var writer = new KafkaPrimitiveWriter();
        writer.WriteInt64(first.Offset);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt8(2);
        writer.WriteUInt32(0);
        writer.WriteInt16((short)(LogAppendTimeAttributes | (short)codec));
        writer.WriteInt32((int)(last.Offset - first.Offset));
        writer.WriteInt64(first.Message.PublishedAt.ToUnixTimeMilliseconds());
        writer.WriteInt64(last.Message.PublishedAt.ToUnixTimeMilliseconds());
        writer.WriteInt64(-1);
        writer.WriteInt16(-1);
        writer.WriteInt32(-1);
        writer.WriteInt32(batch.Count);
        writer.WriteRaw(KafkaRecordsCompression.Compress(codec, [.. batch.SelectMany(item => item.Encoded)]));

        var bytes = writer.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), bytes.Length - 12);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(17), KafkaCrc32C.Compute(bytes.AsSpan(BytesBeforeAttributes)));
        return bytes;
    }
}
