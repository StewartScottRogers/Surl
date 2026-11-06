using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Reads and checks the record batches of one Produce partition's <c>records</c> (the Kafka
/// "Record Batch" and "Record" sections; ADR-0105 decision 4), every batch in turn, until the
/// bytes end.
/// </summary>
/// <remarks>
/// The checks, in order, for each batch: at least the 61 bytes of a batch header, and a
/// <c>batchLength</c> of at least the 49 bytes after it that stays within <c>records</c>,
/// otherwise <c>CORRUPT_MESSAGE</c> (2); <c>magic</c> 2, otherwise
/// <c>UNSUPPORTED_FOR_MESSAGE_FORMAT</c> (43); the CRC-32C of the bytes from <c>attributes</c> to
/// the batch's end equal to <c>crc</c>, otherwise 2; neither the transactional (bit 4) nor the
/// control (bit 5) attribute, otherwise 43; codec 0 to 4, otherwise 2; for codec 1 to 4, the bytes
/// after the header decompressing under <see cref="KafkaRecordsCompression"/> within the
/// decompression bound, otherwise <c>MESSAGE_TOO_LARGE</c> (10) past the bound and 2 for a payload
/// the codec cannot decode; then, in the records as they are or as decompressed, at least one record, exactly
/// <c>records count</c> of them, each with the <c>offsetDelta</c> of its place, the last at
/// <c>lastOffsetDelta</c>, each <c>length</c> matching its bytes, no negative header count or
/// header key length, and nothing after the last, otherwise 2. Empty or null <c>records</c> is 2.
/// <c>baseOffset</c>, <c>partitionLeaderEpoch</c>, the timestamps, <c>producerId</c>,
/// <c>producerEpoch</c> and <c>baseSequence</c> are not checked.
/// </remarks>
public static class KafkaRecordBatchReader
{
    /// <summary>The bytes of a batch header, up to and including <c>records count</c>: 61.</summary>
    public const int BatchHeaderBytes = 61;

    private const int BatchLengthPosition = 8;
    private const int BatchLengthFieldEnd = 12;
    private const int MagicPosition = 16;
    private const int CrcPosition = 17;
    private const int AttributesPosition = 21;
    private const int LastOffsetDeltaPosition = 23;
    private const int RecordCountPosition = 57;
    private const byte CurrentMagic = 2;
    private const int CodecMask = 0x07;
    private const int TransactionalOrControl = 0x30;
    private const int LastCodec = (int)KafkaCompressionCodec.Zstd;

    private static readonly KafkaRecordBatchReadResult CorruptMessage = new(KafkaErrorCode.CorruptMessage, []);

    /// <summary>Reads every batch in <paramref name="records"/>.</summary>
    /// <param name="records">A partition's <c>records</c>.</param>
    /// <param name="decompressionBound">
    /// The most bytes one compressed batch's records may decompress to
    /// (<see cref="KafkaRecordsCompression.DecompressionBound"/>); 256 MiB unless given.
    /// </param>
    /// <returns>Every record, or the error of the first batch that failed.</returns>
    public static KafkaRecordBatchReadResult Read(ReadOnlyMemory<byte> records, int decompressionBound = KafkaRecordsCompression.BoundWithoutMaxMessage)
    {
        var produced = new List<KafkaProducedRecord>();
        var rest = records;
        do
        {
            if (!TrySplitBatch(ref rest, out var batch))
            {
                return CorruptMessage;
            }

            var error = ReadBatch(batch, decompressionBound, produced);
            if (error != KafkaErrorCode.None)
            {
                return new(error, []);
            }
        }
        while (!rest.IsEmpty);

        return new(KafkaErrorCode.None, produced);
    }

    // Takes the next batch off the front of rest: its header must be whole, and its batchLength
    // must cover the rest of the header and stay within the bytes left.
    private static bool TrySplitBatch(ref ReadOnlyMemory<byte> rest, out ReadOnlyMemory<byte> batch)
    {
        batch = default;
        if (rest.Length < BatchHeaderBytes)
        {
            return false;
        }

        var batchLength = BinaryPrimitives.ReadInt32BigEndian(rest.Span[BatchLengthPosition..]);
        if (batchLength < BatchHeaderBytes - BatchLengthFieldEnd || batchLength > rest.Length - BatchLengthFieldEnd)
        {
            return false;
        }

        batch = rest[..(BatchLengthFieldEnd + batchLength)];
        rest = rest[(BatchLengthFieldEnd + batchLength)..];
        return true;
    }

