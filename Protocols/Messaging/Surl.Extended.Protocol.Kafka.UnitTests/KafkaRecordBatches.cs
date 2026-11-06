using System.Text;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Record batches built byte by byte from the Kafka "Record Batch" and "Record" layouts, with the
/// CRC-32C computed here bit by bit from the Castagnoli polynomial, independently of the
/// library's <see cref="KafkaCrc32C"/>.
/// </summary>
internal static class KafkaRecordBatches
{
    /// <summary>A record: length, attributes 0, timestampDelta 0, offsetDelta, key, value, headers.</summary>
    public static byte[] Record(int offsetDelta, byte[]? key, byte[]? value, params (byte[]? Key, byte[]? Value)[] headers) =>
        TimedRecord(offsetDelta, 0, key, value, headers);

    /// <summary>A record: length, attributes 0, the given timestampDelta and offsetDelta, key, value, headers.</summary>
    public static byte[] TimedRecord(int offsetDelta, long timestampDelta, byte[]? key, byte[]? value, params (byte[]? Key, byte[]? Value)[] headers)
    {
        byte[] body =
        [
            0x00,
            .. VarLong(timestampDelta),
            .. VarLong(offsetDelta),
            .. VarBytes(key),
            .. VarBytes(value),
            .. VarLong(headers.Length),
            .. headers.SelectMany(header => (byte[])[.. VarBytes(header.Key), .. VarBytes(header.Value)]),
        ];
        return [.. VarLong(body.Length), .. body];
    }

    /// <summary>A record with a UTF-8 value, no key and no headers.</summary>
    public static byte[] Record(int offsetDelta, string value) => Record(offsetDelta, null, Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// A batch holding <paramref name="records"/>: baseOffset 0, partitionLeaderEpoch 0, the
    /// given magic and attributes, lastOffsetDelta the record count less one unless given,
    /// timestamps 0, producerId -1, producerEpoch -1, baseSequence -1, the record count unless
    /// given, and its CRC-32C (XORed with <paramref name="crcFlip"/>).
    /// </summary>
    public static byte[] Batch(byte[][] records, byte magic = 2, short attributes = 0, int? lastOffsetDelta = null, int? count = null, uint crcFlip = 0)
    {
        var recordCount = count ?? records.Length;
        return BatchOfPayload([.. records.SelectMany(record => record)], recordCount, attributes, lastOffsetDelta ?? recordCount - 1, magic, crcFlip);
    }

    /// <summary>
    /// A batch whose bytes after the header are <paramref name="payload"/> - the records,
    /// compressed under the codec in <paramref name="attributes"/> - with the given record count
    /// and lastOffsetDelta the count less one, otherwise as <see cref="Batch"/>.
    /// </summary>
    public static byte[] CompressedBatch(short attributes, int recordCount, byte[] payload) =>
        BatchOfPayload(payload, recordCount, attributes, recordCount - 1, 2, 0);

    private static byte[] BatchOfPayload(byte[] payload, int recordCount, short attributes, int lastOffsetDelta, byte magic, uint crcFlip)
    {
        byte[] checkedPart =
        [
            .. Int16(attributes),
            .. Int32(lastOffsetDelta),
            .. Int64(0),
            .. Int64(0),
            .. Int64(-1),
            .. Int16(-1),
            .. Int32(-1),
            .. Int32(recordCount),
            .. payload,
        ];
        byte[] afterLength = [.. Int32(0), magic, .. UInt32(HandCrc32C(checkedPart) ^ crcFlip), .. checkedPart];
        return [.. Int64(0), .. Int32(afterLength.Length), .. afterLength];
    }

    /// <summary>
    /// A batch as the broker serves it (ADR-0105 decision 4): the given baseOffset,
    /// partitionLeaderEpoch 0, magic 2, attributes 8 (LogAppendTime), lastOffsetDelta the record
    /// count less one, the given baseTimestamp and maxTimestamp, producerId -1, producerEpoch -1,
    /// baseSequence -1, the record count, and its CRC-32C.
    /// </summary>
    public static byte[] ServedBatch(long baseOffset, long baseTimestamp, long maxTimestamp, params byte[][] records)
    {
        byte[] checkedPart =
        [
            .. Int16(8),
            .. Int32(records.Length - 1),
            .. Int64(baseTimestamp),
            .. Int64(maxTimestamp),
            .. Int64(-1),
            .. Int16(-1),
            .. Int32(-1),
            .. Int32(records.Length),
            .. records.SelectMany(record => record),
        ];
        byte[] afterLength = [.. Int32(0), 2, .. UInt32(HandCrc32C(checkedPart)), .. checkedPart];
        return [.. Int64(baseOffset), .. Int32(afterLength.Length), .. afterLength];
    }

    /// <summary>The CRC-32C of <paramref name="bytes"/>, bit by bit: reflected polynomial
    /// 0x82F63B78, initial value and final XOR all ones.</summary>
    public static uint HandCrc32C(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0x82F63B78u : crc >> 1;
            }
        }

        return ~crc;
    }

    /// <summary>A VARINT or VARLONG: zigzag, then seven bits a byte, low first.</summary>
    public static byte[] VarLong(long value)
    {
        var zigzag = (ulong)((value << 1) ^ (value >> 63));
        var bytes = new List<byte>();
        while (zigzag >= 0x80)
        {
            bytes.Add((byte)(zigzag | 0x80));
            zigzag >>= 7;
        }

        bytes.Add((byte)zigzag);
        return [.. bytes];
    }

    public static byte[] VarBytes(byte[]? value) => value is null ? VarLong(-1) : [.. VarLong(value.Length), .. value];

    public static byte[] UInt32(uint value) => Int32((int)value);

    public static byte[] Int64(long value) => [.. Int32((int)(value >> 32)), .. Int32((int)value)];
}
