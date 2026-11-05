using System.Buffers.Binary;
using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Builds the record batches a Fetch serves from a topic log's records (ADR-0105 decisions 3 and
/// 4; the Kafka "Record Batch" and "Record" layouts): each batch with <c>baseOffset</c> its first
/// record's offset, <c>partitionLeaderEpoch</c> 0, <c>magic</c> 2, attributes LogAppendTime (bit 3)
/// and no codec, <c>baseTimestamp</c> and <c>maxTimestamp</c> its first and last record's publish
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

    // The batch's attributes: timestamp type LogAppendTime (bit 3), no codec, not transactional.
    private const short LogAppendTimeAttributes = 0x08;

    // The bytes from the start of the batch to the end of the crc field: baseOffset,
    // batchLength, partitionLeaderEpoch, magic, crc.
    private const int BytesBeforeAttributes = 21;

    /// <summary>
    /// Writes <paramref name="records"/>, in order, as batches, adding records while the batches
    /// stay within <paramref name="maxBytes"/>; when <paramref name="takeFirstWhole"/> is true the
    /// first record is written whatever it costs, so the first batch of a response always makes
    /// progress (KIP-74).
    /// </summary>
    /// <param name="records">The records, in offset order, contiguous.</param>
    /// <param name="maxBytes">The most bytes the batches may take together.</param>
    /// <param name="takeFirstWhole">Whether the first record is written even past <paramref name="maxBytes"/>.</param>
    /// <returns>The batches' bytes, back to back; empty when no record fits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
    public static byte[] Write(IReadOnlyList<BusLogRecord> records, long maxBytes, bool takeFirstWhole)
    {
        ArgumentNullException.ThrowIfNull(records);

        var output = new List<byte>();
        var batch = new List<(BusLogRecord Record, byte[] Encoded)>();
        var total = 0L;
        foreach (var record in records)
        {
            var startsBatch = batch.Count is 0 or MaxRecordsPerBatch;
            var encoded = EncodeRecord(record, startsBatch ? record : batch[0].Record);
            var cost = encoded.Length + (startsBatch ? BatchHeaderBytes : 0);
            if (total + cost > maxBytes && !(total == 0 && takeFirstWhole))
            {
                break;
            }

            if (batch.Count == MaxRecordsPerBatch)
            {
                output.AddRange(EncodeBatch(batch));
                batch.Clear();
            }

            batch.Add((record, encoded));
            total += cost;
        }

        if (batch.Count > 0)
        {
            output.AddRange(EncodeBatch(batch));
        }

        return [.. output];
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

    private static byte[] EncodeBatch(List<(BusLogRecord Record, byte[] Encoded)> batch)
    {
        var first = batch[0].Record;
        var last = batch[^1].Record;
        var writer = new KafkaPrimitiveWriter();
        writer.WriteInt64(first.Offset);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt8(2);
        writer.WriteUInt32(0);
        writer.WriteInt16(LogAppendTimeAttributes);
        writer.WriteInt32((int)(last.Offset - first.Offset));
        writer.WriteInt64(first.Message.PublishedAt.ToUnixTimeMilliseconds());
        writer.WriteInt64(last.Message.PublishedAt.ToUnixTimeMilliseconds());
        writer.WriteInt64(-1);
        writer.WriteInt16(-1);
        writer.WriteInt32(-1);
        writer.WriteInt32(batch.Count);
        foreach (var item in batch)
        {
            writer.WriteRaw(item.Encoded);
        }

        var bytes = writer.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), bytes.Length - 12);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(17), KafkaCrc32C.Compute(bytes.AsSpan(BytesBeforeAttributes)));
        return bytes;
    }
}