    private static KafkaErrorCode ReadBatch(ReadOnlyMemory<byte> batch, int decompressionBound, List<KafkaProducedRecord> produced)
    {
        var span = batch.Span;
        if (span[MagicPosition] != CurrentMagic)
        {
            return KafkaErrorCode.UnsupportedForMessageFormat;
        }

        if (KafkaCrc32C.Compute(span[AttributesPosition..]) != BinaryPrimitives.ReadUInt32BigEndian(span[CrcPosition..]))
        {
            return KafkaErrorCode.CorruptMessage;
        }

        var attributes = BinaryPrimitives.ReadInt16BigEndian(span[AttributesPosition..]);
        var attributeError = AttributeError(attributes);
        if (attributeError != KafkaErrorCode.None)
        {
            return attributeError;
        }

        var codec = (KafkaCompressionCodec)(attributes & CodecMask);
        var payload = batch[BatchHeaderBytes..];
        if (codec == KafkaCompressionCodec.None)
        {
            return ReadRecords(span, payload, produced);
        }

        var decompressionError = KafkaRecordsCompression.Decompress(codec, payload.Span, decompressionBound, out var decompressed);
        return decompressionError != KafkaErrorCode.None ? decompressionError : ReadRecords(span, decompressed, produced);
    }

    private static KafkaErrorCode AttributeError(short attributes) =>
        (attributes & TransactionalOrControl) != 0 ? KafkaErrorCode.UnsupportedForMessageFormat
        : (attributes & CodecMask) <= LastCodec ? KafkaErrorCode.None
        : KafkaErrorCode.CorruptMessage;

    // The records of a batch whose header is header, from recordBytes: the bytes after the
    // header, or those bytes decompressed.
    private static KafkaErrorCode ReadRecords(ReadOnlySpan<byte> header, ReadOnlyMemory<byte> recordBytes, List<KafkaProducedRecord> produced)
    {
        var lastOffsetDelta = BinaryPrimitives.ReadInt32BigEndian(header[LastOffsetDeltaPosition..]);
        var count = BinaryPrimitives.ReadInt32BigEndian(header[RecordCountPosition..]);
        if (count < 1 || lastOffsetDelta != count - 1)
        {
            return KafkaErrorCode.CorruptMessage;
        }

        var reader = new KafkaPrimitiveReader(recordBytes);
        var records = new List<KafkaProducedRecord>();
        try
        {
            for (var index = 0; index < count; index++)
            {
                records.Add(ReadRecord(reader, index));
            }
        }
        catch (KafkaMalformedRequestException)
        {
            return KafkaErrorCode.CorruptMessage;
        }

        if (reader.Remaining != 0)
        {
            return KafkaErrorCode.CorruptMessage;
        }

        produced.AddRange(records);
        return KafkaErrorCode.None;
    }

    // One record; a record that disagrees with its batch throws, as a truncated one does.
    private static KafkaProducedRecord ReadRecord(KafkaPrimitiveReader reader, int index)
    {
        var length = reader.ReadVarInt();
        var start = reader.Position;
        reader.ReadInt8();
        reader.ReadVarLong();
        var offsetDelta = reader.ReadVarInt();
        var key = ReadVarIntBytes(reader);
        var value = ReadVarIntBytes(reader);
        var headers = ReadHeaders(reader);
        return offsetDelta == index && reader.Position - start == length
            ? new KafkaProducedRecord(key, value, headers)
            : throw new KafkaMalformedRequestException($"Record {index} disagrees with its batch.");
    }

    private static List<BusMessageHeader> ReadHeaders(KafkaPrimitiveReader reader)
    {
        var count = reader.ReadVarInt();
        if (count < 0)
        {
            throw new KafkaMalformedRequestException($"A record's header count is {count}.");
        }

        var headers = new List<BusMessageHeader>();
        for (var index = 0; index < count; index++)
        {
            if (ReadHeader(reader) is { } header)
            {
                headers.Add(header);
            }
        }

        return headers;
    }

    // One header; null when its value is null or either part is not well-formed UTF-8.
    private static BusMessageHeader? ReadHeader(KafkaPrimitiveReader reader)
    {
        var name = ReadVarIntBytes(reader) ?? throw new KafkaMalformedRequestException("A header key is null.");
        var value = ReadVarIntBytes(reader);
        return value is { } present && Utf8.IsValid(name.Span) && Utf8.IsValid(present.Span)
            ? new BusMessageHeader(Encoding.UTF8.GetString(name.Span), Encoding.UTF8.GetString(present.Span))
            : null;
    }

    // A VARINT length, negative for null, then that many bytes.
    private static ReadOnlyMemory<byte>? ReadVarIntBytes(KafkaPrimitiveReader reader)
    {
        var length = reader.ReadVarInt();
        return length < 0 ? (ReadOnlyMemory<byte>?)null : reader.ReadRawBytes(length);
    }
}
